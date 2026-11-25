using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Destaque da porta escondida (TileType.DoorHidden). Adicionado automaticamente pelo
/// DungeonGridLevelsDoors ao criar a porta. Ao usar a "Ajuda especial", as portas escondidas
/// do andar atual pulsam (cor dourada no material + luz pontual) por alguns segundos,
/// ajudando o jogador a encontrá-las. Para ao abrir/atravessar a porta.
/// </summary>
public class HiddenDoorGlow : MonoBehaviour
{
    static readonly List<HiddenDoorGlow> active = new List<HiddenDoorGlow>();

    [ColorUsage(false, true)] public Color glowColor = new Color(1f, 0.78f, 0.25f) * 2.2f;
    public float pulseSpeed = 3f;
    public float lightRange = 2.6f;
    public float maxLightIntensity = 3.5f;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    Renderer[] renderers;
    Material[][] originalShared;
    Material[][] glowMaterials;
    Light glowLight;
    Coroutine routine;
    bool done; // porta já usada: não brilha mais

    public bool IsGlowing => routine != null;

    void OnEnable() { if (!active.Contains(this)) active.Add(this); }
    void OnDisable()
    {
        active.Remove(this);
        StopGlow();
    }

    /// <summary>Faz brilhar todas as portas escondidas ativas (andar atual). Retorna quantas.</summary>
    public static int HighlightAll(float seconds)
    {
        int count = 0;
        foreach (HiddenDoorGlow glow in active.ToArray())
        {
            if (glow == null || glow.done || !glow.isActiveAndEnabled) continue;
            glow.Highlight(seconds);
            count++;
        }
        return count;
    }

    /// <summary>Chamado quando o jogador usa/atravessa a porta: para o brilho de vez.</summary>
    public void MarkUsed()
    {
        done = true;
        StopGlow();
    }

    public void Highlight(float seconds)
    {
        if (done) return;
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(GlowRoutine(seconds));
    }

    IEnumerator GlowRoutine(float seconds)
    {
        Setup();
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            // 0..1 pulsando; some suavemente no último segundo
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * pulseSpeed);
            float fade = Mathf.Clamp01(seconds - t);
            Apply(Mathf.Lerp(0.35f, 1f, pulse) * fade);
            yield return null;
        }
        routine = null;
        Restore();
    }

    void Setup()
    {
        if (renderers == null)
        {
            renderers = GetComponentsInChildren<Renderer>();
            originalShared = new Material[renderers.Length][];
            glowMaterials = new Material[renderers.Length][];
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null) continue;
            originalShared[i] = r.sharedMaterials;
            Material[] mats = new Material[originalShared[i].Length];
            for (int m = 0; m < mats.Length; m++)
            {
                if (originalShared[i][m] == null) continue;
                mats[m] = new Material(originalShared[i][m]) { name = originalShared[i][m].name + " (brilho)" };
                mats[m].EnableKeyword("_EMISSION");
                mats[m].globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            glowMaterials[i] = mats;
            r.materials = mats;
        }

        if (glowLight == null)
        {
            var lightGO = new GameObject("Brilho da porta escondida");
            lightGO.transform.SetParent(transform, false);
            // A porta é um plano: transform.up é a normal (lado visível). A luz fica um pouco à frente.
            lightGO.transform.position = transform.position + transform.up.normalized * 0.45f;
            glowLight = lightGO.AddComponent<Light>();
            glowLight.type = LightType.Point;
            glowLight.shadows = LightShadows.None;
            glowLight.color = new Color(1f, 0.8f, 0.35f);
            glowLight.range = lightRange;
            glowLight.renderMode = LightRenderMode.ForcePixel;
        }
        glowLight.enabled = true;
    }

    void Apply(float k)
    {
        if (glowMaterials != null)
        {
            Color baseTint = Color.Lerp(Color.white, glowColor, k);
            Color emission = glowColor * k;
            foreach (Material[] mats in glowMaterials)
            {
                if (mats == null) continue;
                foreach (Material mat in mats)
                {
                    if (mat == null) continue;
                    if (mat.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, baseTint);
                    if (mat.HasProperty(ColorId)) mat.SetColor(ColorId, baseTint);
                    if (mat.HasProperty(EmissionId)) mat.SetColor(EmissionId, emission);
                }
            }
        }
        if (glowLight != null) glowLight.intensity = maxLightIntensity * k;
    }

    void StopGlow()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
        Restore();
    }

    void Restore()
    {
        if (renderers != null)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && originalShared[i] != null)
                    renderers[i].sharedMaterials = originalShared[i];

                if (glowMaterials[i] != null)
                    foreach (Material mat in glowMaterials[i])
                        if (mat != null) Destroy(mat);
                glowMaterials[i] = null;
                originalShared[i] = null;
            }
        }
        if (glowLight != null) glowLight.enabled = false;
    }
}
