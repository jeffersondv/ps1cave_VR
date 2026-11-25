using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using CommonUsages = UnityEngine.XR.CommonUsages;
using InputDevice = UnityEngine.XR.InputDevice;

/// <summary>
/// Leitura dos botões dos controles Meta Quest 2 (Oculus Touch) e Quest 3 (Touch Plus).
///
/// Cada botão é lido por DUAS fontes, combinadas pelo nível (pressionado/solto) antes de detectar
/// o "apertou neste frame" — assim uma única pressão nunca conta duas vezes:
///  1. Input System (OpenXR): &lt;XRController&gt;{LeftHand|RightHand}/primaryButton e /triggerPressed
///  2. API antiga XR InputDevices (CommonUsages.primaryButton / triggerButton)
///
/// - A/X (primaryButton): sempre age no JOGO (NPC, porta, diálogo). Não é usado pela interface.
/// - Gatilho (indicador): age no jogo só quando o laser NÃO está sobre UI/objeto interativo.
/// </summary>
public static class OpenXRControllerInput
{
    public const float StickDeadzone = 0.5f;

    private static InputDevice leftController;
    private static InputDevice rightController;

    private static InputAction primaryAction;
    private static InputAction triggerAction;

    private static int lastReadFrame = -1;
    private static bool previousPrimary, previousTrigger;
    private static bool primaryPressedThisFrame, triggerPressedThisFrame;

    public static Vector2 LeftThumbstick
    {
        get
        {
            InputDevice controller = GetController(XRNode.LeftHand, ref leftController);
            return controller.isValid &&
                   controller.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 axis)
                ? axis
                : Vector2.zero;
        }
    }

    /// <summary>A ou X apertado neste frame (qualquer controle).</summary>
    public static bool PrimaryPressedThisFrame { get { Refresh(); return primaryPressedThisFrame; } }

    /// <summary>Gatilho (dedo indicador) apertado neste frame (qualquer controle).</summary>
    public static bool TriggerPressedThisFrame { get { Refresh(); return triggerPressedThisFrame; } }

    /// <summary>A/X ou gatilho apertado neste frame, sem considerar a interface.</summary>
    public static bool InteractPressedThisFrame
    {
        get { Refresh(); return primaryPressedThisFrame || triggerPressedThisFrame; }
    }

    /// <summary>
    /// Botão de interação para o JOGO (NPC, porta, avançar diálogo):
    /// A/X sempre; gatilho só se o laser não estiver apontando para UI/objeto interativo.
    /// Falso com o menu de ajuda aberto.
    /// </summary>
    public static bool GameplayInteractPressedThisFrame
    {
        get
        {
            Refresh();
            if (HelpMenu.IsOpen) return false;
            if (primaryPressedThisFrame) return true;
            return triggerPressedThisFrame && !GameInputGate.PointerBusy;
        }
    }

    private static void Refresh()
    {
        if (lastReadFrame == Time.frameCount) return;
        lastReadFrame = Time.frameCount;

        EnsureActions();

        InputDevice left = GetController(XRNode.LeftHand, ref leftController);
        InputDevice right = GetController(XRNode.RightHand, ref rightController);

        bool primary = primaryAction.IsPressed() ||
                       LegacyButton(left, CommonUsages.primaryButton) ||
                       LegacyButton(right, CommonUsages.primaryButton);

        bool trigger = triggerAction.IsPressed() ||
                       LegacyButton(left, CommonUsages.triggerButton) ||
                       LegacyButton(right, CommonUsages.triggerButton);

        primaryPressedThisFrame = primary && !previousPrimary;
        triggerPressedThisFrame = trigger && !previousTrigger;
        previousPrimary = primary;
        previousTrigger = trigger;
    }

    private static void EnsureActions()
    {
        if (primaryAction != null) return;

        primaryAction = new InputAction("Interact A/X", InputActionType.Button);
        primaryAction.AddBinding("<XRController>{LeftHand}/primaryButton");
        primaryAction.AddBinding("<XRController>{RightHand}/primaryButton");
        primaryAction.Enable();

        triggerAction = new InputAction("Interact Trigger", InputActionType.Button);
        triggerAction.AddBinding("<XRController>{LeftHand}/triggerPressed");
        triggerAction.AddBinding("<XRController>{RightHand}/triggerPressed");
        triggerAction.Enable();
    }

    private static bool LegacyButton(InputDevice controller, InputFeatureUsage<bool> usage)
    {
        return controller.isValid && controller.TryGetFeatureValue(usage, out bool pressed) && pressed;
    }

    private static InputDevice GetController(XRNode hand, ref InputDevice controller)
    {
        if (!controller.isValid)
            controller = InputDevices.GetDeviceAtXRNode(hand);

        return controller;
    }
}
