using System.Collections.Generic;
using UnityEngine;

public class ChunkBuilder
{
    readonly ChunkManager m;
    public ChunkBuilder(ChunkManager manager) { m = manager; }

    public GameObject Build(ChunkData c, Transform parent, float yBase)
    {
        float span = c.size * m.cellSize;
        Vector3 origin = new Vector3(c.cx * span, yBase, c.cz * span);
        var go = new GameObject($"Chunk_{c.cx}_{c.cz}_f{c.floor}");
        go.transform.SetParent(parent, false);

        BuildFloor(c, go.transform, origin, span);
        BuildWalls(c, go.transform, origin);
        BuildCeiling(c, go.transform, origin, span);
        BuildLights(c, go.transform, origin);
        return go;
    }

    void BuildFloor(ChunkData c, Transform parent, Vector3 origin, float span)
    {
        if (c.isPitRoom && c.pitField != null)
        {
            var pf = c.pitField;
            float lx0 = pf.ox, lz0 = pf.oz, lw = pf.totalW, lh = pf.totalH;
            // base floor everywhere EXCEPT the lattice footprint, so the holes stay open
            FloorPiece(parent, origin, 0, 0, lx0, span, origin.y);
            FloorPiece(parent, origin, lx0 + lw, 0, span - (lx0 + lw), span, origin.y);
            FloorPiece(parent, origin, lx0, 0, lw, lz0, origin.y);
            FloorPiece(parent, origin, lx0, lz0 + lh, lw, span - (lz0 + lh), origin.y);
            BuildPitFloor(c, parent, origin, span);
            return;
        }
        FloorPiece(parent, origin, 0, 0, span, span, origin.y);   // normal full slab
    }

    void BuildPitFloor(ChunkData c, Transform parent, Vector3 origin, float span)
    {
        var pf = c.pitField;
        float depth = Mathf.Max(0.5f, m.floorHeight - m.ceilingHeight);

        // walkway grid (thin carpet, between the holes)
        FloorPiece(parent, origin, pf.ox, pf.oz, pf.totalW, pf.walk, origin.y);                  // bottom border
        for (int r = 0; r < pf.rows; r++)
            FloorPiece(parent, origin, pf.ox, pf.RowStart(r) + pf.pitH, pf.totalW, pf.walk, origin.y);
        FloorPiece(parent, origin, pf.ox, pf.oz, pf.walk, pf.totalH, origin.y - 0.005f);         // left border
        for (int cc = 0; cc < pf.cols; cc++)
            FloorPiece(parent, origin, pf.ColStart(cc) + pf.colW[cc], pf.oz, pf.walk, pf.totalH, origin.y - 0.005f);

        // connecting shafts go full depth (open into the room below);
        // dead-ends stop short so their floor stays hidden in the gap, not poking the lower ceiling
        for (int cc = 0; cc < pf.cols; cc++)
            for (int rr = 0; rr < pf.rows; rr++)
            {
                float hx = pf.ColStart(cc), hz = pf.RowStart(rr), w = pf.colW[cc], h = pf.pitH;
                if (pf.IsConnecting(cc, rr))
                {
                    BuildWell(parent, origin, hx, hz, w, h, depth);
                }
                else
                {
                    float ded = Mathf.Max(0.5f, depth - 0.6f);
                    BuildWell(parent, origin, hx, hz, w, h, ded);
                    FloorPiece(parent, origin, hx, hz, w, h, origin.y - ded);   // closed bottom, kept above the lower ceiling
                }
            }
    }

    void BuildWell(Transform parent, Vector3 origin, float x0, float z0, float w, float h, float depth)
    {
        float cy = origin.y - depth * 0.5f, th = 0.1f;
        WallBox(parent, new Vector3(origin.x + x0,             cy, origin.z + z0 + h * 0.5f), new Vector3(th, depth, h));
        WallBox(parent, new Vector3(origin.x + x0 + w,         cy, origin.z + z0 + h * 0.5f), new Vector3(th, depth, h));
        WallBox(parent, new Vector3(origin.x + x0 + w * 0.5f,  cy, origin.z + z0),            new Vector3(w, depth, th));
        WallBox(parent, new Vector3(origin.x + x0 + w * 0.5f,  cy, origin.z + z0 + h),        new Vector3(w, depth, th));
    }

    void WallBox(Transform parent, Vector3 pos, Vector3 scale)
    {
        var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
        b.name = "WellWall"; b.transform.SetParent(parent, false);
        b.transform.position = pos; b.transform.localScale = scale;
        b.isStatic = true;
        if (m.floorMaterial) b.GetComponent<Renderer>().sharedMaterial = m.floorMaterial;
        var t = b.AddComponent<WallTextureScale>(); t.worldUnitsPerTile = m.floorTexelSize;
    }

