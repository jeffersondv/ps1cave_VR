using System.Collections.Generic;
using UnityEngine;

public class DungeonGrid : MonoBehaviour
{
    [System.Serializable]
    public struct NPCSpawn
    {
        public string name;
        public int gridX;
        public int gridZ;
        public GameObject npcPrefab;
    }

    [Header("Prefabs dos Blocos")]
    public GameObject wallPrefab;
    public GameObject floorPrefab;
    public GameObject ceilingPrefab;
    public GameObject meuPrefabEstatua;

    [Header("Lista de NPCs / Enigmas no Grid")]
    public List<NPCSpawn> npcList;

    [Header("Configurações da Grade")]
    public float tileSize = 2.0f;

    // Matriz 2D da Dungeon (1 = Parede, 0 = Caminho)
    private int[,] map = new int[,]
    {
        {1, 1, 1, 1, 1, 1, 1, 1},
        {1, 0, 0, 0, 1, 0, 0, 1},
        {1, 0, 1, 0, 1, 0, 1, 1},
        {1, 0, 1, 0, 0, 0, 0, 1},
        {1, 0, 1, 1, 1, 1, 0, 1},
        {1, 0, 0, 0, 0, 1, 0, 1},
        {1, 1, 1, 1, 1, 1, 1, 1}
    };

    void Awake()
    {
		
		// Garante que a lista está inicializada
		if (npcList == null)
			npcList = new List<NPCSpawn>();

		// Adiciona um novo NPC manualmente por código
		NPCSpawn estatua;
		estatua.name = "Estátua do Guardião";
		estatua.gridX = 3;
		estatua.gridZ = 1;
		estatua.npcPrefab = meuPrefabEstatua; // Referência para o GameObject/Prefab

		npcList.Add(estatua);
		
		
        GenerateDungeon();
        SpawnNPCs();
    }

    void GenerateDungeon()
    {
        int height = map.GetLength(0);
        int width = map.GetLength(1);

        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector3 position = new Vector3(x * tileSize, 0, z * tileSize);

                if (map[z, x] == 1)
                {
                    if (wallPrefab != null)
                        Instantiate(wallPrefab, position + Vector3.up * (tileSize / 2f), Quaternion.identity, transform);
                }
                else
                {
                    if (floorPrefab != null)
                        Instantiate(floorPrefab, position, Quaternion.identity, transform);

                    if (ceilingPrefab != null)
                        Instantiate(ceilingPrefab, position + Vector3.up * tileSize, Quaternion.Euler(180, 0, 0), transform);
                }
            }
        }
    }

    void SpawnNPCs()
    {
        foreach (var npc in npcList)
        {
            if (npc.npcPrefab != null)
            {
                Vector3 position = new Vector3(npc.gridX * tileSize, tileSize / 2f, npc.gridZ * tileSize);
                Instantiate(npc.npcPrefab, position, Quaternion.identity, transform);
            }
        }
    }

    public bool IsWalkable(int gridX, int gridZ)
    {
        int height = map.GetLength(0);
        int width = map.GetLength(1);

        if (gridX < 0 || gridX >= width || gridZ < 0 || gridZ >= height)
            return false;

        return map[gridZ, gridX] == 0;
    }
}