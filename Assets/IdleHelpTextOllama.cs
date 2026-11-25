using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks; // Necessário para chamadas assíncronas
using TMPro;
using UnityEngine;

/// <summary>
/// Dica automática quando o jogador fica parado.
///
/// Para cada andar existe uma configuração (contexto, objetivo e dicas fixas). O prompt enviado
/// ao Ollama junta: persona (Velho Sábio ou o último NPC com quem o jogador falou) + contexto do
/// andar + estado real do jogo (inventário, o que há à frente/lados, dicas já dadas).
/// Quanto mais vezes o jogador trava no mesmo andar, mais direta fica a dica.
/// Se o Ollama não responder (sem rede no Quest, por exemplo), usa as dicas fixas do andar.
/// </summary>
public class IdleHelpTextOllama : MonoBehaviour
{
    [Serializable]
    public class LevelHintConfig
    {
        [Tooltip("Índice do andar em DungeonGridLevelsDoors (0 = primeiro mapa).")]
        public int andar;
        public string nome = "Andar";
        [TextArea(3, 8)]
        [Tooltip("O que existe neste andar (para a IA). O jogador não vê este texto.")]
        public string contexto;
        [TextArea(2, 5)]
        [Tooltip("O que o jogador precisa fazer neste andar.")]
        public string objetivo;
        [Tooltip("Dicas fixas, da mais vaga para a mais direta. Usadas se o Ollama falhar.")]
        public List<string> dicasFixas = new List<string>();
        [Tooltip("Etapas que dependem do estado do jogador (verificadas em ordem; a 1ª que valer substitui objetivo e dicas acima).")]
        public List<HintStage> etapas = new List<HintStage>();
    }

    public enum HintCondition { Sempre, SemChave, ComChave, SemCristal, ComCristal }

    [Serializable]
    public class HintStage
    {
        [Tooltip("Quando esta etapa vale. ComChave inclui já ter usado a chave numa porta.")]
        public HintCondition quando = HintCondition.Sempre;
        [TextArea(2, 4)]
        public string objetivo;
        [Tooltip("Dicas da mais vaga para a mais direta.")]
        public List<string> dicas = new List<string>();
    }

    public enum OllamaHintMode
    {
        [Tooltip("O jogo escolhe a dica certa e o Ollama só a reescreve com a voz do NPC (mais confiável).")]
        ReescreverDica,
        [Tooltip("O Ollama cria a dica a partir do contexto do andar e do estado (mais variado, menos preciso).")]
        GerarComContexto
    }

    [Header("UI")]
    public TextMeshProUGUI helpText;

    [Header("Referência da IA")]
    public OllamaManager ollamaManager; // Arraste seu objeto OllamaManager no Inspector
    [TextArea]
    [Tooltip("Persona padrão. É trocada pelo prompt do último NPC com quem o jogador falou.")]
    public string promptForOllama = "";

    [Tooltip("Pedido base enviado junto com o estado do jogo.")]
    public string defaultHelpText = "";

    private string defaultPromptForOllama;

    [Header("Configurações")]
    public float idleTimeLimit = 5f;
    public float movementThreshold = 0.02f;

    [Header("Dicas por andar")]
    [Tooltip("ReescreverDica: a dica vem das etapas/dicas fixas e o Ollama só dá a voz do personagem.")]
    public OllamaHintMode modo = OllamaHintMode.ReescreverDica;
    [Tooltip("Usa as dicas fixas quando o Ollama falhar ou não estiver configurado.")]
    public bool usarDicasFixasSemOllama = true;
    [Tooltip("Envia o estado do jogo (inventário, arredores) para a IA.")]
    public bool enviarEstadoDoJogo = true;
    public List<LevelHintConfig> dicasPorAndar = new List<LevelHintConfig>();
    [Header("Tamanho da resposta")]
    [Tooltip("Limite de palavras pedido ao modelo.")]
    [Min(8)] public int maxPalavras = 25;
    [Tooltip("A resposta é cortada (em fim de frase) se passar disto.")]
    [Min(40)] public int maxCaracteres = 160;
    [Tooltip("A persona do NPC é reduzida a este tamanho (personas longas confundem modelos pequenos).")]
    [Min(0)] public int maxPersonaChars = 200;

