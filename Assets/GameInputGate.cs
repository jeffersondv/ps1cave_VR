using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Decide se o gatilho/A/X deve agir no JOGO (interagir com NPC, porta, avançar diálogo) ou se
/// pertence à interface: menu de ajuda aberto, ou laser do controle apontando para UI/objeto interativo.
/// Sem isso, clicar no botão "Fechar" com o gatilho também faria o jogador interagir com o que está à frente.
/// </summary>
public static class GameInputGate
{
    static XRRayInteractor[] rays;
    static int lastRefreshFrame = -1;
    static int lastFindFrame = -1000;
    static int lastPointerBusyFrame = -10;

    /// <summary>Gameplay deve ignorar os botões neste frame.</summary>
    public static bool Blocked => HelpMenu.IsOpen || PointerBusy;

    /// <summary>Algum laser está sobre UI ou objeto interativo (neste frame ou no anterior).</summary>
    public static bool PointerBusy
    {
        get
        {
            Refresh();
            return Time.frameCount - lastPointerBusyFrame <= 1;
        }
    }

    static void Refresh()
    {
        if (lastRefreshFrame == Time.frameCount) return;
        lastRefreshFrame = Time.frameCount;

        bool needFind = rays == null || rays.Length == 0;
        if (!needFind)
            foreach (XRRayInteractor r in rays)
                if (r == null) { needFind = true; break; }

        // Procura os lasers de novo só de vez em quando (evita FindObjects a cada frame)
        if (needFind && Time.frameCount - lastFindFrame > 60)
        {
            lastFindFrame = Time.frameCount;
            rays = Object.FindObjectsByType<XRRayInteractor>(FindObjectsSortMode.None);
        }
        if (rays == null) return;

        foreach (XRRayInteractor r in rays)
        {
            if (r == null || !r.isActiveAndEnabled) continue;
            if (r.hasHover || r.IsOverUIGameObject())
            {
                lastPointerBusyFrame = Time.frameCount;
                return;
            }
        }
    }
}
