using UnityEngine;
using System.Collections.Generic;

public class BackroomsGenerator : MonoBehaviour
{
    public GridPathfinder Pathfinder { get; private set; }
    [Header("Grid")]
    public int cells = 20;
    public float cellSize = 4f;

    [Header("Walls")]
    public float wallHeight = 4f;
    public float wallThickness = 0.6f;
    public float wallTexelSize = 4f;

    [Header("Enemies")]
    public GameObject enemyPrefab;
    public int enemyCount = 3;
    public float minDistanceFromPlayer = 30f;
    public float enemyY = 1f;

    [Header("Rooms")]
    public int minRoom = 3;
    public int maxRoom = 8;
    [Range(0f,1f)] public float roomStopChance = 0.3f;
    public int doorsPerWall = 1;
    public int extraOpenings = 25;

    [Header("Materials / Ceiling")]
    public Material wallMaterial;
    public Material floorMaterial;
    public float floorTexelSize = 4f;
    public GameObject ceilingPrefab;
    public float ceilingTileSize = 40f;
    public float ceilingHeight = 4f;          // ceiling AND lights both sit here now
    public float ceilingSquareSize = 4f;

    [Header("Spawn")]
    public Transform player;

    [Header("Lights")]
    public GameObject lightPrefab;
    public int lightSpacingCells = 3;
    public int sparseSpacingCells = 5;
    [Range(0f,1f)] public float sparseRoomChance = 0.4f;

    [Header("Seed")]
    public int seed = 0;
    public bool randomizeSeed = true;

    private bool[,] vWalls, hWalls;
    private List<RectInt> rooms;
    private Transform root;

    [ContextMenu("Generate")]
    public void Generate()
    {
        if (randomizeSeed) seed = Random.Range(0, 99999);
        Random.InitState(seed);
        ClearOld();
        root = new GameObject("Generated").transform;
        root.SetParent(transform, false);

        vWalls = new bool[cells + 1, cells];
        hWalls = new bool[cells, cells + 1];

        for (int y = 0; y < cells; y++) { vWalls[0, y] = true; vWalls[cells, y] = true; }
        for (int x = 0; x < cells; x++) { hWalls[x, 0] = true; hWalls[x, cells] = true; }
        rooms = new List<RectInt>();
        Divide(0, 0, cells, cells);
        AddOpenings(extraOpenings);
        Pathfinder = new GridPathfinder(cells, cellSize, vWalls, hWalls);
        CarveSpawn();
        BuildFloor();
        BuildWalls();
        BuildCeiling();
        BuildLights();
        SpawnEnemies();
    }

    void Start() { Generate(); }

    void BuildLights()
    {
        if (!lightPrefab || rooms == null) return;

        foreach (var r in rooms)
        {
            int spacing = (Random.value < sparseRoomChance) ? sparseSpacingCells : lightSpacingCells;
            int nx = Mathf.Max(1, Mathf.CeilToInt(r.width  / (float)spacing));
            int ny = Mathf.Max(1, Mathf.CeilToInt(r.height / (float)spacing));

            for (int a = 0; a < nx; a++)
                for (int b = 0; b < ny; b++)
                {
                    float fx = (a + 0.5f) / nx;
                    float fy = (b + 0.5f) / ny;
                    float x = SnapToGrid((r.x + fx * r.width) * cellSize, ceilingSquareSize);
                    float z = SnapToGrid((r.y + fy * r.height) * cellSize, ceilingSquareSize);
                    var l = Instantiate(lightPrefab, root);
                    l.transform.position = new Vector3(x, ceilingHeight - 0.02f, z);  // flush
                }
        }
    }

    void CarveSpawn()
    {
        int sx = cells / 2, sy = cells / 2;
        for (int x = sx; x <= sx + 1; x++) for (int y = sy - 1; y <= sy + 1; y++)
            if (x > 0 && x < cells) vWalls[x, y] = false;
        for (int x = sx - 1; x <= sx + 1; x++) for (int y = sy; y <= sy + 1; y++)
            if (y > 0 && y < cells) hWalls[x, y] = false;
        if (player) player.position = new Vector3((sx + 0.5f) * cellSize, 1.2f, (sy + 0.5f) * cellSize);
    }

    void BuildWalls()
    {
        for (int x = 0; x <= cells; x++)
        {
            int y = 0;
            while (y < cells)
            {
                if (vWalls[x, y])
                {
                    int y0 = y; while (y < cells && vWalls[x, y]) y++;
                    float len = (y - y0) * cellSize;
                    float cz = (y0 + y) * 0.5f * cellSize;
                    MakeWall(new Vector3(x * cellSize, wallHeight * 0.5f, cz),
                             new Vector3(wallThickness, wallHeight, len + wallThickness));  // +overlap
                }
                else y++;
            }
        }
        for (int y = 0; y <= cells; y++)
        {
            int x = 0;
            while (x < cells)
            {
                if (hWalls[x, y])
                {
                    int x0 = x; while (x < cells && hWalls[x, y]) x++;
                    float len = (x - x0) * cellSize;
                    float cx = (x0 + x) * 0.5f * cellSize;
                    MakeWall(new Vector3(cx, wallHeight * 0.5f, y * cellSize),
                             new Vector3(len + wallThickness, wallHeight, wallThickness));  // +overlap
                }
                else x++;
            }
        }
    }

