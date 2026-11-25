using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class DungeonNPC : MonoBehaviour
{
    [SerializeField] private OllamaManager ollamaManager;

    [Tooltip("Quem é o NPC e o que ele sabe. Seja curto: 1 a 3 frases. O estado do jogo é adicionado automaticamente.")]
    [TextArea(3, 5)]
    [SerializeField] private string systemPrompt =
        "Você é um velho mago preso na masmorra de Phantasy Star. " +
        "Responda em no máximo 2 frases, de forma mística e retro." +
		"Você sabe que essa caverna é Naula e tem uma padaria.";

    [Tooltip("Frase que o jogador \"diz\" ao NPC ao interagir.")]
    [SerializeField] private string playerGreeting = "Olá, qual o nome desse lugar ?";

    [Tooltip("Título da caixa de diálogo quando o NPC não tem nome definido.")]
    [SerializeField] private string defaultTitle = "???";

    [Header("Ajuda especial (Ollama)")]
    [Tooltip("Quantas interações até aparecer a opção \"Ajuda especial\".")]
    [Min(1)] public int interactionsForSpecialHelp = 3;

    [Tooltip("Pergunta enviada ao Ollama quando o jogador escolhe \"Ajuda especial\".")]
    [TextArea(2, 4)]
    [SerializeField] private string specialHelpRequest =
        "Estou perdido. Preciso de uma ajuda especial: o que devo fazer agora para avançar?";

    [Tooltip("Mensagem mostrada enquanto o Ollama gera a resposta.")]
    [SerializeField] private string thinkingMessage = "Hmm... deixe-me pensar...";

    [Header("Prompt dinâmico")]
    [Tooltip("ReescreverDica: o jogo escolhe a dica da etapa atual e o Ollama só a diz com a voz do NPC (mais confiável).\n" +
             "GerarComContexto: o Ollama cria a resposta com os fatos do andar e o estado do jogador.")]
    [SerializeField] private IdleHelpTextOllama.OllamaHintMode modo = IdleHelpTextOllama.OllamaHintMode.ReescreverDica;
    [Tooltip("Envia ao Ollama os fatos do andar atual, o inventário e os arredores do jogador.")]
    [SerializeField] private bool usarEstadoDoJogo = true;
    [Tooltip("Limite de palavras pedido ao modelo.")]
    [Min(8)] [SerializeField] private int maxPalavras = 25;
    [Tooltip("A resposta é cortada (em fim de frase) se passar disto.")]
    [Min(40)] [SerializeField] private int maxCaracteres = 160;
    [Tooltip("A persona é reduzida a este tamanho (personas longas confundem modelos pequenos).")]
    [Min(0)] [SerializeField] private int maxPersonaChars = 200;
    [Tooltip("Mostra no Console o prompt enviado e a resposta.")]
    [SerializeField] private bool logPrompts = false;

    public string SystemPrompt => systemPrompt;
    public string PlayerGreeting => playerGreeting;
    public int InteractionCount => interactionCount;

    private bool isThinking = false;
    private int interactionCount = 0;
    private int specialHelpCount = 0;                       // quantas ajudas especiais já deu nesta etapa
    private string specialHelpStageId;
    private readonly List<string> previousAnswers = new List<string>();

    /// <summary>
    /// Conta uma conversa com o NPC. Retorna true quando a opção "Ajuda especial" deve aparecer.
    /// </summary>
    public bool RegisterInteraction()
    {
        interactionCount++;
        return interactionCount >= interactionsForSpecialHelp;
    }

    /// <summary>Chamado ao selecionar "Ajuda especial": pede a resposta ao Ollama.</summary>
    public void RequestSpecialHelp(string title)
    {
        if (isThinking)
            return;

        if (DialogueManager.Instance != null && !string.IsNullOrWhiteSpace(thinkingMessage))
        {
            string dialogTitle = string.IsNullOrWhiteSpace(title) ? defaultTitle : title;
            DialogueManager.Instance.ShowMessage(dialogTitle, thinkingMessage);
        }

        TalkToNPC(specialHelpRequest, title);
    }

    private void Awake()
    {
        if (ollamaManager == null)
            ollamaManager = GetComponent<OllamaManager>();
    }

    public async void TalkToNPC(string playerInput, string title = null)
    {
        if (ollamaManager == null)
        {
            Debug.LogError($"[DungeonNPC] '{name}' sem OllamaManager.", this);
            return;
        }

        // Evita várias requisições simultâneas se o jogador apertar o botão repetidamente
        if (isThinking)
            return;

        isThinking = true;
        Debug.Log($"[DungeonNPC] '{name}' pensando na resposta...", this);

        PlayerController player = GameHintContext.FindPlayer();

        // Pegou a chave/cristal ou mudou de andar: as dicas recomeçam para a nova etapa
        string stageId = GameHintContext.CurrentStageId(player);
        if (stageId != specialHelpStageId)
        {
            specialHelpStageId = stageId;
            specialHelpCount = 0;
            previousAnswers.Clear();
        }

        // A ajuda especial começa já concreta (o jogador conversou várias vezes) e fica mais direta a cada pedido
        int hintNumber = 1 + specialHelpCount;
        string fixedHint = GameHintContext.SelectHint(GameHintContext.CurrentLevel(player), player, hintNumber, out string objective);

        string system, user;
        if (modo == IdleHelpTextOllama.OllamaHintMode.ReescreverDica && !string.IsNullOrWhiteSpace(fixedHint))
        {
            system = GameHintContext.RewriteSystemPrompt(systemPrompt, maxPersonaChars, maxPalavras);
            user = "Dica: " + fixedHint;
        }
        else
        {
            system = BuildSystemPrompt(player, objective, hintNumber);
            user = BuildUserMessage(player, playerInput);
        }
        if (logPrompts)
            Debug.Log($"[DungeonNPC] '{name}' SYSTEM:\n{system}\n\nUSER:\n{user}", this);

        string npcResponse;
        try
        {
            npcResponse = await ollamaManager.SendPromptAsync(system, user);
        }
        finally
        {
            isThinking = false;
        }

        npcResponse = GameHintContext.CleanResponse(npcResponse, 2, maxCaracteres);
        if (logPrompts)
            Debug.Log($"[DungeonNPC] '{name}' RESPOSTA: {npcResponse}", this);

        // Ollama fora do ar ou resposta inválida: a dica fixa da etapa garante uma ajuda útil
        if (string.IsNullOrEmpty(npcResponse))
            npcResponse = fixedHint;
        if (string.IsNullOrEmpty(npcResponse))
            return;

        specialHelpCount++;
        previousAnswers.Add(npcResponse);
        if (previousAnswers.Count > 3) previousAnswers.RemoveAt(0);

        Debug.Log($"NPC: {npcResponse}");

        if (DialogueManager.Instance != null)
        {
            string dialogTitle = string.IsNullOrWhiteSpace(title) ? defaultTitle : title;
            DialogueManager.Instance.ShowMessage(dialogTitle, npcResponse);
        }
    }

    // ------------------------------------------------------------------ prompt

    // Modo GerarComContexto
    private string BuildSystemPrompt(PlayerController player, string objective, int hintNumber)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Você é um personagem de um jogo de exploração de masmorra e responde ao jogador.");
        string persona = GameHintContext.ShortPersona(systemPrompt, maxPersonaChars);
        if (!string.IsNullOrWhiteSpace(persona))
            sb.AppendLine("QUEM VOCÊ É E O QUE SABE (se contradizer os FATOS DO JOGO, valem os fatos): " + persona);
        sb.AppendLine();

        if (usarEstadoDoJogo)
        {
            string facts = GameHintContext.LevelFacts(GameHintContext.CurrentLevel(player), objective);
            if (!string.IsNullOrEmpty(facts))
            {
                sb.Append(facts);
                sb.AppendLine();
            }
        }

        sb.Append(GameHintContext.ResponseRules(maxPalavras));
        sb.AppendLine(GameHintContext.DetailInstruction(hintNumber));
        return sb.ToString();
    }

    private string BuildUserMessage(PlayerController player, string playerInput)
    {
        var sb = new StringBuilder();
        sb.AppendLine("O jogador pergunta: " + (string.IsNullOrWhiteSpace(playerInput) ? playerGreeting : playerInput.Trim()));

        if (usarEstadoDoJogo)
        {
            sb.AppendLine();
            sb.Append(GameHintContext.PlayerState(player));
        }

        if (previousAnswers.Count > 0)
        {
            sb.AppendLine("- Você já disse (não repita, avance a dica):");
            foreach (string a in previousAnswers) sb.AppendLine("  * " + a);
        }
        return sb.ToString();
    }
}
