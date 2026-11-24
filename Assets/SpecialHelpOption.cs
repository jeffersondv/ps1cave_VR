using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Opção "Ajuda especial" selecionável em VR: um círculo vermelho que aparece no mundo,
/// ao lado do NPC, depois de N interações. Seleção por olhar:
///   - olhar para o círculo e segurar o olhar até o anel encher (dwell), ou
///   - olhar para o círculo e apertar A/X / gatilho (ou E/Espaço/clique no Editor).
/// Ao selecionar, pede a resposta ao Ollama através do DungeonNPC.
///
/// Os visuais são criados por código (SpriteRenderer + LineRenderer + TextMeshPro),
/// sem prefab e sem shaders que possam ser removidos do build.
/// </summary>
[DefaultExecutionOrder(-50)] // decide o "olhar" antes do PlayerController/DialogueManager lerem o botão
public class SpecialHelpOption : MonoBehaviour
{
    public static SpecialHelpOption Instance { get; private set; }

    /// <summary>Verdadeiro enquanto o jogador olha para a opção: o botão de interação é dela.</summary>
    public static bool IsGazed => Instance != null && Instance.visible && Instance.gazed;

    [Header("Texto")]
    public string label = "Ajuda especial";

    [Header("Posição (relativa à câmera, em direção ao NPC)")]
    public float distance = 0.9f;
    public float sideOffset = 0.28f;   // à direita, para não cobrir o NPC
    public float heightOffset = 0.02f; // o painel de diálogo fica na parte de baixo da visão

    [Header("Seleção")]
    public float radius = 0.09f;
    [Tooltip("Tempo olhando fixamente para selecionar (s)")]
    public float dwellTime = 1.5f;
    [Tooltip("Folga angular extra para considerar que está olhando (graus)")]
    public float gazeToleranceDeg = 3f;

    [Header("Cores")]
    public Color circleColor = new Color(0.85f, 0.05f, 0.05f, 1f);
    public Color circleGazedColor = new Color(1f, 0.2f, 0.2f, 1f);
    public Color ringColor = Color.white;

    Transform visualRoot;
    SpriteRenderer disc;
    LineRenderer ring;
    TextMeshPro text;
    const int RingSegments = 48;

    bool visible;
    bool gazed;
    float dwell;
    DungeonNPC targetNpc;
    string targetTitle;
    Transform player;
    Vector3 shownAtPlayerPos;
    Camera cam;

    /// <summary>Mostra a opção para o NPC indicado (cria o objeto na primeira vez).</summary>
    public static void Show(DungeonNPC npc, string title, Transform playerTransform)
    {
        if (Instance == null)
            new GameObject("SpecialHelpOption").AddComponent<SpecialHelpOption>();
        Instance.ShowInternal(npc, title, playerTransform);
    }

    public static void HideOption()
    {
        if (Instance != null) Instance.SetVisible(false);
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        BuildVisuals();
        SetVisible(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void ShowInternal(DungeonNPC npc, string title, Transform playerTransform)
    {
        targetNpc = npc;
        targetTitle = title;
        player = playerTransform;
        shownAtPlayerPos = player != null ? player.position : Vector3.zero;
        cam = Camera.main;
        if (cam == null && player != null) cam = player.GetComponentInChildren<Camera>();
        if (cam == null || npc == null) return;

        // Posiciona entre o jogador e o NPC, um pouco à direita, virado para a câmera
        Vector3 toNpc = npc.transform.position - cam.transform.position;
        toNpc.y = 0f;
        if (toNpc.sqrMagnitude < 0.0001f) toNpc = cam.transform.forward;
        toNpc.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, toNpc);

        transform.position = cam.transform.position + toNpc * distance + right * sideOffset + Vector3.up * heightOffset;
        transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position, Vector3.up);

        dwell = 0f;
        SetVisible(true);
    }

    void SetVisible(bool value)
    {
        visible = value;
        gazed = false;
        dwell = 0f;
        if (visualRoot != null) visualRoot.gameObject.SetActive(value);
    }

