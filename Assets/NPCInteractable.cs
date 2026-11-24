using UnityEngine;
using System.Collections.Generic;

public class NPCInteractable : MonoBehaviour
{
    [Header("Informações do Enigma / Diálogo")]
    public string objectName = "Estátua Antiga";

    [TextArea(3, 5)]
    public string promptForOllama;
    
    [TextArea(3, 5)]
    public string riddleHint = "Para abrir a porta de ferro, deves olhar para o norte e pronunciar a palavra secreta.";

    [Header("Lista de Dicas")]
    public List<string> hintList = new List<string>();

    [Header("Referência ao Baú Filho")]
    [SerializeField] private GameObject chestChild;

    [Header("Dicas Especiais")]
    public string SpecialHint = "Esta é uma dica especial que pode ser usada em algum momento do jogo.";

    public int numeroDeInteracoes = 0;

    public bool SempreAparecer = true;

    [Header("Som ao encontrar")]
    public AudioSource prefabSound;


    private DungeonGridLevelsDoors dungeonGrid;
    private int spawnedFloorIndex;
    private int spawnedGridX;
    private int spawnedGridZ;

    // Método chamado quando o jogador interage
    public void Interact()
    {
        //Debug.Log($"[{objectName}]: {riddleHint}");
        
        // Se tiver um Canvas de UI, pode enviar a mensagem para o ecrã aqui:
        // UIManager.Instance.ShowDialogue(objectName, riddleHint);

        this.numeroDeInteracoes++;
        CountdownTimer.RegisterNPCInteraction(this);

		// Exibe a mensagem na interface gráfica do jogo
        if (DialogueManager.Instance != null)
        {
            
            if(objectName == "NPCChest")
            {
                this.gameObject.SetActive(false);
                if (chestChild != null)
                {
                    Debug.Log($"Baú encontrado! {chestChild.name}");
                    chestChild.SetActive(true); // Ativa o objeto do baú                    
                }
            }else if(objectName == "Mago")
            {
                Debug.Log($"NPCQuestion! {numeroDeInteracoes}");
                if (this.numeroDeInteracoes >= 2)
                {
                    DialogueManager.Instance.ShowMessage(objectName, SpecialHint);
                    RemoveFromMapIfNeeded();
                    return; // Sai do método para não mostrar a dica normal
                }
            }else if(objectName == "Dragon" || objectName == "Red Dragon")
            {
                Debug.Log($"Dragon! {numeroDeInteracoes}");
                if (this.numeroDeInteracoes >= 1)
                {
                    DialogueManager.Instance.ShowMessage(objectName, SpecialHint);
                    
                    CountdownTimer countdownTimer = FindObjectOfType<CountdownTimer>();
                    if (countdownTimer != null)
                    {
                        countdownTimer.FinishCountdown();
                        countdownTimer.ShowGameOver();
                    }
                    return; // Sai do método para não mostrar a dica normal
                }
            }else
            {
                if (chestChild != null)
                {
                    
                    Debug.Log($"Baú encontrado! {chestChild.name}");
                    chestChild.SetActive(true); // Ativa o objeto do baú
                }

                if (this.numeroDeInteracoes >= 2)
                {
                    DialogueManager.Instance.ShowMessage(objectName, SpecialHint);
                    RemoveFromMapIfNeeded();
                    return; // Sai do método para não mostrar a dica normal
                }
            }

            DialogueManager.Instance.ShowMessage(objectName, riddleHint);

            if (hintList != null && hintList.Count > 0)
            {
                foreach (string hint in hintList)
                {
                    DialogueManager.Instance.ShowMessage(objectName, hint);
                }
            }

        }

        RemoveFromMapIfNeeded();
		
        /*if (Chest.Instance != null)
        {
            this.gameObject.SetActive(false); // Desativa o objeto do baú
            Chest.Instance.visible = true;
        }*/

		
    }

    public void SetChestChild(GameObject chest)
    {
        chestChild = chest;
    }

    public void SetMapTile(DungeonGridLevelsDoors grid, int floorIndex, int gridX, int gridZ)
    {
        dungeonGrid = grid;
        spawnedFloorIndex = floorIndex;
        spawnedGridX = gridX;
        spawnedGridZ = gridZ;
    }

    private void RemoveFromMapIfNeeded()
    {
        if (SempreAparecer)
            return;

        if (dungeonGrid != null)
            dungeonGrid.RemoveNPCFromMap(spawnedFloorIndex, spawnedGridX, spawnedGridZ);

        gameObject.SetActive(false);
    }


    public int GetInteractionCount()
    {
        return numeroDeInteracoes;
    }

    public bool GetAlwaysAppear()
    {
        return SempreAparecer;
    }

    public GameObject GetChestChild()
    {
        return chestChild;
    }


}