    [Tooltip("Mostra no Console o prompt enviado e a resposta (para ajustar as dicas).")]
    public bool logPrompts = false;

    private float idleTimer = 0f;
    private Vector3 lastPosition;

    private bool isFetching = false;  // Evita fazer requisições repetidas enquanto aguarda a API
    private bool isTextShown = false;  // Controla se a dica já está visível

    private PlayerController player;
    private int lastFloorIndex = -1;
    private readonly Dictionary<string, int> hintsGivenPerStage = new Dictionary<string, int>();
    private string lastStageId;
    private readonly List<string> recentHints = new List<string>();

    private void Awake()
    {
        defaultPromptForOllama = promptForOllama;
        player = GetComponent<PlayerController>();
        if (ollamaManager == null) ollamaManager = GetComponent<OllamaManager>();
        if (dicasPorAndar == null || dicasPorAndar.Count == 0)
            dicasPorAndar = CreateDefaultLevelHints();
        else
            FillMissingStages(dicasPorAndar);
    }

    /// <summary>Andares salvos antes das "etapas" existirem recebem as etapas padrão.</summary>
    public static bool FillMissingStages(List<LevelHintConfig> levels)
    {
        bool changed = false;
        List<LevelHintConfig> defaults = CreateDefaultLevelHints();
        foreach (LevelHintConfig cfg in levels)
        {
            if (cfg == null || (cfg.etapas != null && cfg.etapas.Count > 0)) continue;
            LevelHintConfig def = defaults.Find(d => d.andar == cfg.andar);
            if (def == null || def.etapas.Count == 0) continue;
            cfg.etapas = def.etapas;
            changed = true;
        }
        return changed;
    }

    // Preenche as dicas padrão ao adicionar o componente ou se a lista estiver vazia no Inspector
    private void Reset() => dicasPorAndar = CreateDefaultLevelHints();

    private void OnValidate()
    {
        if (dicasPorAndar == null || dicasPorAndar.Count == 0)
            dicasPorAndar = CreateDefaultLevelHints();
        else
            FillMissingStages(dicasPorAndar);
    }

    private void Start()
    {
        lastPosition = transform.position;

        if (helpText != null)
        {
            helpText.gameObject.SetActive(false);
        }
    }

    public void SetPromptForOllama(string prompt)
    {
        promptForOllama = string.IsNullOrWhiteSpace(prompt)
            ? defaultPromptForOllama
            : prompt;

        ResetIdle();
    }

    public void DisableHelpText()
    {
        ResetIdle();
        enabled = false;
    }

    private void Update()
    {
        // Ao trocar de andar, a persona volta ao padrão e as dicas recomeçam vagas
        int floor = CurrentFloor;
        if (floor != lastFloorIndex)
        {
            lastFloorIndex = floor;
            promptForOllama = defaultPromptForOllama;
            recentHints.Clear();
        }

        // Mudou a etapa (pegou a chave, o cristal...): dicas anteriores não valem mais
        string stageId = GameHintContext.CurrentStageId(player);
        if (stageId != lastStageId)
        {
            lastStageId = stageId;
            recentHints.Clear();
        }

        float distanceMoved = Vector3.Distance(transform.position, lastPosition);

        // Uma mensagem de diálogo (porta, NPC, item...) tem prioridade: esconde a dica
        // para os textos não se sobreporem e só volta a contar a inatividade depois dela.
        if (HelpMenu.IsOpen ||
            (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive()))
        {
            if (isTextShown || isFetching || idleTimer > 0f)
                ResetIdle();
        }
        // Se o jogador está parado e ainda não buscamos o texto
        else if (distanceMoved < movementThreshold)
        {
            if (!isTextShown && !isFetching)
            {
                idleTimer += Time.deltaTime;

                if (idleTimer >= idleTimeLimit)
                {
                    // Chama o método assíncrono para obter o texto da IA
                    _ = FetchHelpFromOllama();
                }
            }
        }
        else
        {
            // O jogador se moveu: cancela e limpa a interface
            ResetIdle();
        }

        lastPosition = transform.position;
    }

