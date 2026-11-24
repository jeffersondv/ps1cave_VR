using System.Collections.Generic;
using UnityEngine;
using static TileType;

public enum TileType
{
    Floor = 0,
    Wall = 1,
    StairsUp = 2,
    StairsDown = 3,
    Pit = 4,
    Trap = 5,
    DoorNormal = 6, // Porta Normal
    DoorKey = 7,    // Porta com Chave
    DoorMagic = 8,  // Porta Mágica
    DoorOpen = 9,    // Porta Aberta (passável)
    DoorHidden = 10,    // Porta Aberta Oculta (passável)
    Key = 11,
    NPC = 51,
    NPC2 = 52,
    NPC3 = 53,
    NPC4 = 54,
    NPC5 = 55,
    NPCChest = 59,
    NPCQuestion = 60,
    Dragon = 61,
    NPCCristal = 62, // Tile especial para o NPC com cristal

    EndGame = 99, // Tile especial para indicar o fim do jogo

    WallButton = 11
}


public class DungeonGridLevelsDoors : MonoBehaviour
{
    [Header("Prefabs dos Blocos")]
    public GameObject wallPrefab;
    public GameObject floorPrefab;
    public GameObject ceilingPrefab;
    public GameObject stairsUpPrefab;
    public GameObject stairsDownPrefab;
    public GameObject trapPrefab;

    public GameObject wallButtonPrefab;

    [Header("Prefabs das Portas")]
    public GameObject doorNormalPrefab;
    public GameObject doorKeyPrefab;
    public GameObject doorMagicPrefab;
    public GameObject doorHiddenPrefab;


    [Header("Prefabs de Itens")]
    public GameObject keyPrefab;
    
    [Header("Prefabs de NPCs")]
    public GameObject npc1Prefab;
    public GameObject npc2Prefab;
    public GameObject npc3Prefab;
    public GameObject npc4Prefab;
    public GameObject npc5Prefab;
    public GameObject chestPrefab; // Prefab do baú
    public GameObject npc2Chest; // Prefab do NPC que aparece junto ao baú
    public GameObject npcQuestionPrefab; // Prefab do NPC com pergunta
    public GameObject npcCristalPrefab; // Prefab do NPC com cristal

    [Header("Prefabs de Inimigos")]
    public GameObject DragonPrefab;

    [Header("Configurações")]
    public float tileSize = 2.0f;
    public int currentFloorIndex = 0;

    public float npcYoffset = 0.7f;

    public GameObject RestartButtonPrefab; // Prefab do botão de reinício

    [Header("Referências")]
    public PlayerController player; // <--- Adicione esta linha

    private List<TileType[,]> floors = new List<TileType[,]>();

    // Mapeia coordenadas (X, Z) para o objeto interativo correspondente
    private Dictionary<Vector2Int, GameObject> gridObjects = new Dictionary<Vector2Int, GameObject>();

    public void RegisterObject(int x, int z, GameObject obj)
    {
        Vector2Int pos = new Vector2Int(x, z);
        gridObjects[pos] = obj;
    }

    public GameObject GetObjectAt(int x, int z)
    {
        Vector2Int pos = new Vector2Int(x, z);
        if (gridObjects.TryGetValue(pos, out GameObject obj))
            return obj;
        return null;
    }

    public void ActivateDragonAdjacentTo(int gridX, int gridZ)
    {
        Vector2Int[] directions =
        {
            new Vector2Int(0, 1),
            new Vector2Int(1, 0),
            new Vector2Int(0, -1),
            new Vector2Int(-1, 0)
        };

        foreach (Vector2Int direction in directions)
        {
            int x = gridX + direction.x;
            int z = gridZ + direction.y;

            if (GetTileType(x, z) != TileType.Dragon)
                continue;

            GameObject dragon = GetObjectAt(x, z);
            if (dragon != null)
            {
                dragon.SetActive(true);
                player.FreezeMovement(); // Congela os movimentos do jogador
                player.SetOllamaPromptFrom(dragon);
                CountdownTimer countdownTimer = FindObjectOfType<CountdownTimer>();
                if (countdownTimer != null)
                {
                    countdownTimer.stopSeamlessSound();
                }
                dragon.GetComponent<NPCInteractable>().prefabSound.PlayScheduled(AudioSettings.dspTime + 0.1); // Toca o som do dragão
            }
        }
    }