    void FloorPiece(Transform parent, Vector3 origin, float x0, float z0, float w, float d, float topY)
    {
        if (w <= 0.01f || d <= 0.01f) return;
        var f = GameObject.CreatePrimitive(PrimitiveType.Cube);
        f.name = "Floor"; f.transform.SetParent(parent, false);
        f.transform.position = new Vector3(origin.x + x0 + w * 0.5f, topY - 0.1f, origin.z + z0 + d * 0.5f);
        f.transform.localScale = new Vector3(w, 0.2f, d);
        f.isStatic = true;
        var r = f.GetComponent<Renderer>();
        if (m.floorMaterial) r.sharedMaterial = m.floorMaterial;
        var mpb = new MaterialPropertyBlock(); r.GetPropertyBlock(mpb);
        mpb.SetVector("_BaseMap_ST", new Vector4(w / m.floorTexelSize, d / m.floorTexelSize,
            (origin.x + x0) / m.floorTexelSize, (origin.z + z0) / m.floorTexelSize));
        r.SetPropertyBlock(mpb);
    }

    void BuildWalls(ChunkData c, Transform parent, Vector3 origin)
    {
        int size = c.size; float cs = m.cellSize;
        for (int x = 0; x < size; x++)
        {
            int y = 0;
            while (y < size)
            {
                if (c.vWalls[x, y])
                {
                    int y0 = y; while (y < size && c.vWalls[x, y]) y++;
                    float len = (y - y0) * cs, cz = (y0 + y) * 0.5f * cs;
                    MakeWall(parent, origin + new Vector3(x * cs, m.wallHeight * 0.5f, cz),
                             new Vector3(m.wallThickness, m.wallHeight, len + m.wallThickness));
                }
                else y++;
            }
        }
        for (int y = 0; y < size; y++)
        {
            int x = 0;
            while (x < size)
            {
                if (c.hWalls[x, y])
                {
                    int x0 = x; while (x < size && c.hWalls[x, y]) x++;
                    float len = (x - x0) * cs, cx = (x0 + x) * 0.5f * cs;
                    MakeWall(parent, origin + new Vector3(cx, m.wallHeight * 0.5f, y * cs),
                             new Vector3(len + m.wallThickness, m.wallHeight, m.wallThickness));
                }
                else x++;
            }
        }
    }

    void MakeWall(Transform parent, Vector3 pos, Vector3 scale)
    {
        var w = GameObject.CreatePrimitive(PrimitiveType.Cube);
        w.name = "Wall"; w.transform.SetParent(parent, false);
        w.transform.position = pos; w.transform.localScale = scale;
        w.isStatic = true;
        if (m.wallMaterial) w.GetComponent<Renderer>().sharedMaterial = m.wallMaterial;
        var t = w.AddComponent<WallTextureScale>(); t.worldUnitsPerTile = m.wallTexelSize;
    }

    void BuildCeiling(ChunkData c, Transform parent, Vector3 origin, float span)
    {
        if (c.ceilingOpen && c.aboveField != null) { BuildOpenCeiling(c, parent, origin, span); return; }
        if (!m.ceilingPrefab) return;
        int n = Mathf.CeilToInt(span / m.ceilingTileSize);
        for (int i = 0; i < n; i++) for (int j = 0; j < n; j++)
        {
            var t = Object.Instantiate(m.ceilingPrefab, parent);
            t.transform.position = origin + new Vector3(
                i * m.ceilingTileSize + m.ceilingTileSize * 0.5f, m.ceilingHeight,
                j * m.ceilingTileSize + m.ceilingTileSize * 0.5f);
        }
    }

    // ceiling for the floor below a pit room: solid everywhere except precise holes under connecting shafts
    void BuildOpenCeiling(ChunkData c, Transform parent, Vector3 origin, float span)
    {
        var pf = c.aboveField;
        var holes = new List<Vector4>();   // x0, z0, x1, z1
        for (int cc = 0; cc < pf.cols; cc++)
            for (int rr = 0; rr < pf.rows; rr++)
                if (pf.IsConnecting(cc, rr))
                {
                    float x0 = pf.ColStart(cc), z0 = pf.RowStart(rr);
                    holes.Add(new Vector4(x0, z0, x0 + pf.colW[cc], z0 + pf.pitH));
                }

        var zset = new SortedSet<float> { 0f, span };
        foreach (var h in holes) { zset.Add(Mathf.Clamp(h.y, 0, span)); zset.Add(Mathf.Clamp(h.w, 0, span)); }
        var zs = new List<float>(zset);

        for (int i = 0; i < zs.Count - 1; i++)
        {
            float za = zs[i], zb = zs[i + 1], zmid = (za + zb) * 0.5f;
            var xcuts = new List<Vector2>();
            foreach (var h in holes) if (zmid > h.y && zmid < h.w) xcuts.Add(new Vector2(h.x, h.z));
            xcuts.Sort((a, b) => a.x.CompareTo(b.x));

            float xprev = 0f;
            foreach (var xc in xcuts)
            {
                if (xc.x > xprev) CeilingSlab(parent, origin, xprev, za, xc.x - xprev, zb - za);
                xprev = Mathf.Max(xprev, xc.y);
            }
            if (xprev < span) CeilingSlab(parent, origin, xprev, za, span - xprev, zb - za);
        }
    }