    void MakeWall(Vector3 pos, Vector3 scale)
    {
        var w = GameObject.CreatePrimitive(PrimitiveType.Cube);
        w.name = "Wall";
        w.transform.SetParent(root, false);
        w.transform.position = pos;
        w.transform.localScale = scale;
        w.isStatic = true;
        if (wallMaterial) w.GetComponent<Renderer>().sharedMaterial = wallMaterial;
        var t = w.AddComponent<WallTextureScale>();
        t.worldUnitsPerTile = wallTexelSize;
    }

    void BuildFloor()
    {
        float total = cells * cellSize;
        var f = GameObject.CreatePrimitive(PrimitiveType.Cube);
        f.name = "Floor";
        f.transform.SetParent(root, false);
        f.transform.position = new Vector3(total * 0.5f, -0.1f, total * 0.5f);
        f.transform.localScale = new Vector3(total, 0.2f, total);
        f.isStatic = true;
        var r = f.GetComponent<Renderer>();
        if (floorMaterial) r.sharedMaterial = floorMaterial;
        var mpb = new MaterialPropertyBlock();
        r.GetPropertyBlock(mpb);
        mpb.SetVector("_BaseMap_ST", new Vector4(total / floorTexelSize, total / floorTexelSize, 0, 0));
        r.SetPropertyBlock(mpb);
    }

    void BuildCeiling()
    {
        if (!ceilingPrefab) return;
        float total = cells * cellSize;
        int n = Mathf.CeilToInt(total / ceilingTileSize);
        for (int i = 0; i < n; i++) for (int j = 0; j < n; j++)
        {
            var c = Instantiate(ceilingPrefab, root);
            c.transform.position = new Vector3(
                i * ceilingTileSize + ceilingTileSize * 0.5f,
                ceilingHeight,
                j * ceilingTileSize + ceilingTileSize * 0.5f);
        }
    }

    void ClearOld()
    {
        var ex = transform.Find("Generated");
        if (ex) { if (Application.isPlaying) Destroy(ex.gameObject); else DestroyImmediate(ex.gameObject); }
    }

    void Divide(int x0, int y0, int x1, int y1)
    {
        int w = x1 - x0, h = y1 - y0;
        bool canV = w >= minRoom * 2;
        bool canH = h >= minRoom * 2;
        if ((!canV && !canH) || (w <= maxRoom && h <= maxRoom && Random.value < roomStopChance))
        {
            rooms.Add(new RectInt(x0, y0, w, h));
            return;
        }

        bool vertical = (canV && canH) ? (w >= h) : canV;
        if (vertical)
        {
            int sx = Random.Range(x0 + minRoom, x1 - minRoom + 1);
            for (int y = y0; y < y1; y++) vWalls[sx, y] = true;
            for (int d = 0; d < doorsPerWall; d++) vWalls[sx, Random.Range(y0, y1)] = false;
            Divide(x0, y0, sx, y1);
            Divide(sx, y0, x1, y1);
        }
        else
        {
            int sy = Random.Range(y0 + minRoom, y1 - minRoom + 1);
            for (int x = x0; x < x1; x++) hWalls[x, sy] = true;
            for (int d = 0; d < doorsPerWall; d++) hWalls[Random.Range(x0, x1), sy] = false;
            Divide(x0, y0, x1, sy);
            Divide(x0, sy, x1, y1);
        }
    }

    void AddOpenings(int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (Random.value < 0.5f)
            {
                int x = Random.Range(1, cells), y = Random.Range(0, cells);
                vWalls[x, y] = false;
            }
            else
            {
                int x = Random.Range(0, cells), y = Random.Range(1, cells);
                hWalls[x, y] = false;
            }
        }
    }

    void SpawnEnemies()
    {
        if (!enemyPrefab) return;

        Vector2Int spawnCell = new Vector2Int(cells / 2, cells / 2);
        Vector3 playerStart = new Vector3((spawnCell.x + 0.5f) * cellSize, 0f, (spawnCell.y + 0.5f) * cellSize);

        int placed = 0, attempts = 0;
        while (placed < enemyCount && attempts < enemyCount * 50)
        {
            attempts++;
            int i = Random.Range(0, cells);
            int j = Random.Range(0, cells);
            Vector3 pos = new Vector3((i + 0.5f) * cellSize, enemyY, (j + 0.5f) * cellSize);
            if (Vector3.Distance(pos, playerStart) < minDistanceFromPlayer) continue;

            var e = Instantiate(enemyPrefab, pos, Quaternion.identity, root);
            var ai = e.GetComponent<EnemyController>();
            if (ai) { ai.generator = this; ai.player = player; }
            placed++;
        }
    }

    float SnapToGrid(float v, float grid) => (Mathf.Floor(v / grid) + 0.5f) * grid;
}