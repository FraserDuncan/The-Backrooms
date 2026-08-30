using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChunkManager : MonoBehaviour
{
    [Header("Grid")]
    public int chunkSize = 16;
    public float cellSize = 4f;

    [Header("Floors")]
    public int floors = 8;
    public float floorHeight = 8f;   // pit depth = floorHeight - ceilingHeight

    [Header("Streaming")]
    public int renderDistance = 2;
    public int floorLoadRange = 1;
    public int chunksPerFrame = 2;

    [Header("Rooms")]
    public int minRoom = 3;
    public int maxRoom = 8;
    [Range(0f,1f)] public float roomStopChance = 0.3f;
    public int doorsPerWall = 1;
    public int extraOpenings = 3;
    public int seamOpenings = 2;

    [Header("Walls")]
    public float wallHeight = 4f;
    public float wallThickness = 0.6f;
    public float wallTexelSize = 4f;
    public Material wallMaterial;

    [Header("Floor / Ceiling")]
    public Material floorMaterial;
    public float floorTexelSize = 4f;
    public GameObject ceilingPrefab;
    public float ceilingTileSize = 32f;
    public float ceilingHeight = 4f;
    public float ceilingSquareSize = 4f;
    public Material ceilingMaterial;        // used for the cut ceiling under pit rooms
    public float ceilingTexelSize = 2f;     // tiling for that cut ceiling

    [Header("Lights")]
    public GameObject lightPrefab;
    public int lightSpacingCells = 3;
    public int sparseSpacingCells = 5;
    [Range(0f,1f)] public float sparseRoomChance = 0.4f;

    [Header("Decoration")]
    [Range(0f,1f)] public float pitChance = 0.15f;
    public float pitMinSize = 1.5f;   // hole width range, metres
    public float pitMaxSize = 3f;
    public float pitWalkway = 1.5f;    // carpet path between holes

    [Header("Spawn")]
    public Transform player;

    [Header("Seed")]
    public int worldSeed = 0;
    public bool randomizeSeed = true;

    Transform root;
    ChunkBuilder builder;
    readonly Dictionary<Vector3Int, GameObject> loaded = new();
    readonly HashSet<Vector3Int> pending = new();
    readonly Queue<Vector3Int> buildQueue = new();
    Vector2Int playerChunk;
    int playerFloor;

    void Start()
    {
        ClearOld();
        if (randomizeSeed) worldSeed = Random.Range(0, 99999);
        root = new GameObject("Chunks").transform;
        root.SetParent(transform, false);
        builder = new ChunkBuilder(this);

        if (player)
        {
            float cc = (chunkSize / 2 + 0.5f) * cellSize;
            player.position = new Vector3(cc, (floors - 1) * floorHeight + 1.2f, cc);
        }
        playerChunk = player ? WorldToChunk(player.position) : Vector2Int.zero;
        playerFloor = player ? WorldToFloor(player.position) : floors - 1;

        var k0 = new Vector3Int(playerChunk.x, playerFloor, playerChunk.y);
        loaded[k0] = BuildChunk(k0.x, k0.z, k0.y);
        UpdateChunks();
        StartCoroutine(BuildLoop());
    }

    void Update()
    {
        if (!player) return;
        var pc = WorldToChunk(player.position);
        int pf = WorldToFloor(player.position);
        if (pc != playerChunk || pf != playerFloor) { playerChunk = pc; playerFloor = pf; UpdateChunks(); }
    }

    Vector2Int WorldToChunk(Vector3 p)
    {
        float span = chunkSize * cellSize;
        return new Vector2Int(Mathf.FloorToInt(p.x / span), Mathf.FloorToInt(p.z / span));
    }
    int WorldToFloor(Vector3 p) => Mathf.Clamp(Mathf.FloorToInt(p.y / floorHeight), 0, floors - 1);

    void UpdateChunks()
    {
        int fLo = Mathf.Max(0, playerFloor - floorLoadRange);
        int fHi = Mathf.Min(floors - 1, playerFloor + floorLoadRange);

        var desired = new HashSet<Vector3Int>();
        for (int dx = -renderDistance; dx <= renderDistance; dx++)
            for (int dz = -renderDistance; dz <= renderDistance; dz++)
                for (int f = fLo; f <= fHi; f++)
                    desired.Add(new Vector3Int(playerChunk.x + dx, f, playerChunk.y + dz));

        var remove = new List<Vector3Int>();
        foreach (var kv in loaded) if (!desired.Contains(kv.Key)) remove.Add(kv.Key);
        foreach (var k in remove) { Destroy(loaded[k]); loaded.Remove(k); }

        foreach (var k in desired)
            if (!loaded.ContainsKey(k) && !pending.Contains(k)) { pending.Add(k); buildQueue.Enqueue(k); }
    }

    IEnumerator BuildLoop()
    {
        while (true)
        {
            int budget = chunksPerFrame;
            while (budget > 0 && buildQueue.Count > 0)
            {
                var k = buildQueue.Dequeue();
                pending.Remove(k);
                if (!loaded.ContainsKey(k) && IsDesired(k))
                    loaded[k] = BuildChunk(k.x, k.z, k.y);
                budget--;
            }
            yield return null;
        }
    }

    bool IsDesired(Vector3Int k)
    {
        int fLo = Mathf.Max(0, playerFloor - floorLoadRange);
        int fHi = Mathf.Min(floors - 1, playerFloor + floorLoadRange);
        return Mathf.Abs(k.x - playerChunk.x) <= renderDistance
            && Mathf.Abs(k.z - playerChunk.y) <= renderDistance
            && k.y >= fLo && k.y <= fHi;
    }

    GameObject BuildChunk(int cx, int cz, int floor)
    {
        var data = new ChunkData(cx, cz, floor, this);
        return builder.Build(data, root, floor * floorHeight);
    }

    void ClearOld()
    {
        var ex = transform.Find("Chunks");
        if (ex) { if (Application.isPlaying) Destroy(ex.gameObject); else DestroyImmediate(ex.gameObject); }
    }
}