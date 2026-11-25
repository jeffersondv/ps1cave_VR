using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Navegação discreta tile a tile para VR (Meta Quest) e teclado.
///
/// Analógico esquerdo:  frente/trás = 1 tile; lados = 1 tile lateral (ou giro, se permitirLateral = false)
/// Analógico direito:   esquerda/direita = giro de 90°
/// Segurar o analógico repete o passo a cada "intervaloRepeticao" segundos.
/// Teclado: W/S/↑/↓ andam, A/D/←/→ giram.
///
/// A colisão e as regras (portas, escadas, NPCs) ficam no PlayerController (grid).
/// </summary>
public class MovimentoUnidadesVR : MonoBehaviour
{
    [Header("Inputs (XRI Default Input Actions)")]
    [Tooltip("Ex: XRI Left Locomotion / Move")]
    public InputActionProperty moveInput;
    [Tooltip("Ex: XRI Right Locomotion / Turn")]
    public InputActionProperty turnInput;

    [Header("Configuração")]
    [Tooltip("Ângulo de cada giro")]
    public float anguloGiro = 90.0f;
    [Tooltip("Inclinação do analógico para disparar um passo/giro")]
    [Range(0.3f, 0.9f)]
    public float deadzone = 0.6f;
    [Tooltip("Abaixo deste valor o analógico é considerado solto")]
    [Range(0.05f, 0.5f)]
    public float zonaSoltura = 0.3f;
    [Tooltip("Tempo para repetir o passo enquanto o analógico continua pressionado")]
    public float intervaloRepeticao = 0.45f;
    [Tooltip("Analógico esquerdo para os lados anda de lado (true) ou gira (false)")]
    public bool permitirLateral = true;

    PlayerController playerController;
    Vector2Int passoSegurado;
    float proximaRepeticao;
    bool giroSegurado;

    void OnEnable()
    {
        playerController = GetComponent<PlayerController>();
        DisableBuiltInXRLocomotion();
        moveInput.action?.Enable();
        turnInput.action?.Enable();
    }

    // Desliga locomoção contínua do XRI caso exista um rig de exemplo como filho
    void DisableBuiltInXRLocomotion()
    {
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child.name == "Move" || child.name == "Turn")
                child.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        if (playerController == null || !playerController.enabled || HelpMenu.IsOpen)
            return;

        Vector2 move = LerVetor(moveInput);
        if (move.sqrMagnitude < 0.0001f)
            move = OpenXRControllerInput.LeftThumbstick; // fallback via XR InputDevices
        Vector2 turn = LerVetor(turnInput);

        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y = 1f;
            else if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y = -1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) turn.x = -1f;
            else if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) turn.x = 1f;
        }

        if (!permitirLateral && Mathf.Abs(move.x) > Mathf.Abs(move.y) && Mathf.Abs(turn.x) < zonaSoltura)
        {
            turn.x = move.x;
            move = Vector2.zero;
        }

        ProcessarGiro(turn.x);
        ProcessarPasso(move);
    }

    static Vector2 LerVetor(InputActionProperty prop)
    {
        InputAction action = prop.action;
        return action != null && action.enabled ? action.ReadValue<Vector2>() : Vector2.zero;
    }

    void ProcessarPasso(Vector2 input)
    {
        float mag = input.magnitude;
        if (mag < zonaSoltura)
        {
            passoSegurado = Vector2Int.zero;
            return;
        }
        if (mag < deadzone)
            return; // histerese: mantém estado até soltar ou empurrar de vez

        Vector2Int dir = Mathf.Abs(input.y) >= Mathf.Abs(input.x)
            ? new Vector2Int(0, input.y > 0 ? 1 : -1)
            : new Vector2Int(input.x > 0 ? 1 : -1, 0);

        if (dir != passoSegurado)
        {
            passoSegurado = dir;
            proximaRepeticao = Time.time; // novo toque: passo imediato (ou assim que ficar livre)
        }

        if (Time.time >= proximaRepeticao && DarPasso(dir))
            proximaRepeticao = Time.time + intervaloRepeticao;
    }

    bool DarPasso(Vector2Int dir)
    {
        if (playerController.IsBusy)
            return false;

        Vector3 direcao = dir.y != 0
            ? ObterEixoCardeal(transform.forward) * dir.y
            : ObterEixoCardeal(transform.right) * dir.x;

        playerController.TryMoveVR(direcao);
        return true;
    }

    void ProcessarGiro(float x)
    {
        if (Mathf.Abs(x) < zonaSoltura)
        {
            giroSegurado = false;
            return;
        }
        if (giroSegurado || Mathf.Abs(x) < deadzone || playerController.IsBusy)
            return;

        giroSegurado = true;
        playerController.RotateVR(Mathf.Sign(x) * anguloGiro);
    }

    // Converte qualquer direção no eixo puro mais próximo (N/S/L/O)
    static Vector3 ObterEixoCardeal(Vector3 vetor)
    {
        vetor.y = 0f;
        if (Mathf.Abs(vetor.x) > Mathf.Abs(vetor.z))
            return new Vector3(Mathf.Sign(vetor.x), 0f, 0f);
        return new Vector3(0f, 0f, Mathf.Sign(vetor.z));
    }
}
