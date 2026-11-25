using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    [Header("Inventário do Jogador")]
    public bool hasKey = false;
    public bool hasLearnedMagic = false;
    public bool hasCrystal = false;

    [Tooltip("Já usou a chave em uma porta trancada (a chave é consumida). Usado pelas dicas.")]
    public bool usedKey = false;

    /// <summary>Tem a chave agora ou já a usou: a etapa "buscar a chave" já passou.</summary>
    public bool KeyObtained => hasKey || usedKey;

    // Método para coletar a chave
    public void CollectKey()
    {
        hasKey = true;
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.ShowMessage("Chave Encontrada", "Encontrou uma chave antiga! Pode usá-la para abrir portas trancadas.");
        }
    }

    // Método para Magic
    public void LearnMagic()
    {
        hasLearnedMagic = true;
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.ShowMessage("Você aprendeu uma magia", "Ela pode abrir as portas trancadas com magia.");
        }
    }

    // Método para coletar o cristal
    public void CollectCrystal()
    {
        hasCrystal = true;
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.ShowMessage("Cristal Encontrado", "Encontrou um cristal mágico! Pode usá-lo para desbloquear poderes especiais.");
        }
    }
}