    void CeilingSlab(Transform parent, Vector3 origin, float x0, float z0, float w, float d)
    {
        if (w <= 0.01f || d <= 0.01f) return;
        var f = GameObject.CreatePrimitive(PrimitiveType.Cube);
        f.name = "Ceiling"; f.transform.SetParent(parent, false);
        f.transform.position = origin + new Vector3(x0 + w * 0.5f, m.ceilingHeight + 0.1f, z0 + d * 0.5f);
        f.transform.localScale = new Vector3(w, 0.2f, d);
        f.isStatic = true;
        Material mat = m.ceilingMaterial ? m.ceilingMaterial
                     : (m.ceilingPrefab ? m.ceilingPrefab.GetComponentInChildren<Renderer>()?.sharedMaterial : null);
        var r = f.GetComponent<Renderer>();
        if (mat) r.sharedMaterial = mat;
        var mpb = new MaterialPropertyBlock(); r.GetPropertyBlock(mpb);
        mpb.SetVector("_BaseMap_ST", new Vector4(w / m.ceilingTexelSize, d / m.ceilingTexelSize,
            (origin.x + x0) / m.ceilingTexelSize, (origin.z + z0) / m.ceilingTexelSize));
        r.SetPropertyBlock(mpb);
    }

    void BuildLights(ChunkData c, Transform parent, Vector3 origin)
    {
        if (!m.lightPrefab) return;
        float cs = m.cellSize;
        var rng = new System.Random(LightSeed(m.worldSeed, c.cx, c.cz, c.floor));
        foreach (var r in c.rooms)
        {
            int spacing = (rng.NextDouble() < m.sparseRoomChance) ? m.sparseSpacingCells : m.lightSpacingCells;
            int nx = Mathf.Max(1, Mathf.CeilToInt(r.width  / (float)spacing));
            int ny = Mathf.Max(1, Mathf.CeilToInt(r.height / (float)spacing));
            for (int a = 0; a < nx; a++) for (int b = 0; b < ny; b++)
            {
                float fx = (a + 0.5f) / nx, fy = (b + 0.5f) / ny;
                float lx = (r.x + fx * r.width) * cs, lz = (r.y + fy * r.height) * cs;
                if (c.isPitRoom && c.pitField != null && c.pitField.IsOverAnyPit(lx, lz)) continue;          // dark shafts
                if (c.ceilingOpen && c.aboveField != null && c.aboveField.IsUnderConnectingPit(lx, lz)) continue;
                float x = SnapToGrid(origin.x + lx, m.ceilingSquareSize);
                float z = SnapToGrid(origin.z + lz, m.ceilingSquareSize);
                var l = Object.Instantiate(m.lightPrefab, parent);
                l.transform.position = new Vector3(x, origin.y + m.ceilingHeight - 0.02f, z);
            }
        }
    }

    static int LightSeed(int s, int cx, int cz, int floor)
    {
        unchecked { uint h = ((uint)s ^ 0xC2B2AE35u) * 2654435761u;
            h = (h ^ (uint)cx) * 2654435761u; h = (h ^ (uint)cz) * 2654435761u;
            h = (h ^ (uint)floor) * 2654435761u; h ^= h >> 13; return (int)h; }
    }

    public void BuildPerimeter(Transform parent, Vector3 min, Vector3 max, float height)
    {
        float th = m.wallThickness, cy = height * 0.5f;
        float lenX = max.x - min.x, lenZ = max.z - min.z;
        float midX = (min.x + max.x) * 0.5f, midZ = (min.z + max.z) * 0.5f;
        MakeWall(parent, new Vector3(min.x, cy, midZ), new Vector3(th, height, lenZ + th));
        MakeWall(parent, new Vector3(max.x, cy, midZ), new Vector3(th, height, lenZ + th));
        MakeWall(parent, new Vector3(midX, cy, min.z), new Vector3(lenX + th, height, th));
        MakeWall(parent, new Vector3(midX, cy, max.z), new Vector3(lenX + th, height, th));
    }

    float SnapToGrid(float v, float grid) => (Mathf.Floor(v / grid) + 0.5f) * grid;
}