using UnityEngine;

public class DungeonNPC : MonoBehaviour
{
    [SerializeField] private OllamaManager ollamaManager;

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

    public string SystemPrompt => systemPrompt;
    public string PlayerGreeting => playerGreeting;
    public int InteractionCount => interactionCount;

    private bool isThinking = false;
    private int interactionCount = 0;

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

        string npcResponse;
        try
        {
            npcResponse = await ollamaManager.SendPromptAsync(systemPrompt, playerInput);
        }
        finally
        {
            isThinking = false;
        }

        if (string.IsNullOrEmpty(npcResponse))
            return;

        Debug.Log($"NPC: {npcResponse}");

        if (DialogueManager.Instance != null)
        {
            string dialogTitle = string.IsNullOrWhiteSpace(title) ? defaultTitle : title;
            DialogueManager.Instance.ShowMessage(dialogTitle, npcResponse.Trim());
        }
    }
}
