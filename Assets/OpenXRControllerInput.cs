using UnityEngine;
using UnityEngine.XR;

public static class OpenXRControllerInput
{
    public const float StickDeadzone = 0.5f;

    private static InputDevice leftController;
    private static InputDevice rightController;
    private static bool previousInteractPressed;
    private static bool interactPressedThisFrame;
    private static int lastInteractReadFrame = -1;

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

    public static bool InteractPressedThisFrame
    {
        get
        {
            if (lastInteractReadFrame == Time.frameCount)
                return interactPressedThisFrame;

            lastInteractReadFrame = Time.frameCount;
            bool isPressed = IsInteractButtonPressed(GetController(XRNode.LeftHand, ref leftController)) ||
                             IsInteractButtonPressed(GetController(XRNode.RightHand, ref rightController));

            interactPressedThisFrame = isPressed && !previousInteractPressed;
            previousInteractPressed = isPressed;
            return interactPressedThisFrame;
        }
    }

    private static InputDevice GetController(XRNode hand, ref InputDevice controller)
    {
        if (!controller.isValid)
            controller = InputDevices.GetDeviceAtXRNode(hand);

        return controller;
    }

    private static bool IsInteractButtonPressed(InputDevice controller)
    {
        return controller.isValid &&
               ((controller.TryGetFeatureValue(CommonUsages.primaryButton, out bool primaryButton) &&
                 primaryButton) ||
                (controller.TryGetFeatureValue(CommonUsages.triggerButton, out bool triggerButton) &&
                 triggerButton));
    }
}