    void Awake()
    {
        InitializeFloors();
        GenerateCurrentFloor();
    }

    void InitializeFloors()
    {
        // Exemplo de mapa com as 3 portas:
        // 6 = Normal, 7 = Chave, 8 = Mágica

        TileType[,] hall = new TileType[,]
        {
            { Wall,     Wall,          Wall,    Wall,     Wall,          Wall,    Wall,  Wall },
            { Wall,     Wall,          Wall,    Wall,     StairsDown,    Wall,    Wall,  Wall },
            { Wall,     Wall,          Wall,    Wall,     DoorHidden,    Wall,    Wall,  Wall },
            { Wall,     NPCQuestion,   Floor,   Floor,    Floor,         Floor,   Wall,  Wall },            
            { Wall,     Wall,          Wall,    Wall,     Wall,          Wall,    Wall,  Wall }    
        };

        floors.Add(hall);

        TileType[,] floor0 = new TileType[,]
        {
            { TileType.Wall,    TileType.Wall,  TileType.Wall,      TileType.Wall,       TileType.Wall,         TileType.Wall,  TileType.Wall,       TileType.Wall },
            { TileType.Wall,    TileType.NPC5,  TileType.Floor,     TileType.DoorNormal,   TileType.Floor,      TileType.Floor,      TileType.Wall,  TileType.Wall },
            { TileType.Wall,    TileType.Wall,  TileType.Wall,      TileType.Wall,       TileType.Floor,        TileType.Wall,  TileType.Wall,       TileType.Wall },
            { TileType.Wall,    TileType.Wall,  TileType.EndGame,   TileType.DoorMagic,  TileType.Floor,        TileType.Floor, TileType.StairsDown, TileType.Wall },
            { TileType.Wall,    TileType.Wall,  TileType.Wall,      TileType.Wall,       TileType.Wall,         TileType.Wall,  TileType.Wall,       TileType.Wall }
        };

        floors.Add(floor0);

        TileType[,] floor1 = new TileType[,]
        {
            { Wall, Wall,       Wall,       StairsDown,     Wall,   Wall,       Wall },
            { Wall, Floor,      Floor,      Floor,          Floor,  StairsUp,   Wall },
            { Wall, Floor,      Wall,       Wall,           Wall,   Wall,       Wall },
            { Wall, Floor,      DoorNormal, Floor,          Floor,  NPC2,       Wall },
            { Wall, Wall,       Wall,       Wall,           Wall,   Wall,       Wall }
        };

        floors.Add(floor1);

        TileType[,] floor2 = new TileType[,]
        {
            { Wall, StairsUp,   Wall,       Wall,           Wall,       Wall,           Wall,      Wall },
            { Wall, Floor,      Wall,       NPCChest,       Wall,       Floor,          Wall,      Wall },
            { Wall, Floor,      Wall,       Floor,          Wall,       NPC4,           Wall,      Wall },
            { Wall, Floor,      Wall,       Floor,          Wall,       Floor,          Wall,      Wall },
            { Wall, Floor,      Wall,       DoorNormal,     Wall,       DoorNormal,     Wall,      Wall },
            { Wall, Floor,      Floor,      Floor,          Floor,      Floor,          StairsDown,  Wall },
            { Wall, Wall,       Wall,       DoorNormal,           Wall,       DoorNormal,     Wall,      Wall },
            { Wall, Wall,       Wall,       Wall,           Wall,       Floor,          Wall,      Wall },
            { Wall, Wall,       Wall,       Wall,           Wall,       Floor,          Wall,      Wall },
            { Wall, Wall,       Wall,       NPC3,           Wall,       NPC,            Wall,      Wall },
            { Wall, Wall,       Wall,       Wall,           Wall,       Wall,           Wall,      Wall }

        };

        floors.Add(floor2);

        TileType[,] floor3 = new TileType[,]
        {
            { Wall, StairsUp,   Wall,       Wall,           Wall,       Wall,           Wall,       Wall,  Wall  },
            { Wall, Floor,      Wall,       Dragon,         Floor,      Floor,          Floor,       Floor, Wall },
            { Wall, Floor,      Wall,       Wall,           Wall,       Wall,           Wall,       Floor, Wall  },
            { Wall, Floor,      Wall,       Wall,           Wall,       Wall,           Wall,       Floor, Wall  },
            { Wall, Floor,      Wall,       Wall,           NPCCristal, Floor,    DoorNormal,       Floor, Wall  },
            { Wall, Floor,      Wall,       Wall,           Wall,       Wall,           Wall,       Floor, Wall  },
            { Wall, Floor,      Wall,       Wall,           Wall,       Wall,           Wall,       Floor, Wall  },
            { Wall, Floor,      Wall,       Wall,           Wall,       Wall,           Wall,       Floor, Wall  },
            { Wall, Floor,      Wall,       Wall,           Wall,       Wall,           Wall,       Floor, Wall  },
            { Wall, Floor,      Floor,      Floor,          DoorKey,    Floor,          Floor,      Floor, Wall  },
            { Wall, Wall,       Wall,       Wall,           Wall,       Wall,           Wall,       Wall,  Wall  }

        };

        floors.Add(floor3);


    }

