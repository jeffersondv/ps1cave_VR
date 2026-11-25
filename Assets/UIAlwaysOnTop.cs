using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Faz toda a UI do Canvas ser desenhada por cima da cena (ZTest Always).
/// Em VR o Canvas "Screen Space - Camera" é um plano a ~1 m da cabeça: ao girar a cabeça
/// perto de uma parede, partes do plano ficam atrás dela e a caixa de diálogo era cortada.
///
/// - Imagens/painéis: material com o shader "UI/AlwaysOnTop".
/// - Textos TextMeshPro: cópia do material da fonte com o shader "Distance Field Overlay".
/// Os shaders ficam referenciados aqui para serem incluídos no build.
/// </summary>
[RequireComponent(typeof(Canvas))]
public class UIAlwaysOnTop : MonoBehaviour
{
    public Shader uiShader;
    public Shader tmpOverlayShader;        // TextMeshPro/Distance Field Overlay
    public Shader tmpMobileOverlayShader;  // TextMeshPro/Mobile/Distance Field Overlay

    Material uiMaterial;
    readonly Dictionary<Material, Material> tmpMaterials = new Dictionary<Material, Material>();

    void Reset() => FindShaders();
    void OnValidate() => FindShaders();

    void FindShaders()
    {
        if (uiShader == null) uiShader = Shader.Find("UI/AlwaysOnTop");
        if (tmpOverlayShader == null) tmpOverlayShader = Shader.Find("TextMeshPro/Distance Field Overlay");
        if (tmpMobileOverlayShader == null) tmpMobileOverlayShader = Shader.Find("TextMeshPro/Mobile/Distance Field Overlay");
    }

    void Start() => Apply();

    /// <summary>Aplica em todos os elementos do Canvas (inclusive inativos). Pode ser chamado de novo.</summary>
    public void Apply()
    {
        FindShaders();
        if (uiShader != null && uiMaterial == null)
            uiMaterial = new Material(uiShader) { name = "UI AlwaysOnTop (runtime)" };

        foreach (Graphic g in GetComponentsInChildren<Graphic>(true))
        {
            if (g is TMP_Text tmp)
            {
                ApplyToText(tmp);
            }
            else if (uiMaterial != null && (g.material == null || g.material == g.defaultMaterial))
            {
                g.material = uiMaterial;
            }
        }
    }

    void ApplyToText(TMP_Text tmp)
    {
        Material original = tmp.fontSharedMaterial;
        if (original == null || original.shader == null) return;
        string shaderName = original.shader.name;
        if (shaderName.Contains("Overlay")) return;

        if (!tmpMaterials.TryGetValue(original, out Material overlay))
        {
            Shader s = shaderName.Contains("Mobile") ? tmpMobileOverlayShader : tmpOverlayShader;
            if (s == null) s = tmpMobileOverlayShader != null ? tmpMobileOverlayShader : tmpOverlayShader;
            if (s == null) return;

            overlay = new Material(original) { name = original.name + " (Overlay)" };
            overlay.shader = s;
            tmpMaterials[original] = overlay;
        }
        tmp.fontSharedMaterial = overlay;
    }

    void OnDestroy()
    {
        if (uiMaterial != null) Destroy(uiMaterial);
        foreach (Material m in tmpMaterials.Values)
            if (m != null) Destroy(m);
    }
}
