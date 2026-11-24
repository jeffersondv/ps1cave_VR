using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Canvas))]
public class CountdownTimer : MonoBehaviour
{
    private const float CountdownDurationSeconds = 10f * 60f;

    private static HashSet<int> interactedNPCs = new HashSet<int>();

    public static int DistinctInteractedNPCCount => interactedNPCs.Count;

    private static float remainingSeconds = CountdownDurationSeconds;
    private static bool hasFinished;

    private TMP_Text timerText;
    private int lastDisplayedSeconds = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        interactedNPCs.Clear();
        remainingSeconds = CountdownDurationSeconds;
        hasFinished = false;
    }

    public static void RegisterNPCInteraction(Component npc)
    {
        if (npc != null)
            interactedNPCs.Add(npc.GetInstanceID());
    }

    [Header("Prefab ao terminar a contagem regressiva")]
    public AudioSource prefabGameOver;

    [SerializeField] private SeamlessAudioSequencer seamlessAudioSequencer;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private GameObject restartButton;

    private void Awake()
    {
        TMP_Text fontSource = GetComponentInChildren<TMP_Text>(true);
        if (fontSource == null || fontSource.font == null)
        {
            throw new InvalidOperationException(
                "CountdownTimer requires a TextMeshPro font asset on the Canvas.");
        }

        GameObject textObject = new GameObject(
            "CountdownTimerText",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.SetParent(transform, false);
        textRect.anchorMin = new Vector2(1f, 1f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.pivot = new Vector2(1f, 1f);
        textRect.anchoredPosition = new Vector2(-24f, -20f);
        textRect.sizeDelta = new Vector2(180f, 52f);

        timerText = textObject.GetComponent<TextMeshProUGUI>();
        timerText.font = fontSource.font;
        timerText.fontSize = 32f;
        timerText.color = Color.white;
        timerText.alignment = TextAlignmentOptions.TopRight;
        timerText.raycastTarget = false;
        timerText.outlineColor = Color.black;
        timerText.outlineWidth = 0.2f;

        UpdateDisplay();
    }

    private void Update()
    {
        if (hasFinished)
        {
            return;
        }

        remainingSeconds = Mathf.Max(0f, remainingSeconds - Time.deltaTime);
        UpdateDisplay();

        if (remainingSeconds <= 0f)
        {
            
            FinishCountdown();

            ShowGameOver(timerText.font);
        }
    }

    private void ShowGameOver(TMP_FontAsset font)
    {
        ShowEndScreen(font, "Game Over", new Color(0.3f, 0f, 0f, 0.72f));
    }

    private void ShowEndScreen(TMP_FontAsset font, string text, Color overlayColor)
    {
        GameObject overlayObject = new GameObject(
            "EndGameOverlay",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(RawImage));
        RectTransform overlayRect = overlayObject.GetComponent<RectTransform>();
        overlayRect.SetParent(transform, false);
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        RawImage colorFilter = overlayObject.GetComponent<RawImage>();
        colorFilter.texture = Texture2D.whiteTexture;
        colorFilter.color = overlayColor;

        GameObject messageObject = new GameObject(
            "EndGameText",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        RectTransform messageRect = messageObject.GetComponent<RectTransform>();
        messageRect.SetParent(overlayRect, false);
        messageRect.anchorMin = Vector2.zero;
        messageRect.anchorMax = Vector2.one;
        messageRect.offsetMin = Vector2.zero;
        messageRect.offsetMax = Vector2.zero;

        TextMeshProUGUI message = messageObject.GetComponent<TextMeshProUGUI>();
        message.font = font;
        message.text = text;
        message.fontSize = 48f;
        message.fontStyle = FontStyles.Bold;
        message.color = Color.white;
        message.alignment = TextAlignmentOptions.Center;
        message.raycastTarget = false;
        message.outlineColor = Color.black;
        message.outlineWidth = 0.2f;

        if (restartButton != null)
        {
            RectTransform buttonRect = restartButton.transform as RectTransform;
            if (buttonRect != null)
            {
                buttonRect.SetParent(overlayRect, false);
                buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
                buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
                buttonRect.pivot = new Vector2(0.5f, 0.5f);
                buttonRect.anchoredPosition = new Vector2(0f, -180f);
                buttonRect.sizeDelta = new Vector2(180f, 40f);
                buttonRect.SetAsLastSibling();
            }

            restartButton.SetActive(true);
        }
    }

    private void UpdateDisplay()
    {
        int displayedSeconds = Mathf.CeilToInt(remainingSeconds);
        if (displayedSeconds == lastDisplayedSeconds)
        {
            return;
        }

        lastDisplayedSeconds = displayedSeconds;
        timerText.text = $"{displayedSeconds / 60:00}:{displayedSeconds % 60:00}";
    }

    public void ShowGameOver()
    {
        if (timerText != null && timerText.font != null)
        {
            ShowGameOver(timerText.font);
        }
        else
        {
            Debug.LogError("CountdownTimer: timerText ou font não foi atribuído.", this);
        }
    }

    public void FinishCountdown()
    {
        hasFinished = true;

            stopSeamlessSound();

            if (prefabGameOver != null)
            {
                prefabGameOver.PlayScheduled(AudioSettings.dspTime + 0.1);
            }
            else
            {
                Debug.LogError("CountdownTimer: prefabGameOver não foi atribuído.", this);
            }

            if (playerController != null)
            {
                playerController.DisableIdleHelpText();
                playerController.FreezeControls();
            }
            else
            {
                Debug.LogError("CountdownTimer: playerController não foi atribuído.", this);
            }
    }

    public void stopSeamlessSound()
    {
        if (seamlessAudioSequencer != null)
        {
            seamlessAudioSequencer.StopSequence();
        }
        else
        {
            Debug.LogError(
                "CountdownTimer: atribua o SeamlessAudioSequencer no Inspector.",
                this);
        }
    }

    public void EndGame()
    {
        if (hasFinished)
        {
            return;
        }

        int elapsedSeconds = Mathf.FloorToInt(CountdownDurationSeconds - remainingSeconds);
        string elapsedTime = $"{elapsedSeconds / 60:00}:{elapsedSeconds % 60:00}";
        string completionMessage =
            $"Fim de Jogo, Parabéns!!\n\nTempo gasto: {elapsedTime}\nNPCs com quem interagiu: {DistinctInteractedNPCCount}";

        remainingSeconds = 0f;
        UpdateDisplay();
        FinishCountdown();
        ShowEndScreen(timerText.font, completionMessage, new Color(0f, 0.25f, 0.08f, 0.82f));
    }

    public static void ResetForNewGame()
    {
        remainingSeconds = CountdownDurationSeconds;
        hasFinished = false;
        interactedNPCs.Clear();
    }

}