    void Update()
    {
        if (!visible) return;

        // Esconde se o jogador andou, o NPC sumiu ou o jogo pausou os controles
        // (o componente DungeonNPC pode estar desabilitado nos prefabs; vale o GameObject ativo)
        if (player == null || targetNpc == null || !targetNpc.gameObject.activeInHierarchy ||
            Vector3.Distance(player.position, shownAtPlayerPos) > 0.2f)
        {
            SetVisible(false);
            return;
        }

        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        // Olhar: ângulo entre a frente da câmera e o centro do círculo
        Vector3 toOption = transform.position - cam.transform.position;
        float angle = Vector3.Angle(cam.transform.forward, toOption);
        float maxAngle = Mathf.Atan2(radius, toOption.magnitude) * Mathf.Rad2Deg + gazeToleranceDeg;
        gazed = angle <= maxAngle;

        bool pressed = OpenXRControllerInput.InteractPressedThisFrame;
        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (kb != null && (kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) pressed = true;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame) pressed = true;

        if (gazed)
        {
            dwell += Time.deltaTime;
            if (pressed || dwell >= dwellTime)
            {
                Select();
                return;
            }
        }
        else
        {
            dwell = Mathf.Max(0f, dwell - Time.deltaTime * 2f);
        }

        UpdateVisuals();
    }

    void Select()
    {
        DungeonNPC npc = targetNpc;
        string title = targetTitle;
        SetVisible(false);

        if (npc == null) return;
        npc.RequestSpecialHelp(title);
    }

    // ------------------------------------------------------------------ visuais

    void BuildVisuals()
    {
        visualRoot = new GameObject("Visual").transform;
        visualRoot.SetParent(transform, false);

        // Disco vermelho
        var discGO = new GameObject("Circle");
        discGO.transform.SetParent(visualRoot, false);
        disc = discGO.AddComponent<SpriteRenderer>();
        disc.sprite = CreateCircleSprite(128);
        disc.color = circleColor;
        float diameter = radius * 2f;
        discGO.transform.localScale = new Vector3(diameter, diameter, 1f); // sprite de 1 unidade

        // Anel de progresso (dwell)
        var ringGO = new GameObject("Progress");
        ringGO.transform.SetParent(visualRoot, false);
        ring = ringGO.AddComponent<LineRenderer>();
        ring.useWorldSpace = false;
        ring.loop = false;
        ring.widthMultiplier = 0.012f;
        ring.numCapVertices = 2;
        ring.material = new Material(Shader.Find("Sprites/Default"));
        ring.startColor = ring.endColor = ringColor;
        ring.positionCount = 0;
        ring.sortingOrder = 1;

        // Texto "?" no centro e rótulo abaixo
        var qGO = new GameObject("Icon");
        qGO.transform.SetParent(visualRoot, false);
        var q = qGO.AddComponent<TextMeshPro>();
        q.text = "?";
        q.fontSize = 0.9f;
        q.alignment = TextAlignmentOptions.Center;
        q.color = Color.white;
        q.rectTransform.sizeDelta = new Vector2(radius * 2f, radius * 2f);
        q.sortingOrder = 2;
        qGO.transform.localPosition = new Vector3(0f, 0f, -0.002f);

        var tGO = new GameObject("Label");
        tGO.transform.SetParent(visualRoot, false);
        text = tGO.AddComponent<TextMeshPro>();
        text.text = label;
        text.fontSize = 0.38f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.outlineWidth = 0.2f;
        text.outlineColor = Color.black;
        text.rectTransform.sizeDelta = new Vector2(0.5f, 0.08f);
        text.sortingOrder = 2;
        tGO.transform.localPosition = new Vector3(0f, -(radius + 0.05f), 0f);
        // A raiz tem +Z apontando para longe da câmera: TMP e sprite já ficam de frente para ela.
    }

    void UpdateVisuals()
    {
        disc.color = gazed ? circleGazedColor : circleColor;
        visualRoot.localScale = Vector3.one * (gazed ? 1.12f : 1f);

        float progress = dwellTime > 0f ? Mathf.Clamp01(dwell / dwellTime) : 0f;
        int count = Mathf.Max(0, Mathf.CeilToInt(progress * RingSegments) + 1);
        if (progress <= 0f) count = 0;
        ring.positionCount = count;
        float r = radius + 0.018f;
        for (int i = 0; i < count; i++)
        {
            float a = Mathf.PI / 2f - (i / (float)RingSegments) * Mathf.PI * 2f;
            ring.SetPosition(i, new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, -0.004f));
        }
    }

    static Sprite CreateCircleSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        float c = (size - 1) / 2f;
        float rOuter = size / 2f - 1f;
        float rInner = rOuter - size * 0.06f; // borda mais clara
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c));
                float alpha = Mathf.Clamp01(rOuter - d + 0.5f);
                byte shade = d > rInner ? (byte)255 : (byte)215;
                pixels[y * size + x] = new Color32(shade, shade, shade, (byte)(alpha * 255));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