    private async Task FetchHelpFromOllama()
    {
        isFetching = true;

        LevelHintConfig level = GetLevelConfig(CurrentFloor);
        string stageId = GameHintContext.CurrentStageId(player);
        int hintNumber = hintsGivenPerStage.TryGetValue(stageId, out int n) ? n : 0;
        string fixedHint = GameHintContext.SelectHint(level, player, hintNumber, out string objective);

        string response = null;
        if (ollamaManager != null)
        {
            string persona = string.IsNullOrWhiteSpace(promptForOllama) ? defaultPromptForOllama : promptForOllama;
            string systemPrompt, userMessage;
            if (modo == OllamaHintMode.ReescreverDica)
            {
                systemPrompt = GameHintContext.RewriteSystemPrompt(persona, maxPersonaChars, maxPalavras);
                userMessage = string.IsNullOrWhiteSpace(fixedHint) ? null : "Dica: " + fixedHint;
            }
            else
            {
                systemPrompt = BuildSystemPrompt(level, objective, persona, hintNumber);
                userMessage = BuildUserMessage(hintNumber);
            }

            if (userMessage != null)
            {
                if (logPrompts)
                    Debug.Log($"[IdleHelp] SYSTEM:\n{systemPrompt}\n\n[IdleHelp] USER:\n{userMessage}", this);
                response = await ollamaManager.SendPromptAsync(systemPrompt, userMessage);
                response = GameHintContext.CleanResponse(response, 2, maxCaracteres);
                if (logPrompts)
                    Debug.Log($"[IdleHelp] RESPOSTA (dica #{hintNumber + 1}): {response}", this);
            }
        }

        // Ollama indisponível ou resposta inválida: usa a dica fixa da etapa
        if (string.IsNullOrWhiteSpace(response) && usarDicasFixasSemOllama)
            response = fixedHint;

        // Nada a mostrar: tenta de novo no próximo ciclo de inatividade
        if (string.IsNullOrWhiteSpace(response))
        {
            ResetIdle();
            return;
        }

        // Verifica se o jogador continua parado após a resposta do Ollama chegar
        if (isFetching)
        {

            if (DialogueManager.Instance != null)
            {

                if (DialogueManager.Instance.IsDialogueActive()) // Reseta o timer para evitar múltiplas chamadas
                {
                    ResetIdle();
                }else{

                    response = response.Trim();
                    if (helpText != null)
                    {
                        helpText.text = response;
                        helpText.gameObject.SetActive(true);
                    }
                    isTextShown = true;

                    hintsGivenPerStage[stageId] = hintNumber + 1;
                    recentHints.Add(response);
                    if (recentHints.Count > 3) recentHints.RemoveAt(0);

                    DialogueManager.Instance.SetIdleHelpVisible(true);
                    DialogueManager.Instance.ClearMessages(); // Limpa mensagens anteriores para evitar sobreposição

                }
            }

        }

        isFetching = false;
    }

