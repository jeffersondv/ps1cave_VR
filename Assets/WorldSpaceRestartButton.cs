using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// Leva o botão "Reiniciar" (ButtonRestart, que fica no Canvas do HUD preso à cabeça) para um
/// pequeno Canvas em World Space na frente do jogador, onde o laser dos controles alcança.
/// Clique: laser + gatilho (dedo indicador), botão A/X, mouse ou tecla R/Enter no Editor.
/// </summary>
public class WorldSpaceRestartButton : MonoBehaviour
{
    public float distance = 0.8f;      // antes das paredes (1 m do centro do bloco)
    public float heightOffset = -0.22f; // abaixo do texto de fim de jogo, que fica no centro da visão
    public Vector2 sizePixels = new Vector2(420f, 120f);
    public float metersPerPixel = 0.001f;
    [Tooltip("Ignora A/X logo após aparecer, para não reiniciar sem querer (s, tempo real).")]
    public float inputDelay = 1f;

    Button button;
    float shownAt;

    /// <summary>Cria o painel em World Space e move o botão para ele.</summary>
    public static WorldSpaceRestartButton Show(GameObject restartButton)
    {
        if (restartButton == null) return null;

        var go = new GameObject("Restart Panel (World Space)", typeof(RectTransform));
        var panel = go.AddComponent<WorldSpaceRestartButton>();
        panel.Build(restartButton);
        return panel;
    }

    void Build(GameObject restartButton)
    {
        Camera cam = Camera.main;

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = cam;
        canvas.sortingOrder = 20;
        gameObject.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;
        gameObject.AddComponent<GraphicRaycaster>();              // mouse no Editor
        gameObject.AddComponent<TrackedDeviceGraphicRaycaster>(); // laser dos controles XR

        var rt = (RectTransform)transform;
        rt.sizeDelta = sizePixels;
        rt.localScale = Vector3.one * metersPerPixel;
        PlaceInFrontOfHead(cam);

        // Move o botão do HUD para cá e o estica para ocupar o painel
        var buttonRect = restartButton.transform as RectTransform;
        if (buttonRect != null)
        {
            buttonRect.SetParent(rt, false);
            buttonRect.anchorMin = Vector2.zero;
            buttonRect.anchorMax = Vector2.one;
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.offsetMin = Vector2.zero;
            buttonRect.offsetMax = Vector2.zero;
            buttonRect.localScale = Vector3.one;
            buttonRect.localRotation = Quaternion.identity;
        }

        var label = restartButton.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null)
        {
            label.enableAutoSizing = false;
            label.fontSize = 44f;
            label.alignment = TextAlignmentOptions.Center;
            if (!label.text.Contains("A/X")) label.text = label.text + "  <size=60%>(gatilho ou A/X)</size>";
        }

        restartButton.SetActive(true);
        button = restartButton.GetComponent<Button>();
        shownAt = Time.unscaledTime;
    }

    void PlaceInFrontOfHead(Camera cam)
    {
        if (cam == null) return;
        Transform head = cam.transform;
        Vector3 forward = head.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f) forward = head.up;
        forward.y = 0f;
        forward.Normalize();

        transform.position = head.position + forward * distance + Vector3.up * heightOffset;
        transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
    }

    void Update()
    {
        if (button == null || !button.isActiveAndEnabled || !button.interactable) return;
        if (Time.unscaledTime - shownAt < inputDelay) return;

        Keyboard kb = Keyboard.current;
        bool pressed = OpenXRControllerInput.PrimaryPressedThisFrame ||
                       (kb != null && (kb.rKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame));
        if (pressed)
            button.onClick.Invoke();
    }
}
