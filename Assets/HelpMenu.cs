using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR;
using CommonUsages = UnityEngine.XR.CommonUsages;
using InputDevice = UnityEngine.XR.InputDevice;

/// <summary>
/// Menu de ajuda com os controles do Meta Quest 2/3, num Canvas em World Space.
/// - Aparece ao iniciar o jogo, na frente do jogador, e pausa o jogo (Time.timeScale = 0).
/// - Fecha pelo botão "Fechar" (laser do controle + gatilho), pelo botão ☰ do controle esquerdo,
///   por B/Y, ou pela tecla H/Esc no Editor.
/// - Reabre pelo botão ☰ (Menu) do controle esquerdo ou pela tecla H.
/// </summary>
public class HelpMenu : MonoBehaviour
{
    public static HelpMenu Instance { get; private set; }
    public static bool IsOpen => Instance != null && Instance.open;

    [Header("Referências")]
    [Tooltip("Objeto com o painel (filho do Canvas). É ativado/desativado ao abrir/fechar.")]
    public GameObject content;
    public Button closeButton;

    [Header("Comportamento")]
    public bool showOnStart = true;
    [Tooltip("Pausa o jogo (cronômetro, movimento, dicas) enquanto o menu está aberto.")]
    public bool pauseGame = true;
    [Tooltip("Espera o rastreamento da cabeça estabilizar antes de posicionar o menu (s).")]
    public float startDelay = 0.75f;

    [Header("Posição (relativa à cabeça)")]
    [Tooltip("Distância do menu. As paredes ficam a 1 m do centro do bloco; o menu é desenhado por cima delas.")]
    public float distance = 0.85f;
    public float heightOffset = -0.08f;

    [Header("Entrada")]
    [Tooltip("Abre/fecha o menu. Padrão: botão ☰ (Menu) do controle esquerdo.")]
    public InputActionProperty toggleAction;

    bool open;
    float previousTimeScale = 1f;
    bool legacyMenuWasPressed;
    bool legacyCloseWasPressed;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Ação padrão (embutida), caso nenhuma tenha sido configurada no Inspector
        if (toggleAction.action == null || toggleAction.action.bindings.Count == 0)
        {
            var action = new InputAction("Toggle Help Menu", InputActionType.Button);
            action.AddBinding("<XRController>{LeftHand}/menu");
            action.AddBinding("<XRController>{LeftHand}/menuButton");
            toggleAction = new InputActionProperty(action);
        }

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        if (content != null) content.SetActive(false);
    }

    void OnEnable() => toggleAction.action?.Enable();
    void OnDisable()
    {
        toggleAction.action?.Disable();
        if (open) Close();
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            if (open && pauseGame) Time.timeScale = previousTimeScale;
            Instance = null;
        }
    }

    IEnumerator Start()
    {
        if (!showOnStart) yield break;
        yield return new WaitForSecondsRealtime(startDelay); // tempo real: não depende do timeScale
        Open();
    }

    void Update()
    {
        InputAction action = toggleAction.action;
        bool toggle = action != null && action.WasPressedThisFrame();

        // Reserva via XR InputDevices, só se a ação não encontrou o botão Menu no controle
        // (usar as duas fontes juntas poderia abrir e fechar o menu em frames seguidos)
        if (action == null || action.controls.Count == 0)
        {
            bool legacyMenu = LegacyButton(XRNode.LeftHand, CommonUsages.menuButton);
            if (legacyMenu && !legacyMenuWasPressed) toggle = true;
            legacyMenuWasPressed = legacyMenu;
        }

        Keyboard kb = Keyboard.current;
        if (kb != null && kb.hKey.wasPressedThisFrame) toggle = true;

        if (toggle)
        {
            if (open) Close(); else Open();
            return;
        }

        if (!open) return;

        // B/Y (botão secundário) ou Esc também fecham
        bool legacyClose = LegacyButton(XRNode.LeftHand, CommonUsages.secondaryButton) ||
                           LegacyButton(XRNode.RightHand, CommonUsages.secondaryButton);
        bool closePressed = legacyClose && !legacyCloseWasPressed;
        legacyCloseWasPressed = legacyClose;

        if (kb != null && kb.escapeKey.wasPressedThisFrame) closePressed = true;

        if (closePressed) Close();
    }

    static bool LegacyButton(XRNode node, InputFeatureUsage<bool> usage)
    {
        InputDevice device = InputDevices.GetDeviceAtXRNode(node);
        return device.isValid && device.TryGetFeatureValue(usage, out bool pressed) && pressed;
    }

    public void Open()
    {
        PlaceInFrontOfHead();
        if (content != null) content.SetActive(true);
        if (!open && pauseGame)
        {
            previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
        }
        open = true;
    }

    public void Close()
    {
        if (content != null) content.SetActive(false);
        if (open && pauseGame) Time.timeScale = previousTimeScale;
        open = false;
    }

    public void Toggle()
    {
        if (open) Close(); else Open();
    }

    /// <summary>Coloca o menu na frente da cabeça (só o giro horizontal), virado para o jogador.</summary>
    void PlaceInFrontOfHead()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        Transform head = cam.transform;

        Vector3 forward = head.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f) forward = head.up; // olhando reto para cima/baixo
        forward.y = 0f;
        forward.Normalize();

        transform.position = head.position + forward * distance + Vector3.up * heightOffset;
        transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
    }
}
