using System.Collections.Generic;
using System.Text;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

/// <summary>
/// Corrige a configuração do projeto e da cena para rodar no Meta Quest 2/3 (APK Android + OpenXR).
/// Menu: Tools > Quest > Corrigir configuração para Meta Quest
/// </summary>
public static class QuestSetupTool
{
    const string RigName = "XR Origin (XR Rig)";

    [MenuItem("Tools/Quest/Corrigir configuração para Meta Quest")]
    public static void FixAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[QuestSetup] Saia do Play Mode antes de executar a correção.");
            return;
        }

        var log = new StringBuilder();
        FixAndroidNetwork(log);
        FixOpenXRAndroidFeatures(log);
        FixScene(log);
        AssetDatabase.SaveAssets();
        Debug.Log("[QuestSetup]\n" + log);
    }

    [MenuItem("Tools/Quest/Debug - Mostrar Ajuda especial (Play Mode)")]
    public static void DebugShowSpecialHelp()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("[QuestSetup] Entre em Play Mode para testar a opção.");
            return;
        }

        PlayerController player = Object.FindFirstObjectByType<PlayerController>();
        DungeonNPC nearest = null;
        float best = float.MaxValue;
        foreach (DungeonNPC npc in Object.FindObjectsByType<DungeonNPC>(FindObjectsSortMode.None))
        {
            float d = Vector3.Distance(npc.transform.position, player.transform.position);
            if (d < best) { best = d; nearest = npc; }
        }

        if (player == null || nearest == null)
        {
            Debug.LogWarning("[QuestSetup] Player ou DungeonNPC não encontrados.");
            return;
        }

        SpecialHelpOption.Show(nearest, nearest.name, player.transform);
        Debug.Log($"[QuestSetup] Ajuda especial mostrada para '{nearest.name}' (distância {best:0.0}).");
    }

    [MenuItem("Tools/Quest/Verificar validacao OpenXR (Android)")]
    public static void ReportValidation()
    {
        var issues = new List<OpenXRFeature.ValidationRule>();
        UnityEditor.XR.OpenXR.OpenXRProjectValidation.GetCurrentValidationIssues(issues, BuildTargetGroup.Android);
        var sb = new StringBuilder($"[QuestValidation] {issues.Count} problema(s):\n");
        foreach (var rule in issues)
            sb.AppendLine($"{(rule.error ? "ERRO" : "aviso")}: {rule.message} (auto-fix: {rule.fixItAutomatic})");
        Debug.Log(sb.ToString());
    }

    [MenuItem("Tools/Quest/Aplicar Fix automatico da validacao OpenXR (Android)")]
    public static void AutoFixValidation()
    {
        var issues = new List<OpenXRFeature.ValidationRule>();
        UnityEditor.XR.OpenXR.OpenXRProjectValidation.GetCurrentValidationIssues(issues, BuildTargetGroup.Android);
        foreach (var rule in issues)
            if (rule.fixItAutomatic && rule.fixIt != null) rule.fixIt();
        AssetDatabase.SaveAssets();
        ReportValidation();
    }

    // ---------------------------------------------------------------------
    // 0) Rede: o Ollama usa http:// na rede local. O Android bloqueia HTTP sem TLS
    //    se "Allow downloads over HTTP" estiver em "Not allowed".
    // ---------------------------------------------------------------------
    static void FixAndroidNetwork(StringBuilder log)
    {
        if (PlayerSettings.insecureHttpOption != InsecureHttpOption.AlwaysAllowed)
        {
            PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
            log.AppendLine("Player Settings: 'Allow downloads over HTTP' -> Always allowed (Ollama via http://).");
        }
        if (!PlayerSettings.Android.forceInternetPermission)
        {
            PlayerSettings.Android.forceInternetPermission = true;
            log.AppendLine("Player Settings (Android): Internet Access -> Require.");
        }
    }

    // ---------------------------------------------------------------------
    // 1) OpenXR (Android): liga Meta Quest Support + Oculus Touch,
    //    desliga Android XR (Samsung/Google), que é incompatível com o Quest.
    // ---------------------------------------------------------------------
    static void FixOpenXRAndroidFeatures(StringBuilder log)
    {
        OpenXRSettings settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
        if (settings == null)
        {
            log.AppendLine("ERRO: OpenXR Settings para Android não encontrado (XR Plug-in Management > Android > OpenXR).");
            return;
        }

        foreach (OpenXRFeature feature in settings.GetFeatures())
        {
            System.Type type = feature.GetType();
            string ns = type.Namespace ?? "";
            bool want = feature.enabled;

            if (type.Name == "MetaQuestFeature" || type.Name == "OculusTouchControllerProfile")
                want = true;                                   // Suporte ao Quest + controles Touch
            else if (ns == "UnityEngine.XR.OpenXR.Features.Android" ||
                     type.Name.StartsWith("AndroidXR"))
                want = false;                                  // Android XR (não roda no Quest)

            if (want != feature.enabled)
            {
                feature.enabled = want;
                EditorUtility.SetDirty(feature);
                log.AppendLine($"OpenXR Android: {type.Name} ({ns}) -> {(want ? "ON" : "OFF")}");
            }
        }
        EditorUtility.SetDirty(settings);
    }

    // ---------------------------------------------------------------------
    // 2) Cena
    // ---------------------------------------------------------------------
    static void FixScene(StringBuilder log)
    {
        var scene = EditorSceneManager.GetActiveScene();

        // 2a) XR Device Simulator só no Editor (no APK ele substitui/remove o HMD e os controles reais)
        foreach (var go in FindAll("XR Device Simulator"))
        {
            if (go.tag != "EditorOnly")
            {
                Undo.RecordObject(go, "EditorOnly simulator");
                go.tag = "EditorOnly";
                log.AppendLine("XR Device Simulator marcado como EditorOnly (fica fora do APK).");
            }
        }

        // 2b) Rig XR: Player > XR Origin (XR Rig) > Camera Offset > Camera (+ TrackedPoseDriver)
        PlayerController player = Object.FindFirstObjectByType<PlayerController>();
        Camera cam = player != null ? player.GetComponentInChildren<Camera>(true) : Camera.main;
        if (player == null || cam == null)
        {
            log.AppendLine("ERRO: Player ou Camera não encontrados.");
        }
        else
        {
            SetupRig(player.transform, cam, log);
            FixCanvas(cam, log);

            // Dicas por andar (inatividade): grava os padrões na cena para editar no Inspector
            IdleHelpTextOllama idle = player.GetComponent<IdleHelpTextOllama>();
            if (idle != null && (idle.dicasPorAndar == null || idle.dicasPorAndar.Count == 0 ||
                                 string.IsNullOrWhiteSpace(idle.dicasPorAndar[0].contexto)))
            {
                Undo.RecordObject(idle, "Dicas por andar");
                idle.dicasPorAndar = IdleHelpTextOllama.CreateDefaultLevelHints();
                EditorUtility.SetDirty(idle);
                log.AppendLine($"IdleHelpTextOllama: {idle.dicasPorAndar.Count} configurações de dicas por andar gravadas.");
            }

            // Navegação discreta: prende a cabeça ao centro do tile
            if (player.GetComponent<VRTileRig>() == null)
            {
                VRTileRig tileRig = Undo.AddComponent<VRTileRig>(player.gameObject);
                tileRig.head = cam.transform;
                tileRig.cameraOffset = cam.transform.parent;
                tileRig.rig = cam.transform.parent != null ? cam.transform.parent.parent : null;
                log.AppendLine("VRTileRig adicionado ao Player (câmera presa ao tile).");
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.AppendLine("Cena salva.");
    }

    static void SetupRig(Transform player, Camera cam, StringBuilder log)
    {
        XROrigin origin = cam.GetComponentInParent<XROrigin>();
        if (origin == null)
        {
            Vector3 camLocal = cam.transform.localPosition;

            var rigGO = new GameObject(RigName);
            Undo.RegisterCreatedObjectUndo(rigGO, "XR Rig");
            rigGO.transform.SetParent(player, false);
            rigGO.transform.localPosition = new Vector3(camLocal.x, 0f, camLocal.z);
            rigGO.transform.localRotation = Quaternion.identity;

            var offsetGO = new GameObject("Camera Offset");
            Undo.RegisterCreatedObjectUndo(offsetGO, "Camera Offset");
            offsetGO.transform.SetParent(rigGO.transform, false);
            offsetGO.transform.localPosition = new Vector3(0f, camLocal.y, 0f);

            Undo.SetTransformParent(cam.transform, offsetGO.transform, "Camera to rig");
            cam.transform.localPosition = Vector3.zero;
            cam.transform.localRotation = Quaternion.identity;

            origin = Undo.AddComponent<XROrigin>(rigGO);
            origin.Origin = rigGO;
            origin.CameraFloorOffsetObject = offsetGO;
            origin.Camera = cam;
            // Device: a altura da câmera é a do jogo (CameraYOffset), não a altura real do jogador
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device;
            origin.CameraYOffset = camLocal.y;

            log.AppendLine($"Criado '{RigName}' com XROrigin (Device, Y offset {camLocal.y:0.00}).");
        }

        cam.nearClipPlane = Mathf.Min(cam.nearClipPlane, 0.05f);
        if (!cam.CompareTag("MainCamera")) cam.tag = "MainCamera";

        // Rastreamento da cabeça (sem isto a imagem fica "presa" ao rosto)
        if (cam.GetComponent<TrackedPoseDriver>() == null)
        {
            var tpd = Undo.AddComponent<TrackedPoseDriver>(cam.gameObject);
            tpd.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            tpd.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            tpd.positionInput = new InputActionProperty(MakeAction("Position", "Vector3",
                "<XRHMD>/centerEyePosition", "<HandheldARInputDevice>/devicePosition"));
            tpd.rotationInput = new InputActionProperty(MakeAction("Rotation", "Quaternion",
                "<XRHMD>/centerEyeRotation", "<HandheldARInputDevice>/deviceRotation"));
            tpd.trackingStateInput = new InputActionProperty(MakeAction("Tracking State", "Integer",
                "<XRHMD>/trackingState"));
            log.AppendLine("TrackedPoseDriver adicionado à câmera (rastreamento da cabeça).");
        }
    }

    static InputAction MakeAction(string name, string controlType, params string[] bindings)
    {
        var action = new InputAction(name, InputActionType.Value, expectedControlType: controlType);
        foreach (string b in bindings) action.AddBinding(b);
        return action;
    }

    // Canvas em "Screen Space - Overlay" não é desenhado corretamente no headset.
    static void FixCanvas(Camera cam, StringBuilder log)
    {
        foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!canvas.isRootCanvas) continue;
            if (canvas.gameObject.tag == "EditorOnly") continue;
            if (canvas.transform.root.name.Contains("XR Device Simulator")) continue;

            // UI sempre por cima das paredes (o plano do Canvas atravessa a geometria ao girar a cabeça)
            if (canvas.renderMode != RenderMode.WorldSpace && canvas.GetComponent<UIAlwaysOnTop>() == null)
            {
                Undo.AddComponent<UIAlwaysOnTop>(canvas.gameObject);
                log.AppendLine($"Canvas '{canvas.name}': UIAlwaysOnTop adicionado (UI não é cortada pelas paredes).");
            }

            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;

            Undo.RecordObject(canvas, "Canvas to camera");
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;
            log.AppendLine($"Canvas '{canvas.name}' -> Screen Space - Camera (distância 1 m).");
        }
    }

    static IEnumerable<GameObject> FindAll(string nameContains)
    {
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.parent == null && t.name.Contains(nameContains))
                yield return t.gameObject;
    }
}
