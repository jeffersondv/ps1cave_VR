using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Referências")]
    public DungeonGridLevelsDoors dungeonGrid;
    // Adicione estas variáveis ao seu PlayerController
	public PlayerInventory inventory;
	public bool isMagicPuzzleSolved = false;

    [Header("Parâmetros de Movimento")]
    public float moveSpeed = 3.0f;
    [Tooltip("Move o jogador diretamente para o centro do próximo tile.")]
    public bool instantTileMovement = false;
    public float rotateSpeed = 500.0f;
    public float tileSize = 2.0f;

    [Header("Posição Inicial no Grid (X, Z)")]
    public int gridX = 1;
    public int gridZ = 1;
    public int rotationY = 0; // Rotação inicial em Y (em graus)

    [Tooltip("No VR, o giro de 90° é instantâneo (snap turn) em vez de animado.")]
    public bool snapTurnInstantaneoVR = true;

    private bool isMoving = false;
    private bool isChangingFloor = false;
    private MovimentoUnidadesVR movimentoUnidadesVR;
    private VRTileRig vrTileRig;

    /// <summary>Verdadeiro enquanto o jogador anda, gira ou troca de andar.</summary>
    public bool IsBusy => isMoving || isChangingFloor;

    // Variável para detetar se o último passo foi dado de costas
    private bool lastMoveWasBackward = false;

    [Header("Configurações de Câmera e Interação")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float interactDistance = 2.0f;
    [SerializeField] private LayerMask interactableLayer;
    [SerializeField] private IdleHelpTextOllama idleHelpText;
    [SerializeField] private Transform xrOrigin;

    void Start()
    {
        if (idleHelpText == null)
            idleHelpText = GetComponent<IdleHelpTextOllama>();

        movimentoUnidadesVR = GetComponent<MovimentoUnidadesVR>();

        if (xrOrigin == null)
            xrOrigin = transform.Find("XR Origin (XR Rig)");

        transform.position = new Vector3(gridX * tileSize, tileSize / 2f, gridZ * tileSize);
        transform.rotation = Quaternion.Euler(0, rotationY, 0);

        vrTileRig = GetComponent<VRTileRig>();
        vrTileRig?.RecenterView();
    }

    public void SetOllamaPromptFrom(GameObject interactedObject)
    {
        if (idleHelpText == null || interactedObject == null)
            return;

        NPCInteractable npc = interactedObject.GetComponentInParent<NPCInteractable>();
        DungeonNPC dungeonNpc = interactedObject.GetComponentInParent<DungeonNPC>();

        string prompt = npc != null ? npc.promptForOllama : null;
        if (string.IsNullOrWhiteSpace(prompt) && dungeonNpc != null)
            prompt = dungeonNpc.SystemPrompt;

        idleHelpText.SetPromptForOllama(prompt);
    }

    public void FreezeControls()
    {
        StopAllCoroutines();
        isMoving = false;
        enabled = false;
    }

    public void FreezeMovement()
    {
        StopAllCoroutines();
        isMoving = true; // Impede novos movimentos, mas mantém o script ativo para outras interações
    }

    public void DisableIdleHelpText()
    {
        if (idleHelpText == null)
            idleHelpText = GetComponent<IdleHelpTextOllama>();

        if (idleHelpText != null)
            idleHelpText.DisableHelpText();
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        bool xrInteractPressed = OpenXRControllerInput.InteractPressedThisFrame;

        if (keyboard != null && keyboard.pKey.wasPressedThisFrame)
        {
            DebugPlayerPosition();
        }

        // Oculus Touch: botão A/X ou gatilho; E/Espaço continuam disponíveis no editor.
        // Se o jogador está olhando para a opção "Ajuda especial", o botão é dela
        if (SpecialHelpOption.IsGazed)
            xrInteractPressed = false;

        if (!SpecialHelpOption.IsGazed &&
            ((keyboard != null && (keyboard.eKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)) ||
             xrInteractPressed))
        {
            TryInteract();
            return;
        }

        if (isMoving) return;

        if (movimentoUnidadesVR != null && movimentoUnidadesVR.enabled)
            return;

        Vector2 thumbstick = OpenXRControllerInput.LeftThumbstick;

        if ((keyboard != null && (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)) ||
            thumbstick.y >= OpenXRControllerInput.StickDeadzone)
        {
            TryMove(transform.forward);
        }
        else if ((keyboard != null && (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)) ||
                 thumbstick.y <= -OpenXRControllerInput.StickDeadzone)
        {
            TryMove(-transform.forward);
        }
        else if ((keyboard != null && (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)) ||
                 thumbstick.x <= -OpenXRControllerInput.StickDeadzone)
        {
            StartCoroutine(Rotate(-90f));
        }
        else if ((keyboard != null && (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)) ||
                 thumbstick.x >= OpenXRControllerInput.StickDeadzone)
        {
            StartCoroutine(Rotate(90f));
        }
    }

    public void TryMoveVR(Vector3 direction)
    {
        if (!isMoving)
            TryMove(direction);
    }

    public void RotateVR(float angle)
    {
        if (IsBusy)
            return;

        if (snapTurnInstantaneoVR)
        {
            // Giro instantâneo (snap turn) é mais confortável no headset
            float yaw = Mathf.Round((transform.eulerAngles.y + angle) / 90f) * 90f;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            return;
        }

        StartCoroutine(Rotate(angle));
    }

    void TryInteract()
    {
        // Calcula a posição do bloco logo à frente do jogador
        Vector3 checkPos = transform.position + transform.forward * tileSize;

        // Executa uma verificação na célula à frente
        Collider[] hitColliders = Physics.OverlapSphere(checkPos, 0.5f);
        
        foreach (var hitCollider in hitColliders)
        {
            NPCInteractable npc = hitCollider.GetComponent<NPCInteractable>();
            DungeonNPC npcd = hitCollider.GetComponent<DungeonNPC>();     
            GameObject npcgameObject = hitCollider.gameObject; // Armazena a referência ao GameObject do NPC

            if (npc != null)
            {
                SetOllamaPromptFrom(hitCollider.gameObject);

                Debug.Log($"[{npc.objectName}]:");


                /*if(npc.objectName == "Guia" && (inventory != null && !inventory.hasLearnedMagic))
                {
                
                    //inventory.LearnMagic();
                }*/
                if(npc.objectName == "Cristal")
                {
                    inventory.CollectCrystal();
                
                }else if(npc.objectName == "NPCChest")
                {   

                    if (npcgameObject.activeInHierarchy)
                    {
                        npc.Interact();
                        /*if (playerCamera == null) playerCamera = Camera.main;
                        
                        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

                        Debug.DrawRay(ray.origin, ray.direction * 5.0f, Color.red, 2.0f);
                        // Dispara o raio para a frente do jogador
                        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactableLayer))
                        {
                            // Pega o componente NPC do objeto que foi atingido pelo raio
                            if (hit.collider.TryGetComponent<NPCInteractable>(out var npcHit))
                            {
                                Debug.Log($"Interagindo com: {npcHit.objectName}");
                                hit.collider.gameObject.SetActive(true); // Ativa o objeto do baú
                                //npcHit.Interact();
                                Debug.Log("Baú encontrado!");

                            }
                        }*/
                        //npcgameObject.SetActive(false);
                        
                        break;
                    }
                }else if(npc.objectName == "Chest")
                {
                    if (npcgameObject.activeInHierarchy && (inventory != null && !inventory.hasKey))
                    {
                        npcgameObject.SetActive(false);
                        inventory.hasKey = true; // Marca que o jogador agora possui a chave
                    }else if (npcgameObject.activeInHierarchy && (inventory != null && inventory.hasKey))
                    {
                        Debug.Log("Você já possui a chave!");
                    }
                }else if(npc.objectName == "Red Dragon" || npc.objectName == "Dragon")
                {
                    Debug.Log("Dragão venceu!");

                    npc.prefabSound.Stop(); // Para o som do dragão se estiver tocando
                    
                    //npcgameObject.SetActive(true); // O dragão aparece quando o jogador interage com ele
                    
                    /*CountdownTimer countdownTimer = FindObjectOfType<CountdownTimer>();
                    if (countdownTimer != null)
                    {
                        countdownTimer.stopSeamlessSound();
                    }
                    npc.prefabSound.PlayScheduled(AudioSettings.dspTime + 0.1); // Toca o som do dragão
                    */


                }


                npc.Interact();

                // NPCs com IA: a partir da N-ésima conversa aparece a opção "Ajuda especial"
                // (círculo vermelho); só ao selecioná-la o Ollama é chamado.
                // O dragão fica de fora: a interação com ele encerra o jogo.
                bool isDragon = npc.objectName == "Dragon" || npc.objectName == "Red Dragon";
                if (npcd != null && !isDragon && npcd.RegisterInteraction())
                    SpecialHelpOption.Show(npcd, npc.objectName, transform);

                break;
            }else{
                if (npcd != null)
                {
                    SetOllamaPromptFrom(hitCollider.gameObject);
                    CountdownTimer.RegisterNPCInteraction(npcd);
                    if (DialogueManager.Instance != null)
                        DialogueManager.Instance.ShowMessage("???", "...");

                    if (npcd.RegisterInteraction())
                        SpecialHelpOption.Show(npcd, null, transform);
                }
            }
        }
        
            // Procura por uma porta na posição à frente
            DoorInteractable door = GetDoorAtPosition(checkPos);

            if (door != null)
            {
                // Se encontrar a porta, tenta abrir e aciona a troca de material
                bool opened = door.TryOpenDoor(inventory, isMagicPuzzleSolved);
                if (opened)
                {
                    int doorGridX = Mathf.RoundToInt(checkPos.x / tileSize);
                    int doorGridZ = Mathf.RoundToInt(checkPos.z / tileSize);
                    dungeonGrid.SetTileType(doorGridX, doorGridZ, TileType.DoorOpen);
                }
            }
        
    }

    void ListAllDirectChildren()
    {
        Debug.Log($"--- Listando filhos de {gameObject.name} ---");
        foreach (Transform child in transform)
        {
            Debug.Log($"Filho encontrado: '{child.name}' | Ativo: {child.gameObject.activeSelf}");
        }
    }

    public void DebugPlayerPosition()
    {
        int positionGridX = Mathf.RoundToInt(transform.position.x / tileSize);
        int positionGridZ = Mathf.RoundToInt(transform.position.z / tileSize);
        TileType tile = dungeonGrid != null
            ? dungeonGrid.GetTileType(positionGridX, positionGridZ)
            : TileType.Wall;

        Debug.Log(
            $"[Player] Mundo=({transform.position.x:F2}, {transform.position.y:F2}, {transform.position.z:F2}) " +
            $"Grade=({positionGridX}, {positionGridZ}) Tile={tile} " +
            $"Andar={dungeonGrid?.currentFloorIndex.ToString() ?? "sem grade"} " +
            $"RotacaoY={transform.eulerAngles.y:F1}",
            this);
    }
    
    IEnumerator MoveTo(Vector3 targetPosition)
    {
        isMoving = true;

        if (instantTileMovement)
        {
            transform.position = targetPosition;
            isMoving = false;

            gridX = Mathf.RoundToInt(transform.position.x / tileSize);
            gridZ = Mathf.RoundToInt(transform.position.z / tileSize);

            CheckForItems();
            CheckSpecialTiles();
            DebugPlayerPosition();
            yield break;
        }
        
        while (Vector3.Distance(transform.position, targetPosition) > 0.001f)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
            yield return null;
        }
        transform.position = targetPosition;
        isMoving = false;

        gridX = Mathf.RoundToInt(transform.position.x / tileSize);
        gridZ = Mathf.RoundToInt(transform.position.z / tileSize);

        CheckForItems();

        CheckSpecialTiles();
        DebugPlayerPosition();


    }

    IEnumerator Rotate(float angle)
    {
        isMoving = true;
        Quaternion startRotation = transform.rotation;
        Quaternion targetRotation = transform.rotation * Quaternion.Euler(0, angle, 0);

        float progress = 0f;
        while (progress < 1f)
        {
            progress += Time.deltaTime * (rotateSpeed / 90f);
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, progress);
            yield return null;
        }

        transform.rotation = targetRotation;
        isMoving = false;
    }

    /*void TryMove(Vector3 direction)
    {
        Vector3 targetPos = transform.position + direction * tileSize;

        int targetGridX = Mathf.RoundToInt(targetPos.x / tileSize);
        int targetGridZ = Mathf.RoundToInt(targetPos.z / tileSize);

        if (dungeonGrid != null && dungeonGrid.IsWalkable(targetGridX, targetGridZ))
        {
            gridX = targetGridX;
            gridZ = targetGridZ;
            StartCoroutine(MoveTo(targetPos));
        }
    }*/
	
    /*
	void TryMove(Vector3 direction)
	{
		Vector3 targetPos = transform.position + direction * tileSize;

		int targetGridX = Mathf.RoundToInt(targetPos.x / tileSize);
		int targetGridZ = Mathf.RoundToInt(targetPos.z / tileSize);

		TileType targetTile = dungeonGrid.GetTileType(targetGridX, targetGridZ);

		// Se for uma porta, tenta abrir primeiro
		if (targetTile == TileType.DoorNormal || targetTile == TileType.DoorKey || targetTile == TileType.DoorMagic)
		{
            //Debug.Log("TryOpenDoor");
			Collider[] hitColliders = Physics.OverlapSphere(targetPos, 0.8f);
            
            Debug.Log($"[DEBUG] Total de Colliders encontrados: {hitColliders.Length}");

			foreach (var col in hitColliders)
			{
				DoorInteractable door = col.GetComponent<DoorInteractable>();

                string parentName = col.transform.parent != null ? col.transform.parent.name : "Nenhum";
                string layerName = LayerMask.LayerToName(col.gameObject.layer);

                Debug.Log($" -> Objeto: '{col.name}' | Pai: '{parentName}' | Layer: '{layerName}' | IsTrigger: {col.isTrigger}");
                
				if (door != null)
				{

                    Debug.Log("TryOpenDoor2");

					if (door.TryOpenDoor(inventory, isMagicPuzzleSolved))
					{
						// Altera o tipo de tile para aberto permitindo a passagem
						dungeonGrid.SetTileType(targetGridX, targetGridZ, TileType.DoorOpen);
					}
					return; // Impede o movimento até a porta ser aberta
				}
			}
		}

		// Se o caminho estiver livre, avança
		if (dungeonGrid != null && dungeonGrid.IsWalkable(targetGridX, targetGridZ))
		{
			gridX = targetGridX;
			gridZ = targetGridZ;
			StartCoroutine(MoveTo(targetPos));
		}
	}*/

    void TryMove(Vector3 direction)
    {
        if (isMoving)
            return;
        if (dungeonGrid == null)
            return;

        direction.y = 0f;
        direction = Vector3.Normalize(direction);
        direction = Mathf.Abs(direction.x) >= Mathf.Abs(direction.z)
            ? new Vector3(Mathf.Sign(direction.x), 0f, 0f)
            : new Vector3(0f, 0f, Mathf.Sign(direction.z));

        // Se o vetor da direção do movimento for oposto à visão atual, foi um movimento de costas
        lastMoveWasBackward = Vector3.Dot(direction, transform.forward) < -0.5f;

        Vector3 currentTilePosition = new Vector3(
            Mathf.Round(transform.position.x / tileSize) * tileSize,
            transform.position.y,
            Mathf.Round(transform.position.z / tileSize) * tileSize);

        Vector3 doorPos = currentTilePosition + direction * tileSize;
        int doorGridX = Mathf.RoundToInt(doorPos.x / tileSize);
        int doorGridZ = Mathf.RoundToInt(doorPos.z / tileSize);

        TileType targetTile = dungeonGrid.GetTileType(doorGridX, doorGridZ);
        Debug.Log($"[Move] Tentativa=({doorGridX},{doorGridZ}) Tile={targetTile} Posicao={transform.position}", this);

        // Inclui DoorOpen para permitir atravessar portas já abertas sem parar no meio
        if (targetTile == TileType.DoorNormal || targetTile == TileType.DoorKey ||
            targetTile == TileType.DoorMagic || targetTile == TileType.DoorOpen )
        {
            // Se a porta ainda está fechada, tenta abrir
            if (targetTile != TileType.DoorOpen)
            {
                DoorInteractable door = GetDoorAtPosition(doorPos);
                if (door != null)
                {
                    if (!door.TryOpenDoor(inventory, isMagicPuzzleSolved))
                    {
                        return; // Porta trancada / enigma pendente: impede o movimento
                    }
                    // Atualiza o estado da porta para aberta no grid
                    dungeonGrid.SetTileType(doorGridX, doorGridZ, TileType.DoorOpen);
                }
                else
                {
                    return;
                }
            }

            // Calcula a posição e coordenadas do piso APÓS a porta
            Vector3 destinationPos = doorPos + direction * tileSize;
            int destGridX = Mathf.RoundToInt(destinationPos.x / tileSize);
            int destGridZ = Mathf.RoundToInt(destinationPos.z / tileSize);

            // Se o espaço após a porta estiver livre, atravessa em 2 passos
            if (dungeonGrid.IsWalkable(destGridX, destGridZ))
            {
                gridX = destGridX;
                gridZ = destGridZ;
                StartCoroutine(MoveThroughDoor(doorPos, destinationPos));
            }
            else
            {
                // Caso o espaço atrás da porta esteja bloqueado, anda apenas até a porta
                gridX = doorGridX;
                gridZ = doorGridZ;
                StartCoroutine(MoveTo(doorPos));
            }
            return;
        }
        
        // Movimento normal de 1 passo para pisos comuns
        if (dungeonGrid.IsWalkable(doorGridX, doorGridZ))
        {
            gridX = doorGridX;
            gridZ = doorGridZ;

            if (targetTile == TileType.StairsDown ||
                targetTile == TileType.StairsUp)
            {
                StartCoroutine(MoveToStairs(doorPos, targetTile));
                return;
            }

            StartCoroutine(MoveTo(doorPos));
        }
    }

    IEnumerator MoveToStairs(Vector3 targetPosition, TileType stairTile)
    {
        yield return StartCoroutine(MoveTo(targetPosition));

        if (isChangingFloor)
            yield break;

        gridX = Mathf.RoundToInt(transform.position.x / tileSize);
        gridZ = Mathf.RoundToInt(transform.position.z / tileSize);

        Debug.Log(
            $"[Stairs] Chegou em {stairTile} na grade=({gridX},{gridZ}) Andar={dungeonGrid.currentFloorIndex}",
            this);

        /*isChangingFloor = true;

        if (stairTile == TileType.StairsDown)
            dungeonGrid.ChangeFloor(dungeonGrid.currentFloorIndex + 1);
        else
            dungeonGrid.ChangeFloor(dungeonGrid.currentFloorIndex - 1);
        */
        isChangingFloor = false;
    }

    // Corrotina para mover o jogador suavemente através da porta até a sala seguinte
    IEnumerator MoveThroughDoor(Vector3 doorPos, Vector3 destinationPos)
    {
        isMoving = true;

        if (instantTileMovement)
        {
            transform.position = destinationPos;
            gridX = Mathf.RoundToInt(destinationPos.x / tileSize);
            gridZ = Mathf.RoundToInt(destinationPos.z / tileSize);
            isMoving = false;

            CheckForItems();
            CheckSpecialTiles();
            DebugPlayerPosition();
            yield break;
        }

        // Etapa 1: Mover até o vão da porta
        while (Vector3.Distance(transform.position, doorPos) > 0.001f)
        {
            transform.position = Vector3.MoveTowards(transform.position, doorPos, moveSpeed * Time.deltaTime);
            yield return null;
        }
        transform.position = doorPos;

        // Etapa 2: Continuar o movimento até o piso após a porta
        while (Vector3.Distance(transform.position, destinationPos) > 0.001f)
        {
            transform.position = Vector3.MoveTowards(transform.position, destinationPos, moveSpeed * Time.deltaTime);
            yield return null;
        }
        transform.position = destinationPos;

        isMoving = false;
        CheckForItems();

        CheckSpecialTiles();
    }

    /*void CheckSpecialTilesOld()
    {
        TileType currentTile = dungeonGrid.GetTileType(gridX, gridZ);

        switch (currentTile)
        {
            case TileType.StairsDown:
                Debug.Log("Encontrou escada para descer! Mudando de andar...");
                dungeonGrid.ChangeFloor(dungeonGrid.currentFloorIndex + 1);
                break;

            case TileType.StairsUp:
                Debug.Log("Encontrou escada para subir!");
                dungeonGrid.ChangeFloor(dungeonGrid.currentFloorIndex - 1);
                break;

            case TileType.Trap:
                Debug.Log("Pisou numa armadilha! Sofre dano.");
                break;
        }
    }*/

    void CheckForItems()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, 0.4f);
        foreach (var col in hitColliders)
        {
            KeyItem key = col.GetComponent<KeyItem>();
            if (key != null)
            {
                if (inventory != null)
                {
                    inventory.CollectKey();
                }
                Destroy(col.gameObject); // Remove a chave da cena
                break;
            }
        }
    }

    // Método auxiliar para localizar a porta ajustando a altura do raio de busca
    DoorInteractable GetDoorAtPosition(Vector3 position)
    {
        // Eleva o centro da busca para o meio do bloco (Y = tileSize / 2)
        Vector3 checkCenter = new Vector3(position.x, tileSize / 2f, position.z);
        Collider[] hitColliders = Physics.OverlapSphere(checkCenter, 0.8f, Physics.AllLayers, QueryTriggerInteraction.Collide);

        foreach (var col in hitColliders)
        {
            DoorInteractable door = col.GetComponentInParent<DoorInteractable>();
            if (door != null) return door;
        }
        return null;
    }


    void CheckSpecialTiles()
    {
        if (isChangingFloor)
            return;

        gridX = Mathf.RoundToInt(transform.position.x / tileSize);
        gridZ = Mathf.RoundToInt(transform.position.z / tileSize);

        dungeonGrid.ActivateDragonAdjacentTo(gridX, gridZ);

        TileType currentTile = dungeonGrid.GetTileType(gridX, gridZ);
        
        switch (currentTile)
        {
            case TileType.StairsUp:
                // Bloqueia subir a escada se o movimento foi de costas
                if (lastMoveWasBackward)
                {
                    if (DialogueManager.Instance != null)
                    {
                        DialogueManager.Instance.ShowMessage("Atenção", "Você não pode subir a escada andando de costas. Vire-se de frente!");
                    }
                }
                else
                {
                    Debug.Log($"Subindo do andar {dungeonGrid.currentFloorIndex} para {dungeonGrid.currentFloorIndex - 1}.", this);
                    dungeonGrid.ChangeFloor(dungeonGrid.currentFloorIndex - 1); // Sobe de andar (ChangeFloor já orienta o jogador de costas para a escada)
                }
                break;

            case TileType.StairsDown:
                Debug.Log($"Descendo do andar {dungeonGrid.currentFloorIndex} para {dungeonGrid.currentFloorIndex + 1}.", this);
                dungeonGrid.ChangeFloor(dungeonGrid.currentFloorIndex + 1);
                break;

            case TileType.Trap:
                Debug.Log("Pisou numa armadilha!");
                break;

            case TileType.EndGame:                
                CountdownTimer countdownTimer = FindObjectOfType<CountdownTimer>();
                if (countdownTimer != null)
                {
                    countdownTimer.EndGame();
                }
                else
                {
                    Debug.LogError("CountdownTimer não foi encontrado ao chegar ao tile de fim de jogo.", this);
                }
                break;
        }
    }

    /// <summary>
    /// Rotaciona o jogador para olhar na direção oposta à escada (de costas para ela).
    /// </summary>
    public void FaceAwayFrom(Vector2Int stairGridPos)
    {
        Vector3 awayDir = new Vector3(gridX - stairGridPos.x, 0f, gridZ - stairGridPos.y);
        if (awayDir.sqrMagnitude < 0.0001f)
        {
            AlignWithCorridor();
            return;
        }

        transform.rotation = Quaternion.LookRotation(awayDir.normalized);
        RecenterVRView();
    }

    // Após teleporte/rotação por código, faz o olhar do headset coincidir com a frente do Player
    void RecenterVRView()
    {
        if (vrTileRig == null) vrTileRig = GetComponent<VRTileRig>();
        vrTileRig?.RecenterView();
    }

    /// <summary>
    /// Verifica as 4 direções ao redor do jogador e rotaciona-o para encarar o corredor livre.
    /// </summary>
    public void AlignWithCorridor()
    {
        // 4 Direções Cardinais na grelha: Norte, Leste, Sul, Oeste
        Vector2Int[] directions = new Vector2Int[]
        {
            new Vector2Int(0, 1),   // Norte (+Z)
            new Vector2Int(1, 0),   // Leste (+X)
            new Vector2Int(0, -1),  // Sul (-Z)
            new Vector2Int(-1, 0)   // Oeste (-X)
        };

        foreach (var dir in directions)
        {
            int checkX = gridX + dir.x;
            int checkZ = gridZ + dir.y;

            // Se o piso adjacente for livre para caminhar (chão/porta aberta) e NÃO for outra escada
            TileType adjTile = dungeonGrid.GetTileType(checkX, checkZ);
            if (dungeonGrid.IsWalkable(checkX, checkZ) && adjTile != TileType.StairsUp && adjTile != TileType.StairsDown)
            {
                // Calcula a direção em 3D para onde o jogador deve olhar
                Vector3 targetForward = new Vector3(dir.x, 0, dir.y);

                // Aplica a rotação instantânea para alinhar o olhar do jogador
                transform.rotation = Quaternion.LookRotation(targetForward);
                RecenterVRView();
                break; // Encontrou o primeiro caminho livre do corredor
            }
        }
    }
	
	
}