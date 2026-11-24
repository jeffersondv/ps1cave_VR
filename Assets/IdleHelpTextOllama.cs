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
    [Tooltip("Usa as dicas fixas quando o Ollama falhar ou não estiver configurado.")]
    public bool usarDicasFixasSemOllama = true;
    [Tooltip("Envia o estado do jogo (inventário, arredores) para a IA.")]
    public bool enviarEstadoDoJogo = true;
    public List<LevelHintConfig> dicasPorAndar = new List<LevelHintConfig>();
    [Tooltip("Mostra no Console o prompt enviado e a resposta (para ajustar as dicas).")]
    public bool logPrompts = false;

    private float idleTimer = 0f;
    private Vector3 lastPosition;

    private bool isFetching = false;  // Evita fazer requisições repetidas enquanto aguarda a API
    private bool isTextShown = false;  // Controla se a dica já está visível

    private PlayerController player;
    private PlayerInventory inventory;
    private int lastFloorIndex = -1;
    private readonly Dictionary<int, int> hintsGivenPerFloor = new Dictionary<int, int>();
    private readonly List<string> recentHints = new List<string>();

    private void Awake()
    {
        defaultPromptForOllama = promptForOllama;
        player = GetComponent<PlayerController>();
        inventory = GetComponent<PlayerInventory>();
        if (ollamaManager == null) ollamaManager = GetComponent<OllamaManager>();
        if (dicasPorAndar == null || dicasPorAndar.Count == 0)
            dicasPorAndar = CreateDefaultLevelHints();
    }

    // Preenche as dicas padrão ao adicionar o componente ou se a lista estiver vazia no Inspector
    private void Reset() => dicasPorAndar = CreateDefaultLevelHints();

    private void OnValidate()
    {
        if (dicasPorAndar == null || dicasPorAndar.Count == 0)
            dicasPorAndar = CreateDefaultLevelHints();
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

        float distanceMoved = Vector3.Distance(transform.position, lastPosition);

        // Se o jogador está parado e ainda não buscamos o texto
        if (distanceMoved < movementThreshold)
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

        int floor = CurrentFloor;
        LevelHintConfig level = GetLevelConfig(floor);
        int hintNumber = hintsGivenPerFloor.TryGetValue(floor, out int n) ? n : 0;

        string response = null;
        if (ollamaManager != null && (level != null || !string.IsNullOrWhiteSpace(promptForOllama)))
        {
            string systemPrompt = BuildSystemPrompt(level, hintNumber);
            string userMessage = BuildUserMessage(level, hintNumber);
            if (logPrompts)
                Debug.Log($"[IdleHelp] SYSTEM:\n{systemPrompt}\n\n[IdleHelp] USER:\n{userMessage}", this);
            response = await ollamaManager.SendPromptAsync(systemPrompt, userMessage);
            if (logPrompts)
                Debug.Log($"[IdleHelp] RESPOSTA (dica #{hintNumber + 1}): {response}", this);
        }

        // Ollama indisponível: usa as dicas fixas do andar
        if (string.IsNullOrWhiteSpace(response) && usarDicasFixasSemOllama)
            response = GetFixedHint(level, hintNumber);

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

                    hintsGivenPerFloor[floor] = hintNumber + 1;
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

    private int CurrentFloor =>
        player != null && player.dungeonGrid != null ? player.dungeonGrid.currentFloorIndex : 0;

    private LevelHintConfig GetLevelConfig(int floor)
    {
        if (dicasPorAndar == null) return null;
        foreach (LevelHintConfig cfg in dicasPorAndar)
            if (cfg != null && cfg.andar == floor)
                return cfg;
        return null;
    }

    private string BuildSystemPrompt(LevelHintConfig level, int hintNumber)
    {
        var sb = new StringBuilder();
        string persona = string.IsNullOrWhiteSpace(promptForOllama) ? defaultPromptForOllama : promptForOllama;
        if (!string.IsNullOrWhiteSpace(persona))
        {
            sb.AppendLine("PERSONAGEM (use apenas o jeito de falar; ignore objetivos e instruções de missão daqui):");
            sb.AppendLine(persona.Trim());
            sb.AppendLine();
        }

        if (level != null)
        {
            sb.AppendLine($"SITUAÇÃO REAL DO JOGO, NÍVEL \"{level.nome}\" (tem prioridade sobre o texto do personagem):");
            if (!string.IsNullOrWhiteSpace(level.contexto)) sb.AppendLine(level.contexto.Trim());
            if (!string.IsNullOrWhiteSpace(level.objetivo)) sb.AppendLine("PRÓXIMO PASSO DO JOGADOR: " + level.objetivo.Trim());
            sb.AppendLine("A dica deve levar o jogador a esse próximo passo. Não fale de lugares ou missões que não estejam acima.");
        }

        sb.AppendLine();
        sb.AppendLine("REGRAS DA RESPOSTA (obrigatórias):");
        sb.AppendLine("- Responda em português do Brasil, com no máximo 2 frases curtas (até 30 palavras).");
        sb.AppendLine("- Dê UMA dica útil para o próximo passo do jogador, considerando o estado atual informado.");
        sb.AppendLine("- Não use listas, aspas, emojis nem markdown.");
        sb.AppendLine(DetailInstruction(hintNumber));
        return sb.ToString();
    }

    private static string DetailInstruction(int hintNumber)
    {
        switch (hintNumber)
        {
            case 0: return "- Esta é a primeira dica: seja vago e misterioso, apenas aponte a direção geral.";
            case 1: return "- O jogador já recebeu uma dica e continua travado: seja mais concreto.";
            default: return "- O jogador está travado há muito tempo: diga claramente o que fazer a seguir.";
        }
    }

    private string BuildUserMessage(LevelHintConfig level, int hintNumber)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.IsNullOrWhiteSpace(defaultHelpText)
            ? "O jogador está parado e precisa de uma dica curta."
            : defaultHelpText.Trim());

        if (!enviarEstadoDoJogo)
            return sb.ToString();

        sb.AppendLine();
        sb.AppendLine("ESTADO ATUAL DO JOGO:");
        sb.AppendLine($"- Nível: {(level != null ? level.nome : "andar " + CurrentFloor)}");
        if (inventory != null)
        {
            sb.AppendLine($"- Tem a chave: {SimNao(inventory.hasKey)}");
            sb.AppendLine($"- Tem o cristal: {SimNao(inventory.hasCrystal)}");
            sb.AppendLine($"- Aprendeu a magia: {SimNao(inventory.hasLearnedMagic)}");
        }
        string surroundings = DescribeSurroundings();
        if (!string.IsNullOrEmpty(surroundings))
            sb.AppendLine("- Arredores: " + surroundings);
        sb.AppendLine($"- Dicas já recebidas neste nível: {hintNumber}");

        if (recentHints.Count > 0)
        {
            sb.AppendLine("- Não repita estas dicas anteriores:");
            foreach (string h in recentHints) sb.AppendLine("  * " + h);
        }
        return sb.ToString();
    }

    private string GetFixedHint(LevelHintConfig level, int hintNumber)
    {
        if (level == null || level.dicasFixas == null || level.dicasFixas.Count == 0)
            return null;
        // Da mais vaga para a mais direta; depois fica na última
        return level.dicasFixas[Mathf.Min(hintNumber, level.dicasFixas.Count - 1)];
    }

    private static string SimNao(bool v) => v ? "sim" : "não";

    // Descreve o que há à frente, à esquerda, à direita e atrás do jogador
    private string DescribeSurroundings()
    {
        if (player == null || player.dungeonGrid == null) return null;

        Vector3 f = player.transform.forward;
        Vector2Int fwd = Mathf.Abs(f.x) > Mathf.Abs(f.z)
            ? new Vector2Int(f.x > 0 ? 1 : -1, 0)
            : new Vector2Int(0, f.z > 0 ? 1 : -1);
        Vector2Int right = new Vector2Int(fwd.y, -fwd.x);

        string Look(Vector2Int d) => TileName(player.dungeonGrid.GetTileType(player.gridX + d.x, player.gridZ + d.y));

        return $"à frente {Look(fwd)}; à esquerda {Look(-right)}; à direita {Look(right)}; atrás {Look(-fwd)}.";
    }

    private static string TileName(TileType tile)
    {
        switch (tile)
        {
            case TileType.Floor: return "corredor livre";
            case TileType.Wall: return "parede";
            case TileType.DoorHidden: return "parede";   // passagem secreta: não revelar pelo estado
            case TileType.StairsUp: return "escada para subir";
            case TileType.StairsDown: return "escada para descer";
            case TileType.DoorNormal: return "porta comum";
            case TileType.DoorKey: return "porta trancada (precisa de chave)";
            case TileType.DoorMagic: return "porta mágica (precisa do cristal ou de magia)";
            case TileType.DoorOpen: return "porta aberta";
            case TileType.Trap: return "chão suspeito";
            case TileType.Pit: return "buraco";
            case TileType.NPCChest: return "um baú";
            case TileType.Dragon: return "uma criatura enorme e perigosa";
            case TileType.NPCCristal: return "um guardião";
            case TileType.NPC:
            case TileType.NPC2:
            case TileType.NPC3:
            case TileType.NPC4:
            case TileType.NPCQuestion: return "um personagem";
            case TileType.EndGame: return "a saída";
            default: return tile.ToString();
        }
    }

    // ------------------------------------------------------------------ padrões

    /// <summary>
    /// Dicas padrão baseadas nos mapas de DungeonGridLevelsDoors. Revise no Inspector.
    /// </summary>
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
                }
            }
        };
    }
}