    public void GenerateCurrentFloor()
    {
        // Limpa os objetos do andar anterior
        gridObjects.Clear();
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }

        TileType[,] map = floors[currentFloorIndex];
        int height = map.GetLength(0);
        int width = map.GetLength(1);

        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector3 position = new Vector3(x * tileSize, 0, z * tileSize);
                TileType tile = map[z, x];

                // Instancia o chão se não for um buraco/poço
                if (tile != TileType.Pit && floorPrefab != null)
                {
                    Instantiate(floorPrefab, position, Quaternion.identity, transform);
                    
                    if (ceilingPrefab != null)
                        Instantiate(ceilingPrefab, position + Vector3.up * tileSize, Quaternion.Euler(180, 0, 0), transform);
                }

                if (tile == TileType.NPC && npc1Prefab != null )
                {
                    //position = position + Vector3.up * 1.3f;
                    Quaternion npcRotation = CalculateDoorRotation(map, x, z);
                    //Instantiate(npc1Prefab, position, npcRotation, transform);
                    GameObject npc = SpawnDynamicPrefab(npc1Prefab, x, z, npcYoffset, npcRotation);
                    ConfigureSpawnedNPC(npc, x, z);
                }else
                if (tile == TileType.NPC2 && npc2Prefab != null )
                {
                    //position = position + Vector3.up * 1.3f;
                    Quaternion npcRotation = CalculateDoorRotation(map, x, z);
                    //Instantiate(npc1Prefab, position, npcRotation, transform);
                    GameObject npc = SpawnDynamicPrefab(npc2Prefab, x, z, npcYoffset, npcRotation);
                    ConfigureSpawnedNPC(npc, x, z);
                }else if (tile == TileType.NPC3 && npc3Prefab != null)
                {
                    //position = position + Vector3.up * 1.3f;
                    Quaternion npcRotation = CalculateDoorRotation(map, x, z);
                    //Instantiate(npc1Prefab, position, npcRotation, transform);
                    GameObject npc = SpawnDynamicPrefab(npc3Prefab, x, z, npcYoffset, npcRotation);
                    ConfigureSpawnedNPC(npc, x, z);
                }else if (tile == TileType.NPC4 && npc4Prefab != null)
                {
                    //position = position + Vector3.up * 1.3f;
                    Quaternion npcRotation = CalculateDoorRotation(map, x, z);
                    //Instantiate(npc1Prefab, position, npcRotation, transform);
                    GameObject npc = SpawnDynamicPrefab(npc4Prefab, x, z, npcYoffset, npcRotation);
                    ConfigureSpawnedNPC(npc, x, z);
                }else if (tile == TileType.NPCChest )
                {
                    //position = position + Vector3.up * 1.3f;
                    Quaternion npcRotation = CalculateDoorRotation(map, x, z);
                    //Instantiate(npc1Prefab, position, npcRotation, transform);
                    
                    GameObject InstanceNPC = SpawnDynamicPrefab(npc2Chest, x, z, npcYoffset, npcRotation);
                    GameObject InstanceChest = SpawnDynamicPrefab(chestPrefab, x, z, npcYoffset, npcRotation);
                    InstanceNPC.GetComponent<NPCInteractable>().SetChestChild(InstanceChest); // Associa o prefab do baú ao NPC
                    ConfigureSpawnedNPC(InstanceNPC, x, z);
                }else if (tile == TileType.NPCQuestion)
                {
                    //position = position + Vector3.up * 0.5f; // Ajuste a altura conforme necessário
                    Quaternion npcRotation = CalculateDoorRotation(map, x, z);
                    GameObject npc = SpawnDynamicPrefab(npcQuestionPrefab, x, z, 0.5f, npcRotation);
                    ConfigureSpawnedNPC(npc, x, z);
                }else if (tile == TileType.Dragon && DragonPrefab != null)
                {
                    //position = position + Vector3.up * 0.5f; // Ajuste a altura conforme necessário
                    Quaternion dragonRotation = CalculateDoorRotation(map, x, z);
                    GameObject dragon = SpawnDynamicPrefab(DragonPrefab, x, z, 1f, dragonRotation);
                    RegisterObject(x, z, dragon);
                    dragon.SetActive(false); // Certifica-se de que o dragão está escondido inicialmente
                }else if (tile == TileType.NPCCristal)
                {
                    //position = position + Vector3.up * 0.5f; // Ajuste a altura conforme necessário
                    
                    Quaternion npcRotation = CalculateDoorRotation(map, x, z);
                    GameObject npc = SpawnDynamicPrefab(npcCristalPrefab, x, z, npcYoffset, npcRotation);
                    GameObject cristal = npc.GetComponent<NPCInteractable>().GetChestChild(); // Obtém a referência ao baú do NPC
                    GameObject InstanceChest = SpawnDynamicPrefab(cristal, x, z, npcYoffset, npcRotation);
                    npc.GetComponent<NPCInteractable>().SetChestChild(InstanceChest); // Associa o prefab do baú ao NPC
                    ConfigureSpawnedNPC(npc, x, z);
                }else if (tile == TileType.NPC5 && npc5Prefab != null)
                {
                    //position = position + Vector3.up * 1.3f;
                    Quaternion npcRotation = CalculateDoorRotation(map, x, z);
                    //Instantiate(npc1Prefab, position, npcRotation, transform);
                    GameObject npc = SpawnDynamicPrefab(npc5Prefab, x, z, npcYoffset, npcRotation);
                    ConfigureSpawnedNPC(npc, x, z);
                }

