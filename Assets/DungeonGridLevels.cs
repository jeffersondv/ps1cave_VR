using System.Collections.Generic;
using UnityEngine;

public enum TileTypeOld
{
    Floor = 0,
    Wall = 1,
    StairsUp = 2,
    StairsDown = 3,
    Pit = 4,
    Trap = 5
}

public class DungeonGridLevels : MonoBehaviour
{
    [System.Serializable]
    public class FloorData
    {
        public string floorName = "Andar 1";
        // Definimos o mapa de cada andar aqui
        public TileType[,] map;
    }

    [Header("Prefabs dos Blocos")]
    public GameObject wallPrefab;
    public GameObject floorPrefab;
    public GameObject ceilingPrefab;
    public GameObject stairsUpPrefab;
    public GameObject stairsDownPrefab;
    public GameObject trapPrefab;

    [Header("Configurações")]
    public float tileSize = 2.0f;
    public int currentFloorIndex = 0;

    private List<TileType[,]> floors = new List<TileType[,]>();

    void Awake()
    {
        InitializeFloors();
        GenerateCurrentFloor();
    }

    void InitializeFloors()
    {
        // Andar 0 (Superfície / Entrada)
        TileType[,] floor0 = new TileType[,]
        {
            { TileType.Wall, TileType.Wall,     TileType.Wall,       TileType.Wall },
            { TileType.Wall, TileType.Floor,    TileType.StairsDown, TileType.Wall },
            { TileType.Wall, TileType.Floor,    TileType.Floor,      TileType.Wall },
            { TileType.Wall, TileType.Wall,     TileType.Wall,       TileType.Wall }
        };

        // Andar 1 (Subsolo com Armadilhas)
        TileType[,] floor1 = new TileType[,]
        {
            { TileType.Wall, TileType.Wall,     TileType.Wall,       TileType.Wall },
            { TileType.Wall, TileType.StairsUp, TileType.Trap,       TileType.Wall },
            { TileType.Wall, TileType.Pit,      TileType.Floor,      TileType.Wall },
            { TileType.Wall, TileType.Wall,     TileType.Wall,       TileType.Wall }
        };

        floors.Add(floor0);
        floors.Add(floor1);
    }

    public void GenerateCurrentFloor()
    {
        // Limpa blocos antigos se estiver a mudar de andar
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

                // Sempre instancia o chão base (exceto se for um buraco/pit)
                if (tile != TileType.Pit && floorPrefab != null)
                {
                    Instantiate(floorPrefab, position, Quaternion.identity, transform);
                }

                // Instancia elementos específicos
                switch (tile)
                {
                    case TileType.Wall:
                        if (wallPrefab != null)
                            Instantiate(wallPrefab, position + Vector3.up * (tileSize / 2f), Quaternion.identity, transform);
                        break;

                    case TileType.StairsDown:
                        if (stairsDownPrefab != null)
                            Instantiate(stairsDownPrefab, position, Quaternion.identity, transform);
                        break;

                    case TileType.StairsUp:
                        if (stairsUpPrefab != null)
                            Instantiate(stairsUpPrefab, position, Quaternion.identity, transform);
                        break;

                    case TileType.Trap:
                        if (trapPrefab != null)
                            Instantiate(trapPrefab, position, Quaternion.identity, transform);
                        break;
                }
            }
        }
    }

    // Retorna o tipo de tile numa coordenada específica
    public TileType GetTileType(int gridX, int gridZ)
    {
        TileType[,] map = floors[currentFloorIndex];
        int height = map.GetLength(0);
        int width = map.GetLength(1);

        if (gridX < 0 || gridX >= width || gridZ < 0 || gridZ >= height)
            return TileType.Wall;

        return map[gridZ, gridX];
    }

    public bool IsWalkable(int gridX, int gridZ)
    {
        TileType type = GetTileType(gridX, gridZ);
        // Não é possível andar em paredes ou buracos sem fundo
        return type != TileType.Wall && type != TileType.Pit;
    }

    // Função para carregar outro andar
    public void ChangeFloor(int newFloorIndex)
    {
        if (newFloorIndex >= 0 && newFloorIndex < floors.Count)
        {
            currentFloorIndex = newFloorIndex;
            GenerateCurrentFloor();
        }
    }
}