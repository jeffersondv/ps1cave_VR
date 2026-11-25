using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.XR.OpenXR;
using TrackedPoseDriver = UnityEngine.InputSystem.XR.TrackedPoseDriver;

/// <summary>
/// Configura os controles do Meta Quest 2/3 no rig do jogador:
///  1. remove o modelo de controle solto (preso ao Player) e cria controles rastreados de verdade;
///  2. laser (XR Ray Interactor) em cada mão, com UI (XRUIInputModule + TrackedDeviceGraphicRaycaster);
///  3. menu de ajuda em World Space, exibido ao iniciar.
/// Pode ser executado de novo: o que já existe é reaproveitado.
/// </summary>
public static class QuestControllersSetup
{
    const string SamplesRoot = "Assets/Samples/XR Interaction Toolkit/3.4.1/Starter Assets/";
    const string ActionsPath = SamplesRoot + "XRI Default Input Actions.inputactions";
    const string LeftModelPath = SamplesRoot + "Prefabs/Controllers/XR Controller Left.prefab";
    const string RightModelPath = SamplesRoot + "Prefabs/Controllers/XR Controller Right.prefab";
    const string RayPrefabPath = SamplesRoot + "Prefabs/Interactors/Ray Interactor.prefab";

    [MenuItem("Tools/Quest/Configurar controles, raycast e menu de ajuda")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[QuestControllers] Saia do Play Mode antes de executar.");
            return;
        }

        var log = new StringBuilder("[QuestControllers]\n");

        InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
        if (actions == null)
        {
            Debug.LogError("[QuestControllers] Não achei " + ActionsPath + " (importe os Starter Assets do XRI).");
            return;
        }
        var refs = AssetDatabase.LoadAllAssetsAtPath(ActionsPath).OfType<InputActionReference>().ToList();
        InputActionReference R(string path)
        {
            InputActionReference r = refs.FirstOrDefault(x => x.action != null &&
                                                              x.action.actionMap.name + "/" + x.action.name == path);
            if (r == null) log.AppendLine("  AVISO: ação não encontrada: " + path);
            return r;
        }

        PlayerController player = Object.FindAnyObjectByType<PlayerController>();
        XROrigin origin = player != null ? player.GetComponentInChildren<XROrigin>(true) : Object.FindAnyObjectByType<XROrigin>();
        if (origin == null || origin.CameraFloorOffsetObject == null)
        {
            Debug.LogError("[QuestControllers] XR Origin / Camera Offset não encontrados. Rode antes 'Corrigir configuração para Meta Quest'.");
            return;
        }
        Transform cameraOffset = origin.CameraFloorOffsetObject.transform;

        RemoveLooseControllerModel(player, log);
        EnsureInteractionManager(log);
        EnsureInputActionManager(origin.gameObject, actions, log);

        SetupController(cameraOffset, "Left Controller", InteractorHandedness.Left, "XRI Left", "XRI Left Interaction",
                        LeftModelPath, R, log);
        SetupController(cameraOffset, "Right Controller", InteractorHandedness.Right, "XRI Right", "XRI Right Interaction",
                        RightModelPath, R, log);

        SetupEventSystem(log);
        SetupHelpMenu(origin.Camera, log);
        EnableQuestControllerProfiles(log);

