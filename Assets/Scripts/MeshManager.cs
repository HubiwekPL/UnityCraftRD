using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class MeshManager : MonoBehaviour
{
    public static MeshManager Instance;

    public GameObject player;

    [SerializeField] private int chunkSize = 16;
    [SerializeField] private int chunkBuildsPerFrame = 8;
    [SerializeField] private bool generateColliders = true;

    private readonly Dictionary<Vector3Int, Chunk> chunks =
        new Dictionary<Vector3Int, Chunk>();

    private readonly Dictionary<Material, ushort> materialToId =
        new Dictionary<Material, ushort>();

    private readonly Dictionary<ushort, Material> idToMaterial =
        new Dictionary<ushort, Material>();

    private readonly SortedDictionary<long, Queue<Vector3Int>> rebuildQueue =
        new SortedDictionary<long, Queue<Vector3Int>>();

    private readonly HashSet<Vector3Int> queued =
        new HashSet<Vector3Int>();

    private ushort nextMaterialId = 1;

    private Vector3Int lastPlayerChunk;
    private bool hasPlayerChunk;

    private static readonly Vector3Int[] Directions =
    {
        new Vector3Int(0, 1, 0),
        new Vector3Int(0, -1, 0),
        new Vector3Int(1, 0, 0),
        new Vector3Int(-1, 0, 0),
        new Vector3Int(0, 0, 1),
        new Vector3Int(0, 0, -1)
    };

    private static readonly Vector3[,] FaceVertices =
    {
        {
            new Vector3(0, 1, 0),
            new Vector3(0, 1, 1),
            new Vector3(1, 1, 1),
            new Vector3(1, 1, 0)
        },

        {
            new Vector3(0, 0, 1),
            new Vector3(0, 0, 0),
            new Vector3(1, 0, 0),
            new Vector3(1, 0, 1)
        },

        {
            new Vector3(1, 0, 0),
            new Vector3(1, 1, 0),
            new Vector3(1, 1, 1),
            new Vector3(1, 0, 1)
        },

        {
            new Vector3(0, 0, 1),
            new Vector3(0, 1, 1),
            new Vector3(0, 1, 0),
            new Vector3(0, 0, 0)
        },

        {
            new Vector3(1, 0, 1),
            new Vector3(1, 1, 1),
            new Vector3(0, 1, 1),
            new Vector3(0, 0, 1)
        },

        {
            new Vector3(0, 0, 0),
            new Vector3(0, 1, 0),
            new Vector3(1, 1, 0),
            new Vector3(1, 0, 0)
        }
    };

    private class Chunk
    {
        public Vector3Int coordinate;
        public ushort[] blocks;
        public GameObject gameObject;
        public MeshFilter filter;
        public MeshRenderer renderer;
        public MeshCollider collider;
        public Mesh mesh;
    }

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (player != null)
        {
            lastPlayerChunk = WorldToChunk(
                Vector3Int.FloorToInt(player.transform.position)
            );

            hasPlayerChunk = true;
            RebuildQueueFromPlayer();
        }
    }

    void Update()
    {
        if (player != null)
        {
            Vector3Int currentPlayerChunk =
                WorldToChunk(
                    Vector3Int.FloorToInt(player.transform.position)
                );

            if (!hasPlayerChunk || currentPlayerChunk != lastPlayerChunk)
            {
                lastPlayerChunk = currentPlayerChunk;
                hasPlayerChunk = true;
                RebuildQueueFromPlayer();
            }
        }

        int builds = 0;

        while (builds < chunkBuildsPerFrame && rebuildQueue.Count > 0)
        {
            var enumerator = rebuildQueue.GetEnumerator();

            if (!enumerator.MoveNext())
                break;

            long distance = enumerator.Current.Key;
            Queue<Vector3Int> queue = enumerator.Current.Value;

            Vector3Int coordinate = queue.Dequeue();

            if (queue.Count == 0)
                rebuildQueue.Remove(distance);

            queued.Remove(coordinate);

            if (chunks.TryGetValue(coordinate, out Chunk chunk))
                BuildChunk(chunk);

            builds++;
        }
    }

    public static void Spawn(GameObject prefab, Vector3 position)
    {
        if (Instance == null)
            return;

        Instance.AddBlock(
            prefab,
            Vector3Int.RoundToInt(position)
        );
    }

    public static void Remove(Vector3 position)
    {
        if (Instance == null)
            return;

        Instance.RemoveBlock(
            Vector3Int.RoundToInt(position)
        );
    }

    public static bool Exists(Vector3 position)
    {
        if (Instance == null)
            return false;

        return Instance.GetBlock(
            Vector3Int.RoundToInt(position)
        ) != 0;
    }

    void AddBlock(GameObject prefab, Vector3Int worldPosition)
    {
        MeshRenderer sourceRenderer =
            prefab.GetComponent<MeshRenderer>();

        if (sourceRenderer == null)
            return;

        Material material =
            sourceRenderer.sharedMaterial;

        if (material == null)
            return;

        ushort materialId =
            GetMaterialId(material);

        Vector3Int chunkCoordinate =
            WorldToChunk(worldPosition);

        Vector3Int localPosition =
            WorldToLocal(
                worldPosition,
                chunkCoordinate
            );

        Chunk chunk =
            GetOrCreateChunk(chunkCoordinate);

        int index =
            GetIndex(
                localPosition.x,
                localPosition.y,
                localPosition.z
            );

        if (chunk.blocks[index] == materialId)
            return;

        chunk.blocks[index] = materialId;

        QueueRebuild(chunkCoordinate);

        for (int i = 0; i < Directions.Length; i++)
        {
            Vector3Int neighbor =
                worldPosition + Directions[i];

            Vector3Int neighborChunk =
                WorldToChunk(neighbor);

            if (neighborChunk != chunkCoordinate)
                QueueRebuild(neighborChunk);
        }
    }

    void RemoveBlock(Vector3Int worldPosition)
    {
        Vector3Int chunkCoordinate =
            WorldToChunk(worldPosition);

        if (!chunks.TryGetValue(
            chunkCoordinate,
            out Chunk chunk))
        {
            return;
        }

        Vector3Int localPosition =
            WorldToLocal(
                worldPosition,
                chunkCoordinate
            );

        int index =
            GetIndex(
                localPosition.x,
                localPosition.y,
                localPosition.z
            );

        if (chunk.blocks[index] == 0)
            return;

        chunk.blocks[index] = 0;

        QueueRebuild(chunkCoordinate);

        for (int i = 0; i < Directions.Length; i++)
        {
            Vector3Int neighbor =
                worldPosition + Directions[i];

            Vector3Int neighborChunk =
                WorldToChunk(neighbor);

            if (neighborChunk != chunkCoordinate)
                QueueRebuild(neighborChunk);
        }
    }

    ushort GetBlock(Vector3Int worldPosition)
    {
        Vector3Int chunkCoordinate =
            WorldToChunk(worldPosition);

        if (!chunks.TryGetValue(
            chunkCoordinate,
            out Chunk chunk))
        {
            return 0;
        }

        Vector3Int localPosition =
            WorldToLocal(
                worldPosition,
                chunkCoordinate
            );

        if (
            localPosition.x < 0 ||
            localPosition.y < 0 ||
            localPosition.z < 0 ||
            localPosition.x >= chunkSize ||
            localPosition.y >= chunkSize ||
            localPosition.z >= chunkSize
        )
        {
            return 0;
        }

        return chunk.blocks[
            GetIndex(
                localPosition.x,
                localPosition.y,
                localPosition.z
            )
        ];
    }

    ushort GetMaterialId(Material material)
    {
        if (materialToId.TryGetValue(
            material,
            out ushort id))
        {
            return id;
        }

        id = nextMaterialId++;

        materialToId[material] = id;
        idToMaterial[id] = material;

        return id;
    }

    Chunk GetOrCreateChunk(Vector3Int coordinate)
    {
        if (chunks.TryGetValue(
            coordinate,
            out Chunk existing))
        {
            return existing;
        }

        GameObject obj =
            new GameObject(
                "Chunk_" +
                coordinate.x + "_" +
                coordinate.y + "_" +
                coordinate.z
            );

        obj.transform.SetParent(
            transform,
            false
        );

        obj.transform.localPosition =
            new Vector3(
                coordinate.x * chunkSize,
                coordinate.y * chunkSize,
                coordinate.z * chunkSize
            );

        MeshFilter filter =
            obj.AddComponent<MeshFilter>();

        MeshRenderer renderer =
            obj.AddComponent<MeshRenderer>();

        MeshCollider collider = null;

        if (generateColliders)
            collider = obj.AddComponent<MeshCollider>();

        Chunk chunk = new Chunk
        {
            coordinate = coordinate,

            blocks = new ushort[
                chunkSize *
                chunkSize *
                chunkSize
            ],

            gameObject = obj,
            filter = filter,
            renderer = renderer,
            collider = collider
        };

        chunks.Add(
            coordinate,
            chunk
        );

        return chunk;
    }

    void BuildChunk(Chunk chunk)
    {
        List<Vector3> vertices =
            new List<Vector3>();

        List<Vector2> uvs =
            new List<Vector2>();

        Dictionary<ushort, List<int>> triangles =
            new Dictionary<ushort, List<int>>();

        for (int y = 0; y < chunkSize; y++)
        {
            for (int z = 0; z < chunkSize; z++)
            {
                for (int x = 0; x < chunkSize; x++)
                {
                    ushort block =
                        chunk.blocks[
                            GetIndex(x, y, z)
                        ];

                    if (block == 0)
                        continue;

                    Vector3Int world =
                        new Vector3Int(
                            chunk.coordinate.x * chunkSize + x,
                            chunk.coordinate.y * chunkSize + y,
                            chunk.coordinate.z * chunkSize + z
                        );

                    for (int face = 0; face < 6; face++)
                    {
                        Vector3Int neighbor =
                            world + Directions[face];

                        if (GetBlock(neighbor) != 0)
                            continue;

                        if (!triangles.TryGetValue(
                            block,
                            out List<int> triangleList))
                        {
                            triangleList =
                                new List<int>();

                            triangles.Add(
                                block,
                                triangleList
                            );
                        }

                        int vertexStart =
                            vertices.Count;

                        for (int vertex = 0; vertex < 4; vertex++)
                        {
                            vertices.Add(
                                new Vector3(x, y, z) +
                                FaceVertices[face, vertex]
                            );
                        }

                        uvs.Add(
                            new Vector2(0, 0)
                        );

                        uvs.Add(
                            new Vector2(0, 1)
                        );

                        uvs.Add(
                            new Vector2(1, 1)
                        );

                        uvs.Add(
                            new Vector2(1, 0)
                        );

                        triangleList.Add(
                            vertexStart
                        );

                        triangleList.Add(
                            vertexStart + 1
                        );

                        triangleList.Add(
                            vertexStart + 2
                        );

                        triangleList.Add(
                            vertexStart
                        );

                        triangleList.Add(
                            vertexStart + 2
                        );

                        triangleList.Add(
                            vertexStart + 3
                        );
                    }
                }
            }
        }

        if (chunk.mesh != null)
            Destroy(chunk.mesh);

        Mesh mesh =
            new Mesh();

        mesh.name =
            chunk.gameObject.name + "_Mesh";

        mesh.indexFormat =
            IndexFormat.UInt32;

        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);

        List<Material> materials =
            new List<Material>();

        mesh.subMeshCount =
            triangles.Count;

        int subMesh = 0;

        foreach (
            KeyValuePair<ushort, List<int>> pair
            in triangles)
        {
            mesh.SetTriangles(
                pair.Value,
                subMesh,
                false
            );

            if (idToMaterial.TryGetValue(
                pair.Key,
                out Material material))
            {
                materials.Add(material);
            }

            subMesh++;
        }

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        chunk.mesh = mesh;

        chunk.filter.sharedMesh =
            mesh;

        chunk.renderer.sharedMaterials =
            materials.ToArray();

        if (chunk.collider != null)
        {
            chunk.collider.sharedMesh = null;
            chunk.collider.sharedMesh = mesh;
        }
    }

    void QueueRebuild(Vector3Int coordinate)
    {
        if (!chunks.ContainsKey(coordinate))
            return;

        if (!queued.Add(coordinate))
            return;

        long distance =
            GetDistanceFromPlayer(coordinate);

        if (!rebuildQueue.TryGetValue(
            distance,
            out Queue<Vector3Int> queue))
        {
            queue =
                new Queue<Vector3Int>();

            rebuildQueue.Add(
                distance,
                queue
            );
        }

        queue.Enqueue(coordinate);
    }

    void RebuildQueueFromPlayer()
    {
        if (queued.Count == 0)
            return;

        rebuildQueue.Clear();

        foreach (Vector3Int coordinate in queued)
        {
            long distance =
                GetDistanceFromPlayer(coordinate);

            if (!rebuildQueue.TryGetValue(
                distance,
                out Queue<Vector3Int> queue))
            {
                queue =
                    new Queue<Vector3Int>();

                rebuildQueue.Add(
                    distance,
                    queue
                );
            }

            queue.Enqueue(coordinate);
        }
    }

    long GetDistanceFromPlayer(Vector3Int coordinate)
    {
        if (player == null)
        {
            return
                (long)coordinate.x * coordinate.x +
                (long)coordinate.y * coordinate.y +
                (long)coordinate.z * coordinate.z;
        }

        Vector3Int playerChunk =
            WorldToChunk(
                Vector3Int.FloorToInt(
                    player.transform.position
                )
            );

        long dx =
            coordinate.x - playerChunk.x;

        long dy =
            coordinate.y - playerChunk.y;

        long dz =
            coordinate.z - playerChunk.z;

        return
            dx * dx +
            dy * dy +
            dz * dz;
    }

    Vector3Int WorldToChunk(Vector3Int position)
    {
        return new Vector3Int(
            FloorDivision(
                position.x,
                chunkSize
            ),

            FloorDivision(
                position.y,
                chunkSize
            ),

            FloorDivision(
                position.z,
                chunkSize
            )
        );
    }

    Vector3Int WorldToLocal(
        Vector3Int position,
        Vector3Int chunk)
    {
        return new Vector3Int(
            position.x -
            chunk.x * chunkSize,

            position.y -
            chunk.y * chunkSize,

            position.z -
            chunk.z * chunkSize
        );
    }

    int FloorDivision(
        int value,
        int divisor)
    {
        if (value >= 0)
            return value / divisor;

        return ((value + 1) / divisor) - 1;
    }

    int GetIndex(
        int x,
        int y,
        int z)
    {
        return
            x +
            chunkSize *
            (
                z +
                chunkSize * y
            );
    }
}