                Vector3 objectPos = position + Vector3.up * (tileSize / 2f);

                // INCLUSÃO DO TileType.DoorOpen NA VERIFICAÇÃO:
                if (tile == TileType.DoorNormal || tile == TileType.DoorKey || 
                    tile == TileType.DoorMagic || tile == TileType.DoorOpen || tile == TileType.DoorHidden)
                {
                    Quaternion doorRotation = CalculateDoorRotation(map, x, z);

                    if (tile == TileType.DoorHidden)
                        objectPos.z += 1f; // Ajuste para encostar na parede

                    // Se o tile for DoorOpen, usa o prefab de porta normal como base para a malha
                    GameObject doorPrefabToSpawn = (tile == TileType.DoorOpen) ? doorNormalPrefab : GetDoorPrefab(tile);

                    if (doorPrefabToSpawn != null)
                    {
                        GameObject spawnedDoor = Instantiate(doorPrefabToSpawn, objectPos, doorRotation, transform);

                        // Se a porta já estava aberta na matriz, força a execução do Open() no instante em que nasce
                        if (tile == TileType.DoorOpen)
                        {
                            DoorInteractable doorScript = spawnedDoor.GetComponent<DoorInteractable>();
                            if (doorScript != null)
                            {
                                doorScript.Open(); // Aplica o novo material, desativa colisor e roda a porta
                            }
                        }
                    }
                }else
                if(tile == TileType.StairsDown || tile == TileType.StairsUp)
                {
                    // Calcula a rotação correta com base nas paredes vizinhas
                    Quaternion doorRotation = CalculateDoorRotation(map, x, z);
                    GameObject stairPrefabToSpawn =  ( tile == TileType.StairsDown ? stairsDownPrefab : stairsUpPrefab);
                    
                    if (stairPrefabToSpawn != null)
                    {
                        Instantiate(stairPrefabToSpawn, objectPos, doorRotation, transform);
                    }
                }
                else if (tile == TileType.Wall && wallPrefab != null)
                {
                    Instantiate(wallPrefab, objectPos, Quaternion.identity, transform);
                }else if (tile == TileType.WallButton)
                {
                    // Instancia o botão na parede
                    if (wallButtonPrefab != null)
                    {
                        Instantiate(wallButtonPrefab, objectPos, Quaternion.identity, transform);
                    }

                    //wallButtonPrefab.GetComponent<Collider>().material = Material.npc; // Apenas para diferenciar visualmente

                    wallButtonPrefab.GetComponent<Renderer>().material.color = Color.red;

                    // Instancia o botão interativo
                    if (RestartButtonPrefab != null)
                    {                        
                        //RestartButtonPrefab.SetActive(true); // Certifica-se de que o botão está ativo
                    }
                }


