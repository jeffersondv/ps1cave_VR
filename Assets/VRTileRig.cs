using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Mantém a câmera VR "presa" ao centro do tile do jogador (navegação discreta tile a tile).
///
/// - Cancela o deslocamento físico da cabeça no espaço real (guardian), permitindo só uma
///   pequena inclinação (maxLean). Assim a visão nunca sai do mapa nem atravessa paredes, e
///   girar o Player não causa translação.
/// - Opcionalmente alinha a "frente" do Player (em passos de 90°) à direção em que o jogador
///   está olhando, sem alterar a imagem. Assim "analógico para frente" = para onde se olha.
/// - Recentraliza a visão ao iniciar, ao trocar de andar e quando o usuário recentraliza o Quest.
///
/// Hierarquia esperada: Player (PlayerController) > XR Origin (XR Rig) > Camera Offset > Camera
/// </summary>
[DefaultExecutionOrder(1000)]
public class VRTileRig : MonoBehaviour
{
    [Header("Referências (preenchidas automaticamente se vazias)")]
    public Transform rig;           // XR Origin (XR Rig)
    public Transform cameraOffset;  // Camera Offset
    public Transform head;          // Camera com TrackedPoseDriver

    [Header("Cabeça")]
    [Tooltip("Altura dos olhos em relação ao pivô do Player (o Player fica a tileSize/2 do chão).")]
    public float eyeHeight = 0f;
    [Tooltip("Quanto o jogador pode inclinar a cabeça para os lados/frente (m).")]
    public float maxLean = 0.15f;
    [Tooltip("Quanto o jogador pode abaixar a cabeça (m).")]
    public float maxCrouch = 0.4f;
    [Tooltip("Quanto o jogador pode levantar a cabeça (m).")]
    public float maxRise = 0.15f;

    [Header("Direção")]
    [Tooltip("A frente do Player acompanha o olhar (em passos de 90°).")]
    public bool alignBodyToHead = true;
    [Tooltip("Ângulo entre olhar e frente do Player a partir do qual a frente é trocada.")]
    [Range(46f, 80f)] public float alignThreshold = 55f;

    PlayerController player;
    Vector3 center;
    bool hasCenter;
    bool recenterYawPending;
    readonly List<XRInputSubsystem> inputSubsystems = new List<XRInputSubsystem>();

    void Awake()
    {
        player = GetComponent<PlayerController>();
        if (head == null)
        {
            Camera cam = GetComponentInChildren<Camera>(true);
            if (cam != null) head = cam.transform;
        }
        if (cameraOffset == null && head != null) cameraOffset = head.parent;
        if (rig == null && cameraOffset != null) rig = cameraOffset.parent;
    }

    void OnEnable()
    {
        Application.onBeforeRender += ApplyHeadLock;

        SubsystemManager.GetSubsystems(inputSubsystems);
        foreach (var s in inputSubsystems)
            s.trackingOriginUpdated += OnTrackingOriginUpdated;

        RecenterView();
    }

    void OnDisable()
    {
        Application.onBeforeRender -= ApplyHeadLock;
        foreach (var s in inputSubsystems)
            if (s != null) s.trackingOriginUpdated -= OnTrackingOriginUpdated;
    }

    void OnTrackingOriginUpdated(XRInputSubsystem _) => RecenterView();

    /// <summary>
    /// Faz o olhar atual coincidir com a frente do Player e recentraliza a posição da cabeça.
    /// Chamar depois de teleportar/rotacionar o Player por código (escadas, início).
    /// </summary>
    public void RecenterView()
    {
        hasCenter = false;
        recenterYawPending = true;
        if (isActiveAndEnabled) StartCoroutine(RecenterWhenTracked());
    }

    IEnumerator RecenterWhenTracked()
    {
        // Espera o headset começar a reportar pose (até 2 s)
        float timeout = Time.unscaledTime + 2f;
        while (head != null && head.localRotation == Quaternion.identity &&
               head.localPosition == Vector3.zero && Time.unscaledTime < timeout)
            yield return null;

        if (recenterYawPending) RecenterYawNow();
        hasCenter = false;
    }

    void RecenterYawNow()
    {
        recenterYawPending = false;
        if (rig == null || head == null || cameraOffset == null) return;

        // Yaw da cabeça no espaço do rig (sem a rotação atual do rig)
        Vector3 f = cameraOffset.localRotation * head.localRotation * Vector3.forward;
        f.y = 0f;
        float headYawInRig = f.sqrMagnitude > 0.0001f ? Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg : 0f;
        rig.localRotation = Quaternion.Euler(0f, -headYawInRig, 0f);
    }

    void LateUpdate()
    {
        AlignBodyToHead();
        ApplyHeadLock();
    }

    void ApplyHeadLock()
    {
        if (head == null || cameraOffset == null) return;

        Vector3 h = head.localPosition;
        if (!hasCenter)
        {
            center = h;
            hasCenter = true;
        }

        Vector3 lean = new Vector3(h.x - center.x, 0f, h.z - center.z);
        float leanMag = lean.magnitude;
        if (leanMag > maxLean)
        {
            // Arrasta o centro junto: andar fisicamente pelo quarto não move o jogador no mapa
            Vector3 excess = lean * (1f - maxLean / leanMag);
            center.x += excess.x;
            center.z += excess.z;
            lean -= excess;
        }

        float dy = Mathf.Clamp(h.y - center.y, -maxCrouch, maxRise);

        // Câmera no espaço do rig = offset + head.localPosition = (lean.x, eyeHeight + dy, lean.z)
        cameraOffset.localPosition = new Vector3(lean.x - h.x, eyeHeight + dy - h.y, lean.z - h.z);
        cameraOffset.localRotation = Quaternion.identity;
    }

    void AlignBodyToHead()
    {
        if (!alignBodyToHead || player == null || head == null || rig == null) return;
        if (player.IsBusy || !player.enabled) return;

        Vector3 f = head.forward;
        f.y = 0f;
        if (f.sqrMagnitude < 0.01f) return;

        float headYaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
        float bodyYaw = player.transform.eulerAngles.y;
        float diff = Mathf.DeltaAngle(bodyYaw, headYaw);
        if (Mathf.Abs(diff) < alignThreshold) return;

        float newYaw = Mathf.Round((bodyYaw + diff) / 90f) * 90f;
        float delta = Mathf.DeltaAngle(bodyYaw, newYaw);

        // Gira o corpo e contra-gira o rig: a imagem no headset não muda
        player.transform.rotation = Quaternion.Euler(0f, newYaw, 0f);
        rig.localRotation = Quaternion.Euler(0f, -delta, 0f) * rig.localRotation;
    }
}
