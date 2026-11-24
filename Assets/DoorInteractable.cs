using UnityEngine;

public enum DoorType
{
    Normal,
    Key,
    Magic
}

public class DoorInteractable : MonoBehaviour
{
    public DoorType doorType = DoorType.Normal;
    public bool isOpen = false;

    [Header("Animação / Rotação")]
    public Transform doorMesh;
    public float openAngle = 90f;

    [Header("Materiais")]
    public Material openMaterial; // Arraste aqui o Material da porta aberta/desbloqueada

    public bool TryOpenDoor(PlayerInventory inventory, bool isMagicPuzzleSolved)
    {
        if (isOpen) return true;

        switch (doorType)
        {
            case DoorType.Normal:
                Open();
                return true;

            case DoorType.Key:
                if (inventory != null && inventory.hasKey)
                {
                    inventory.hasKey = false; // Consome a chave
                    if (DialogueManager.Instance != null)
                        DialogueManager.Instance.ShowMessage("Porta Desbloqueada", "Usou a chave para abrir a porta!");
                    Open();
                    return true;
                }
                else
                {
                    if (DialogueManager.Instance != null)
                        DialogueManager.Instance.ShowMessage("Porta Trancada", "Precisa de uma chave para abrir esta porta.");
                    return false;
                }

            case DoorType.Magic:
                if (isMagicPuzzleSolved || (inventory != null && inventory.hasLearnedMagic) || (inventory != null && inventory.hasCrystal))
                {
                    if (DialogueManager.Instance != null)
                        DialogueManager.Instance.ShowMessage("Magia foi Desfeita!", "A porta se abriu!");
                    Open();
                    return true;
                }
                else
                {
                    if (DialogueManager.Instance != null)
                        DialogueManager.Instance.ShowMessage("Porta Mágica", "Uma energia mística sela esta passagem.");
                    return false;
                }
        }

        return false;
    }

    public void Open()
    {
        isOpen = true;

        // --- TROCA DE MATERIAL ---
        if (openMaterial != null)
        {
            // Busca TODOS os Renderers no objeto e nos seus filhos
            Renderer[] renderers = GetComponentsInChildren<Renderer>();

            foreach (Renderer rend in renderers)
            {
                // Cria um array com o novo material para cada sub-slot de material da malha
                Material[] newMaterials = new Material[rend.materials.Length];
                for (int i = 0; i < newMaterials.Length; i++)
                {
                    newMaterials[i] = openMaterial;
                }
                
                // Aplica o novo array de materiais
                rend.materials = newMaterials;
            }
        }
        else
        {
            Debug.LogWarning($"[DoorInteractable] O 'Open Material' não foi atribuído no Inspector do objeto: {gameObject.name}");
        }

        // --- DESATIVA OS COLLIDERS ---
        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (Collider col in colliders)
        {
            col.enabled = false;
        }

        // --- ANIMAÇÃO / ROTAÇÃO ---
        if (doorMesh != null)
        {
            doorMesh.Rotate(0, openAngle, 0);
        }
    }
}