    private void ResetIdle()
    {
        idleTimer = 0f;
        isFetching = false;
        isTextShown = false;

        if (helpText != null && helpText.gameObject.activeSelf)
        {
            helpText.gameObject.SetActive(false);
        }

        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.SetIdleHelpVisible(false);
        }
    }

    // ------------------------------------------------------------------ prompts

    private int CurrentFloor => GameHintContext.CurrentFloor(player);

    /// <summary>Configuração do andar indicado (null se não houver).</summary>
    public LevelHintConfig GetLevelConfig(int floor)
    {
        if (dicasPorAndar == null) return null;
        foreach (LevelHintConfig cfg in dicasPorAndar)
            if (cfg != null && cfg.andar == floor)
                return cfg;
        return null;
    }

    // Modo GerarComContexto: o modelo cria a dica a partir dos fatos e do estado
    private string BuildSystemPrompt(LevelHintConfig level, string objective, string persona, int hintNumber)
    {
        var sb = new StringBuilder();
        persona = GameHintContext.ShortPersona(persona, maxPersonaChars);
        sb.AppendLine("Você é um personagem de um jogo de exploração de masmorra e fala com o jogador.");
        if (!string.IsNullOrWhiteSpace(persona))
            sb.AppendLine("SEU JEITO DE FALAR (use só o estilo; ignore missões e lugares citados aqui): " + persona);
        sb.AppendLine();

        sb.Append(GameHintContext.LevelFacts(level, objective));
        sb.AppendLine();
        sb.Append(GameHintContext.ResponseRules(maxPalavras));
        sb.AppendLine(GameHintContext.DetailInstruction(hintNumber));
        return sb.ToString();
    }

    private string BuildUserMessage(int hintNumber)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.IsNullOrWhiteSpace(defaultHelpText)
            ? "O jogador está parado e precisa de uma dica curta."
            : defaultHelpText.Trim());

        if (!enviarEstadoDoJogo)
            return sb.ToString();

        sb.AppendLine();
        sb.Append(GameHintContext.PlayerState(player));
        sb.AppendLine($"- Dicas já recebidas nesta etapa: {hintNumber}");

        if (recentHints.Count > 0)
        {
            sb.AppendLine("- Não repita estas dicas anteriores:");
            foreach (string h in recentHints) sb.AppendLine("  * " + h);
        }
        return sb.ToString();
    }

    // ------------------------------------------------------------------ padrões

    /// <summary>
    /// Dicas padrão baseadas nos mapas de DungeonGridLevelsDoors. Revise no Inspector.
    /// </summary>
    static HintStage Stage(HintCondition when, string objective, params string[] hints) =>
        new HintStage { quando = when, objetivo = objective, dicas = new List<string>(hints) };

    public static List<LevelHintConfig> CreateDefaultLevelHints()
    {
        return new List<LevelHintConfig>
        {
            new LevelHintConfig
            {
                andar = 0, nome = "Salão de entrada",
                contexto = "Corredor curto com um Mago no fim. Não há porta visível: no meio do corredor existe " +
                           "uma passagem secreta na parede que leva a uma escada para descer. O Mago dá uma pista " +
                           "especial se o jogador falar com ele duas vezes.",
                objetivo = "Falar com o Mago (duas vezes) e encontrar a passagem secreta na parede para descer a escada.",
                dicasFixas = new List<string>
                {
                    "Quem guarda este salão sabe mais do que diz. Insista na conversa.",
                    "Nem toda parede é sólida. Procure no meio do corredor.",
                    "Fique no meio do corredor, vire-se para a parede e siga em frente: a passagem secreta leva à escada."
                },
                etapas = new List<HintStage>
                {
                    Stage(HintCondition.ComCristal, "Subir até o primeiro andar e abrir a porta mágica com o cristal.",
                        "O cristal pulsa: a porta que ele abre fica acima de você.",
                        "Volte pela escada e procure a porta mágica no primeiro andar.")
                }
            },
            new LevelHintConfig
            {
                andar = 1, nome = "Primeiro andar",
                contexto = "Há uma porta comum, uma escada para descer e uma porta mágica que leva à saída da caverna. " +
                           "A porta mágica só abre com o cristal (ou com magia), que fica nos andares de baixo.",
                objetivo = "Descer a escada para buscar o cristal e depois voltar para abrir a porta mágica da saída.",
                dicasFixas = new List<string>
                {
                    "A saída está perto, mas protegida por uma força mágica.",
                    "A porta mágica não cede sem o cristal. Ele está nas profundezas.",
                    "Desça a escada deste andar e procure o cristal; depois volte aqui e atravesse a porta mágica."
                },
                etapas = new List<HintStage>
                {
                    Stage(HintCondition.ComCristal, "Usar o cristal na porta mágica deste andar para sair da caverna.",
                        "O cristal reage a uma força mágica próxima.",
                        "Leve o cristal até a porta mágica deste andar.",
                        "Vá até a porta mágica e interaja com ela: o cristal vai abri-la e libertar você.")
                }
            },
            new LevelHintConfig
            {
                andar = 2, nome = "Segundo andar",
                contexto = "Andar de passagem. Corredores levam a uma escada para descer. Há uma porta trancada com chave " +
                           "que leva a um guia; a chave fica no andar de baixo.",
                objetivo = "Encontrar a escada para descer e continuar a busca pelo cristal.",
                dicasFixas = new List<string>
                {
                    "O caminho continua para baixo.",
                    "Contorne os corredores: há uma escada para descer.",
                    "Ignore a porta trancada por enquanto e siga pelos corredores até a escada para descer."
                },
                etapas = new List<HintStage>
                {
                    Stage(HintCondition.ComCristal, "Subir a escada de volta ao primeiro andar e abrir a porta mágica.",
                        "Com o cristal em mãos, o caminho agora é para cima.",
                        "Encontre a escada para subir e volte ao primeiro andar.")
                }
            },
            new LevelHintConfig
            {
                andar = 3, nome = "Terceiro andar",
                contexto = "Andar com vários corredores e portas comuns. Há um baú com a chave do calabouço, um " +
                           "personagem estranho e guias que dão conselhos. Uma escada desce para o andar do dragão.",
                objetivo = "Pegar a chave no baú (interagir com ele) e depois descer a escada.",
                dicasFixas = new List<string>
                {
                    "Algo guardado aqui abrirá uma porta lá embaixo.",
                    "Procure o baú: dentro dele há uma chave.",
                    "Interaja com o baú para pegar a chave e depois desça a escada deste andar."
                },
                etapas = new List<HintStage>
                {
                    Stage(HintCondition.ComCristal, "Subir de volta ao primeiro andar e abrir a porta mágica.",
                        "Com o cristal em mãos, o caminho agora é para cima.",
                        "Procure a escada para subir e volte ao primeiro andar."),
                    Stage(HintCondition.ComChave, "Descer a escada até o andar mais profundo.",
                        "A chave pesa no seu bolso: a porta dela fica mais abaixo.",
                        "Procure a escada para descer deste andar.",
                        "Siga até a escada para descer: a porta trancada está no andar de baixo.")
                }
            },
            new LevelHintConfig
            {
                andar = 4, nome = "Covil do dragão",
                contexto = "Andar mais profundo. Um dragão mortal fica em um corredor: falar com ele encerra a aventura. " +
                           "Uma porta trancada com chave e uma porta comum levam ao Dezoriano, que guarda o cristal.",
                objetivo = "Evitar o dragão, abrir a porta trancada com a chave, falar com o Dezoriano para obter o cristal " +
                           "e voltar ao primeiro andar para abrir a porta mágica.",
                dicasFixas = new List<string>
                {
                    "Cuidado: nem toda criatura aqui quer conversar.",
                    "Evite o dragão. Use a chave na porta trancada e procure o guardião do cristal.",
                    "Não fale com o dragão. Abra a porta trancada, fale com o Dezoriano para pegar o cristal e volte à porta mágica."
                },
                etapas = new List<HintStage>
                {
                    Stage(HintCondition.ComCristal, "Voltar subindo até o primeiro andar e abrir a porta mágica.",
                        "O cristal é seu. Agora fuja das profundezas.",
                        "Evite o dragão e suba as escadas até o primeiro andar.",
                        "Suba todas as escadas até o primeiro andar e use o cristal na porta mágica."),
                    Stage(HintCondition.SemChave, "Voltar ao andar de cima e pegar a chave no baú.",
                        "Uma porta aqui exige algo que você deixou para trás.",
                        "A porta trancada precisa da chave do baú do andar de cima.",
                        "Suba a escada, pegue a chave no baú e volte para abrir a porta trancada.")
                }
            }
        };
    }
}