        EditorSceneManager.MarkSceneDirty(origin.gameObject.scene);
        EditorSceneManager.SaveScene(origin.gameObject.scene);
        log.AppendLine("Cena salva.");
        Debug.Log(log.ToString());
    }

    // ------------------------------------------------------------------ 1. rig

    /// <summary>O modelo "UniversalController" era filho direto do Player: andava/girava grudado na visão.</summary>
    static void RemoveLooseControllerModel(PlayerController player, StringBuilder log)
    {
        if (player == null) return;
        foreach (Transform child in player.transform.Cast<Transform>().ToList())
        {
            if (child.name != "UniversalController") continue;
            GameObject root = PrefabUtility.IsPartOfPrefabInstance(child.gameObject)
                ? PrefabUtility.GetOutermostPrefabInstanceRoot(child.gameObject)
                : child.gameObject;
            if (root.transform.parent != player.transform) root = child.gameObject;
            Undo.DestroyObjectImmediate(root);
            log.AppendLine("Removido 'UniversalController' solto no Player (controle preso à câmera).");
        }
    }

    static void EnsureInteractionManager(StringBuilder log)
    {
        if (Object.FindAnyObjectByType<XRInteractionManager>() != null) return;
        var go = new GameObject("XR Interaction Manager", typeof(XRInteractionManager));
        Undo.RegisterCreatedObjectUndo(go, "XR Interaction Manager");
        log.AppendLine("Criado 'XR Interaction Manager'.");
    }

    static void EnsureInputActionManager(GameObject originGO, InputActionAsset actions, StringBuilder log)
    {
        InputActionManager mgr = originGO.GetComponent<InputActionManager>();
        if (mgr == null)
        {
            mgr = Undo.AddComponent<InputActionManager>(originGO);
            log.AppendLine("XR Origin: adicionado InputActionManager (habilita as ações do XRI).");
        }
        if (mgr.actionAssets == null) mgr.actionAssets = new List<InputActionAsset>();
        if (!mgr.actionAssets.Contains(actions))
        {
            Undo.RecordObject(mgr, "Input actions");
            mgr.actionAssets.Add(actions);
            EditorUtility.SetDirty(mgr);
        }
    }

    static void SetupController(Transform cameraOffset, string name, InteractorHandedness hand,
                                string poseMap, string interactionMap, string modelPath,
                                System.Func<string, InputActionReference> R, StringBuilder log)
    {
        // Controle: filho do Camera Offset (mesmo espaço da câmera), com rastreamento próprio
        Transform existing = cameraOffset.Find(name);
        GameObject controller = existing != null ? existing.gameObject : new GameObject(name);
        if (existing == null)
        {
            Undo.RegisterCreatedObjectUndo(controller, name);
            controller.transform.SetParent(cameraOffset, false);
            log.AppendLine($"Criado '{name}' em Camera Offset.");
        }

        TrackedPoseDriver tpd = controller.GetComponent<TrackedPoseDriver>();
        if (tpd == null) tpd = Undo.AddComponent<TrackedPoseDriver>(controller);
        Undo.RecordObject(tpd, "Tracked Pose");
        tpd.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
        tpd.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
        tpd.positionInput = new InputActionProperty(R(poseMap + "/Position"));
        tpd.rotationInput = new InputActionProperty(R(poseMap + "/Rotation"));
        tpd.trackingStateInput = new InputActionProperty(R(poseMap + "/Tracking State"));
        EditorUtility.SetDirty(tpd);

        // Modelo 3D do controle (anima gatilho/botões)
        if (controller.transform.Find("Model") == null)
        {
            GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (modelPrefab != null)
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab, controller.transform);
                model.name = "Model";
                Undo.RegisterCreatedObjectUndo(model, "Controller model");
            }
        }

        // Laser
        Transform rayT = controller.transform.Find("Ray Interactor");
        GameObject rayGO;
        if (rayT == null)
        {
            GameObject rayPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RayPrefabPath);
            rayGO = (GameObject)PrefabUtility.InstantiatePrefab(rayPrefab, controller.transform);
            rayGO.name = "Ray Interactor";
            Undo.RegisterCreatedObjectUndo(rayGO, "Ray");
        }
        else rayGO = rayT.gameObject;

        XRRayInteractor ray = rayGO.GetComponent<XRRayInteractor>();
        Undo.RecordObject(ray, "Ray config");
        ray.handedness = hand;
        ray.enableUIInteraction = true;
        ray.lineType = XRRayInteractor.LineType.StraightLine;
        ray.maxRaycastDistance = 8f;
        ray.raycastTriggerInteraction = QueryTriggerInteraction.Collide; // opção "Ajuda especial" usa trigger collider
        // Gatilho = selecionar/clicar (no padrão do XRI, selecionar é o grip; no jogo o gatilho é o "apertar")
        ray.selectInput = ButtonRef(R(interactionMap + "/UI Press"), R(interactionMap + "/UI Press Value"));
        ray.activateInput = ButtonRef(R(interactionMap + "/Activate"), R(interactionMap + "/Activate Value"));
        ray.uiPressInput = ButtonRef(R(interactionMap + "/UI Press"), R(interactionMap + "/UI Press Value"));
        ray.uiScrollInput = new XRInputValueReader<Vector2>("UI Scroll", XRInputValueReader.InputSourceMode.InputActionReference)
        {
            inputActionReference = R(interactionMap + "/UI Scroll")
        };
        EditorUtility.SetDirty(ray);
        PrefabUtility.RecordPrefabInstancePropertyModifications(ray);
        log.AppendLine($"  {name}: TrackedPoseDriver ({poseMap}), modelo e laser (gatilho = clicar).");
    }

    static XRInputButtonReader ButtonRef(InputActionReference performed, InputActionReference value)
    {
        return new XRInputButtonReader("Button", "Value", false, XRInputButtonReader.InputSourceMode.InputActionReference)
        {
            inputActionReferencePerformed = performed,
            inputActionReferenceValue = value
        };
    }

    // ------------------------------------------------------------------ 2. UI

    static void SetupEventSystem(StringBuilder log)
    {
        EventSystem es = Object.FindAnyObjectByType<EventSystem>();
        if (es == null)
        {
            var go = new GameObject("EventSystem", typeof(EventSystem));
            Undo.RegisterCreatedObjectUndo(go, "EventSystem");
            es = go.GetComponent<EventSystem>();
        }

        InputSystemUIInputModule old = es.GetComponent<InputSystemUIInputModule>();
        if (old != null)
        {
            Undo.DestroyObjectImmediate(old);
            log.AppendLine("EventSystem: removido InputSystemUIInputModule.");
        }

        XRUIInputModule module = es.GetComponent<XRUIInputModule>();
        if (module == null) module = Undo.AddComponent<XRUIInputModule>(es.gameObject);
        Undo.RecordObject(module, "XR UI module");
        module.enableXRInput = true;              // lasers dos controles
        module.enableMouseInput = true;           // continua clicando com o mouse no Editor
        module.enableTouchInput = false;
        module.enableBuiltinActionsAsFallback = true;
        EditorUtility.SetDirty(module);
        log.AppendLine("EventSystem: XRUIInputModule (laser + mouse).");
    }

    // ------------------------------------------------------------------ 3. menu de ajuda

    const string HelpBody =
        "<b>Analógico esquerdo</b>: para frente ou para trás anda um bloco; para os lados, passo lateral.\n" +
        "<b>Analógico direito</b>: gira 90° para a esquerda ou para a direita.\n" +
        "<b>Gatilho ou botão A / X</b>: interage com o que está à sua frente (personagens, portas, baús) e avança os diálogos.\n" +
        "<b>Apontar o controle (laser) + gatilho</b>: clica em botões e na opção vermelha \"Ajuda especial\".\n" +
        "<b>Olhar fixo</b> para o círculo vermelho também escolhe a \"Ajuda especial\".\n" +
        "<b>Botão Menu</b> (controle esquerdo): abre e fecha esta ajuda. <b>B / Y</b> também fecham.\n" +
        "<b>Botão Meta</b> (controle direito): menu do sistema do Quest.";

    static void SetupHelpMenu(Camera cam, StringBuilder log)
    {
        HelpMenu existing = Object.FindAnyObjectByType<HelpMenu>(FindObjectsInactive.Include);
        if (existing != null)
        {
            log.AppendLine("Menu de ajuda já existe: mantido (apague-o e rode de novo para recriar).");
            return;
        }

        Sprite panelSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        Sprite buttonSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        // Canvas em World Space: 1000 x 860 px * 0.001 = 1,0 m x 0,86 m
        var root = new GameObject("Help Menu (World Space)", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                                  typeof(TrackedDeviceGraphicRaycaster), typeof(HelpMenu));
        Undo.RegisterCreatedObjectUndo(root, "Help Menu");
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = cam;
        canvas.sortingOrder = 10;
        root.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 3f;
        var rt = (RectTransform)root.transform;
        rt.sizeDelta = new Vector2(1000, 860);
        rt.localScale = Vector3.one * 0.001f;
        rt.position = cam != null ? cam.transform.position + cam.transform.forward * 0.85f : Vector3.zero;

        // Painel: moldura dourada (Content) + fundo escuro opaco (Panel)
        var content = NewUI("Content", root.transform);
        Stretch(content, 0);
        var frame = content.AddComponent<Image>();
        frame.sprite = panelSprite;
        frame.type = Image.Type.Sliced;
        frame.color = new Color(1f, 0.8f, 0.25f, 1f);
        frame.raycastTarget = false;
        var panel = NewUI("Panel", content.transform);
        Stretch(panel, 6);
        var bg = panel.AddComponent<Image>();
        bg.sprite = panelSprite;
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.04f, 0.06f, 0.14f, 1f);
        bg.raycastTarget = false;

        var title = NewText("Title", content.transform, "Como jogar  •  Meta Quest 2 e 3", 46, FontStyles.Bold,
                            TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.3f));
        Anchor(title, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -28), new Vector2(-60, 64));

        var body = NewText("Body", content.transform, HelpBody, 28, FontStyles.Normal, TextAlignmentOptions.TopLeft, Color.white);
        body.GetComponent<TextMeshProUGUI>().lineSpacing = 4f;
        body.GetComponent<TextMeshProUGUI>().paragraphSpacing = 14f;
        // 110 px livres em cima (título) e 150 px embaixo (botão)
        var bodyRt = (RectTransform)body.transform;
        bodyRt.anchorMin = Vector2.zero;
        bodyRt.anchorMax = Vector2.one;
        bodyRt.offsetMin = new Vector2(50, 150);
        bodyRt.offsetMax = new Vector2(-50, -110);

        // Botão Fechar
        var button = NewUI("Close Button", content.transform);
        Anchor(button, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 34), new Vector2(380, 84));
        var bImg = button.AddComponent<Image>();
        bImg.sprite = buttonSprite;
        bImg.type = Image.Type.Sliced;
        bImg.color = new Color(0.8f, 0.1f, 0.1f, 1f);
        var btn = button.AddComponent<Button>();
        ColorBlock colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.75f, 0.75f);
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f);
        colors.selectedColor = Color.white;
        btn.colors = colors;
        var label = NewText("Label", button.transform, "Fechar e jogar", 36, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
        Stretch(label, 0);

        var help = root.GetComponent<HelpMenu>();
        help.content = content;
        help.closeButton = btn;

        log.AppendLine("Criado 'Help Menu (World Space)': Canvas World Space + TrackedDeviceGraphicRaycaster + HelpMenu " +
                       "(abre ao iniciar, botão 'Fechar e jogar' via laser, botão Menu reabre).");
    }

    static GameObject NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return go;
    }

    static GameObject NewText(string name, Transform parent, string text, float size, FontStyles style,
                              TextAlignmentOptions align, Color color)
    {
        var go = NewUI(name, parent);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.alignment = align;
        tmp.color = color;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.raycastTarget = false; // só o botão recebe o laser
        return go;
    }

    static void Stretch(GameObject go, float margin)
    {
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(margin, margin);
        rt.offsetMax = new Vector2(-margin, -margin);
    }

    static void Anchor(GameObject go, Vector2 min, Vector2 max, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var rt = (RectTransform)go.transform;
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    // ------------------------------------------------------------------ OpenXR

    /// <summary>Quest 3 usa o Touch Plus; o perfil Oculus Touch (Quest 2) já foi ligado pelo outro menu.</summary>
    static void EnableQuestControllerProfiles(StringBuilder log)
    {
        foreach (BuildTargetGroup group in new[] { BuildTargetGroup.Android, BuildTargetGroup.Standalone })
        {
            OpenXRSettings settings = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
            if (settings == null) continue;
            foreach (var feature in settings.GetFeatures())
            {
                string type = feature.GetType().Name;
                if ((type == "OculusTouchControllerProfile" || type == "MetaQuestTouchPlusControllerProfile") && !feature.enabled)
                {
                    feature.enabled = true;
                    EditorUtility.SetDirty(feature);
                    log.AppendLine($"OpenXR ({group}): ligado {type}.");
                }
            }
        }
        AssetDatabase.SaveAssets();
    }
}