                /*// Dentro do loop de geração em GenerateCurrentFloor():
                if (tile == TileType.StairsUp || tile == TileType.StairsDown)
                {
                    // Calcula a rotação e o deslocamento em direção à parede
                    GetStairsTransform(map, x, z, out Quaternion stairRotation, out Vector3 stairOffset);

                    // Define qual prefab instanciar
                    GameObject prefabToSpawn = (tile == TileType.StairsUp) ? stairsUpPrefab : stairsDownPrefab;

                    if (prefabToSpawn != null)
                    {
                        // Posição base do piso + elevação vertical + deslocamento para a parede
                        Vector3 stairPosition = position + (Vector3.up * (tileSize / 2f)) + stairOffset;

                        Instantiate(prefabToSpawn, stairPosition, stairRotation, transform);
                    }
                }*/



            }
        }
    }
    
    // Calcula se a porta deve ficar virada para a frente (0°) ou rodada (90°)
    Quaternion CalculateDoorRotation(TileType[,] map, int x, int z)
    {
        int height = map.GetLength(0);
        int width = map.GetLength(1);

        // Verifica se existem paredes à esquerda e à direita
        bool wallLeft = (x > 0) && (map[z, x - 1] == TileType.Wall);
        bool wallRight = (x < width - 1) && (map[z, x + 1] == TileType.Wall);

        float yAngle = 0f;

        // Se NÃO tiver paredes na esquerda/direita, o corredor é vertical (Norte-Sul) -> Roda 90 graus
        if (!(wallLeft && wallRight))
        {
            yAngle = 90f;
        }

        // Se o seu mesh de porta original precisa de estar de pé (ex: -90 no X por ser um Plane):
        // Ajuste o X para -90f. Se o seu prefab já nasceu em pé, use 0f no X.
        float xAngle = 90f; // Mude para 0f se a porta não precisar de ser levantada no eixo X

        return Quaternion.Euler(xAngle, yAngle, 0f);
    }

    GameObject GetDoorPrefab(TileType type)
    {
        switch (type)
        {
            case TileType.DoorNormal: return doorNormalPrefab;
            case TileType.DoorKey: return doorKeyPrefab;
            case TileType.DoorMagic: return doorMagicPrefab;     
            case TileType.DoorHidden: return doorHiddenPrefab;

            default: return null;
        }
    }

    public TileType GetTileType(int gridX, int gridZ)
    {
        TileType[,] map = floors[currentFloorIndex];
        int height = map.GetLength(0);
        int width = map.GetLength(1);

        if (gridX < 0 || gridX >= width || gridZ < 0 || gridZ >= height)
            return TileType.Wall;

        return map[gridZ, gridX];
    }

    // Permite mudar o tipo de bloco no mapa em tempo de execução
    public void SetTileType(int gridX, int gridZ, TileType type)
    {
        floors[currentFloorIndex][gridZ, gridX] = type;
    }

    public void ShowRestartButton()
    {
        if (RestartButtonPrefab != null)
        {
            RestartButtonPrefab.SetActive(true);
        }
    }

    public void RemoveNPCFromMap(int floorIndex, int gridX, int gridZ)
    {
        if (floorIndex < 0 || floorIndex >= floors.Count)
        {
            Debug.LogError($"Andar inválido ao remover NPC: {floorIndex}.");
            return;
        }

        TileType[,] map = floors[floorIndex];
        if (gridX < 0 || gridX >= map.GetLength(1) || gridZ < 0 || gridZ >= map.GetLength(0))
        {
            Debug.LogError($"Coordenadas inválidas ao remover NPC: ({gridX}, {gridZ}).");
            return;
        }

        TileType tile = map[gridZ, gridX];
        if (tile == TileType.NPC || tile == TileType.NPC2 || tile == TileType.NPC3 ||
            tile == TileType.NPC4 || tile == TileType.NPCChest || tile == TileType.NPCQuestion || tile == TileType.NPCCristal )
        {
            map[gridZ, gridX] = TileType.Floor;
        }
    }

    private void ConfigureSpawnedNPC(GameObject instance, int gridX, int gridZ)
    {
        NPCInteractable npc = instance.GetComponent<NPCInteractable>();
        if (npc == null)
        {
            Debug.LogError($"O prefab {instance.name} não possui o componente NPCInteractable.");
            return;
        }

        npc.SetMapTile(this, currentFloorIndex, gridX, gridZ);
    }

    public bool IsWalkable(int gridX, int gridZ)
    {
        TileType type = GetTileType(gridX, gridZ);

        if (type == TileType.StairsUp || type == TileType.StairsDown)
            return true;
        
        // Bloqueia movimento se for parede, buraco ou qualquer porta fechada
        if (type == TileType.Wall || type == TileType.Pit || 
            type == TileType.DoorNormal || type == TileType.DoorKey || type == TileType.DoorMagic ||
            type == TileType.DoorHidden ||
            type == TileType.NPC || type == TileType.NPC2 || type == TileType.NPC3 || type == TileType.NPC4 || type == TileType.NPC5 || 
            type == TileType.NPCChest || type == TileType.NPCQuestion || type == TileType.NPCCristal ||
            type == TileType.Dragon)
        {
            return false;
        }else 
        if (type == TileType.WallButton)
        {
            return false; // Bloqueia movimento se for um botão na parede
        }

        return true;
    }

    // Função para carregar outro andar
    public void ChangeFloorOld(int newFloorIndex)
    {
        if (newFloorIndex >= 0 && newFloorIndex < floors.Count)
        {
            currentFloorIndex = newFloorIndex;
            GenerateCurrentFloor();
        }
    }

    public GameObject SpawnDynamicPrefab(GameObject prefab, float gridX, float gridZ, float yOffset, Quaternion rotation)
    {
        //[Header("Configurações de Altura")]
        //public float customHeightOffset = 1.0f; // Altura desejada em relação ao chão
        
        // Calcula a posição (X, Z) no grid e adiciona a altura no Y
        Vector3 spawnPosition = new Vector3(
            gridX * tileSize, 
            yOffset, 
            gridZ * tileSize
        );

        GameObject instance = Instantiate(prefab, spawnPosition, rotation, transform);
        return instance;
    }

    // Retorna a rotação e o offset necessários para encostar a escada na parede vizinha
    void GetStairsTransform(TileType[,] map, int x, int z, out Quaternion rotation, out Vector3 offset)
    {
        int height = map.GetLength(0);
        int width = map.GetLength(1);

        // Distância para encostar na borda da parede (ajuste este valor se necessário)
        float edgeOffset = tileSize * 0.25f; 

        // 1. Parede ao Norte (+Z)
        if (z + 1 < height && map[z + 1, x] == TileType.Wall)
        {
            rotation = Quaternion.Euler(0f, 0f, 0f);
            offset = new Vector3(0f, 0f, edgeOffset);
            return;
        }

        // 2. Parede ao Leste (+X)
        if (x + 1 < width && map[z, x + 1] == TileType.Wall)
        {
            rotation = Quaternion.Euler(0f, 90f, 0f);
            offset = new Vector3(edgeOffset, 0f, 0f);
            return;
        }

        // 3. Parede ao Sul (-Z)
        if (z - 1 >= 0 && map[z - 1, x] == TileType.Wall)
        {
            rotation = Quaternion.Euler(0f, 180f, 0f);
            offset = new Vector3(0f, 0f, -edgeOffset);
            return;
        }

        // 4. Parede ao Oeste (-X)
        if (x - 1 >= 0 && map[z, x - 1] == TileType.Wall)
        {
            rotation = Quaternion.Euler(0f, 270f, 0f);
            offset = new Vector3(-edgeOffset, 0f, 0f);
            return;
        }

        // Fallback caso a escada esteja no meio da sala sem parede direta
        rotation = Quaternion.identity;
        offset = Vector3.zero;
    }


    public void ChangeFloor(int newFloorIndex)
    {
        Debug.Log($"[ChangeFloor] Solicitado andar {newFloorIndex} a partir do andar {currentFloorIndex}.", this);

        if (newFloorIndex < 0 || newFloorIndex >= floors.Count)
        {
            Debug.LogError($"Não existe o andar {newFloorIndex}. Índice atual: {currentFloorIndex}.", this);
            return;
        }

        int oldFloorIndex = currentFloorIndex;

        currentFloorIndex = newFloorIndex;
        GenerateCurrentFloor();

        TileType stairOnDestination =
            newFloorIndex > oldFloorIndex ? TileType.StairsUp : TileType.StairsDown;

        Vector2Int stairGridPos = FindStairPositionOnCurrentFloor(stairOnDestination);
        if (stairGridPos.x < 0)
        {
            Debug.LogError(
                $"Não foi encontrada a escada {stairOnDestination} no andar destino {newFloorIndex}.",
                this);
            return;
        }

        Vector2Int landingPos = GetAdjacentWalkableTile(stairGridPos.x, stairGridPos.y);
        if (landingPos == stairGridPos)
        {
            Debug.LogError(
                $"Não existe piso adjacente navegável à escada ({stairGridPos.x}, {stairGridPos.y}) no andar {newFloorIndex}.",
                this);
        }

        if (player == null)
            player = FindFirstObjectByType<PlayerController>();

        if (player == null)
        {
            Debug.LogError("PlayerController não foi encontrado na cena!");
            return;
        }

        player.gridX = landingPos.x;
        player.gridZ = landingPos.y;
        player.transform.position = new Vector3(
            landingPos.x * tileSize,
            tileSize / 2f,
            landingPos.y * tileSize);

        // Ao chegar no novo andar, o jogador fica de costas para a escada
        if (landingPos != stairGridPos)
            player.FaceAwayFrom(stairGridPos);
        else
            player.AlignWithCorridor();
    }

    // Procura o local da escada no novo andar
    Vector2Int FindStairPositionOnCurrentFloor(TileType stairType)
    {
        TileType[,] map = floors[currentFloorIndex];
        int height = map.GetLength(0);
        int width = map.GetLength(1);

        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                if (map[z, x] == stairType)
                {
                    return new Vector2Int(x, z);
                }
            }
        }
        return new Vector2Int(-1, -1);
    }

    // Encontra o piso de saída (o "passo a mais") ao lado da escada
    Vector2Int GetAdjacentWalkableTile(int stairX, int stairZ)
    {
        Vector2Int[] directions = new Vector2Int[]
        {
            new Vector2Int(0, 1),   // Norte
            new Vector2Int(1, 0),   // Leste
            new Vector2Int(0, -1),  // Sul
            new Vector2Int(-1, 0)   // Oeste
        };

        foreach (var dir in directions)
        {
            int checkX = stairX + dir.x;
            int checkZ = stairZ + dir.y;

            // Se for um piso navegável e não for outra escada nem parede, é a saída ideal
            if (IsWalkable(checkX, checkZ) && 
                GetTileType(checkX, checkZ) != TileType.StairsUp && 
                GetTileType(checkX, checkZ) != TileType.StairsDown)
            {
                return new Vector2Int(checkX, checkZ);
            }
        }

        // Fallback: se não achar adjacente, retorna a própria escada
        return new Vector2Int(stairX, stairZ);
    }


}