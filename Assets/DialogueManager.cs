using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

// Estrutura para armazenar cada mensagem da fila
[System.Serializable]
public struct DialogueMessage
{
    public string title;
    public string content;

    public DialogueMessage(string title, string content)
    {
        this.title = title;
        this.content = content;
    }
}

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    [Header("Componentes de UI")]
    public GameObject dialoguePanel;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI contentText;

    // Fila para guardar mensagens consecutivas
    private Queue<DialogueMessage> messageQueue = new Queue<DialogueMessage>();
    private bool isShowingMessage = false;
    private DialogueMessage currentMessage;
    private bool isShowingIdleHelp = false;
    private int lastMessageShownFrame = -1;

    [Header("Configurações")]
    public float displayTime = 15.0f; // Tempo (em segundos) que a mensagem fica na tela

    private Coroutine hideCoroutine;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (dialoguePanel != null) 
            dialoguePanel.SetActive(false);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;

        // Se houver uma mensagem visível, permite ao jogador avançar para a próxima
        // (A/X sempre; gatilho só se o laser não estiver sobre UI; nada com o menu de ajuda aberto)
        // Não avança uma mensagem que acabou de aparecer neste mesmo frame (a mesma pressão
        // que falou com o NPC/porta fecharia a mensagem na hora).
        bool xrPressed = OpenXRControllerInput.GameplayInteractPressedThisFrame;
        bool mouseClick = mouse != null && mouse.leftButton.wasPressedThisFrame && !GameInputGate.PointerBusy;
        if (isShowingMessage && Time.frameCount != lastMessageShownFrame &&
            !SpecialHelpOption.IsGazed && !HelpMenu.IsOpen &&
            ((keyboard != null && (keyboard.eKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)) ||
             mouseClick || xrPressed))
        {
            DisplayNextMessage();
        }
    }

    /// <summary>
    /// Adiciona uma nova mensagem à fila de exibição.
    /// </summary>
    public void ShowMessage(string title, string content)
    {
        if (isShowingMessage && currentMessage.title == title && currentMessage.content == content)
            return;

        foreach (DialogueMessage queuedMessage in messageQueue)
        {
            if (queuedMessage.title == title && queuedMessage.content == content)
                return;
        }

        messageQueue.Enqueue(new DialogueMessage(title, content));


        // Cancela a contagem anterior se uma nova interação for feita
        if (hideCoroutine != null)
            StopCoroutine(hideCoroutine);

        // Inicia a contagem para esconder a mensagem
        hideCoroutine = StartCoroutine(HideAfterDelay());
            
        // Se o painel não estiver ativo, inicia a exibição da primeira mensagem
        if (!isShowingMessage)
        {
            DisplayNextMessage();
        }
    }

    IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(displayTime);
        //dialoguePanel.SetActive(false);
        DisplayNextMessage();
    }

    /// <summary>
    /// Exibe a próxima mensagem da fila ou fecha o painel se a fila estiver vazia.
    /// </summary>
    public void DisplayNextMessage()
    {
        if (messageQueue.Count > 0)
        {
            DialogueMessage msg = messageQueue.Dequeue();
            currentMessage = msg;

            if (titleText != null) titleText.text = msg.title;
            if (contentText != null) contentText.text = msg.content;

            if (dialoguePanel != null) dialoguePanel.SetActive(true);
            isShowingMessage = true;
            lastMessageShownFrame = Time.frameCount;
        }
        else
        {
            // Sem mais mensagens na fila: fecha o painel
            isShowingMessage = false;
            UpdatePanelVisibility();
        }
    }

    public void SetIdleHelpVisible(bool visible)
    {
        isShowingIdleHelp = visible;        
        UpdatePanelVisibility();
    }

    private void UpdatePanelVisibility()
    {
        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(isShowingMessage || !isShowingMessage && isShowingIdleHelp);
            //dialoguePanel.SetActive(isShowingMessage);
        }
    }

    /// <summary>
    /// Utilitário para verificar se o diálogo está ativo (útil para bloquear o movimento do jogador)
    /// </summary>
    public bool IsDialogueActive()
    {
        return isShowingMessage;
    }

    public void ClearMessages()
    {
        messageQueue.Clear();
        isShowingMessage = false;
        if (titleText != null) titleText.text = "";
        if (contentText != null) contentText.text = "";
        UpdatePanelVisibility();
    }
}