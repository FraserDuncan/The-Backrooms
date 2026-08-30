using UnityEngine;

public class PitField
{
    public readonly int cellOX, cellOZ, cellW, cellH;   // cell-aligned room footprint
    public readonly float ox, oz, totalW, totalH;       // lattice bounds (metres, centred in the room)
    public readonly int cols, rows;
    public readonly float[] colW;
    public readonly float pitH, walk;
    public readonly bool[] connecting;

    public PitField(int worldSeed, int cx, int cz, int pitFloor, ChunkManager m)
    {
        var rng = new System.Random(Hash(worldSeed, cx, cz, pitFloor));
        cols = rng.Next(3, 7);
        rows = rng.Next(2, 6);
        walk = Mathf.Max(0.5f, m.pitWalkway);
        pitH = Lerp(rng, m.pitMinSize, m.pitMaxSize);
        colW = new float[cols];
        for (int c = 0; c < cols; c++) colW[c] = Lerp(rng, m.pitMinSize, m.pitMaxSize);

        totalW = walk; for (int c = 0; c < cols; c++) totalW += colW[c] + walk;
        totalH = walk + rows * (pitH + walk);

        float cs = m.cellSize;
        cellW = Mathf.CeilToInt(totalW / cs);
        cellH = Mathf.CeilToInt(totalH / cs);
        int maxX = m.chunkSize - cellW - 1, maxZ = m.chunkSize - cellH - 1;
        cellOX = (maxX > 1) ? rng.Next(1, maxX) : 1;
        cellOZ = (maxZ > 1) ? rng.Next(1, maxZ) : 1;

        ox = cellOX * cs + (cellW * cs - totalW) * 0.5f;
        oz = cellOZ * cs + (cellH * cs - totalH) * 0.5f;

        int total = cols * rows;
        int nConn = Mathf.Min(rng.Next(1, 5), total);   // 1-4 ways down, never zero
        connecting = new bool[total];
        int placed = 0;
        while (placed < nConn)
        {
            int idx = rng.Next(0, total);
            if (!connecting[idx]) { connecting[idx] = true; placed++; }
        }
    }
    
    public bool IsOverAnyPit(float lx, float lz)
    {
        for (int c = 0; c < cols; c++)
        {
            float x0 = ColStart(c), x1 = x0 + colW[c];
            if (lx < x0 || lx > x1) continue;
            for (int r = 0; r < rows; r++)
            {
                float z0 = RowStart(r), z1 = z0 + pitH;
                if (lz >= z0 && lz <= z1) return true;
            }
        }
        return false;
    }

    public bool IsUnderConnectingPit(float lx, float lz)
    {
        for (int c = 0; c < cols; c++)
        {
            float x0 = ColStart(c), x1 = x0 + colW[c];
            if (lx < x0 || lx > x1) continue;
            for (int r = 0; r < rows; r++)
            {
                if (!IsConnecting(c, r)) continue;
                float z0 = RowStart(r), z1 = z0 + pitH;
                if (lz >= z0 && lz <= z1) return true;
            }
        }
        return false;
    }

    public bool OverlapsConnectingPit(float lx, float lz, float half)
    {
        for (int c = 0; c < cols; c++)
        {
            float x0 = ColStart(c), x1 = x0 + colW[c];
            if (lx + half < x0 || lx - half > x1) continue;
            for (int r = 0; r < rows; r++)
            {
                if (!IsConnecting(c, r)) continue;
                float z0 = RowStart(r), z1 = z0 + pitH;
                if (lz + half >= z0 && lz - half <= z1) return true;
            }
        }
        return false;
    }

    public float ColStart(int c) { float x = ox + walk; for (int k = 0; k < c; k++) x += colW[k] + walk; return x; }
    public float RowStart(int r) => oz + walk + r * (pitH + walk);
    public bool IsConnecting(int c, int r) => connecting[c * rows + r];
    public bool InsideRoom(float lx, float lz, float cs) =>
        lx >= cellOX * cs && lx < (cellOX + cellW) * cs && lz >= cellOZ * cs && lz < (cellOZ + cellH) * cs;

    static float Lerp(System.Random rng, float a, float b) => a + (float)rng.NextDouble() * (b - a);
    static int Hash(int s, int cx, int cz, int f)
    {
        unchecked { uint h = ((uint)s ^ 0x68E31DA4u) * 2654435761u;
            h = (h ^ (uint)cx) * 2654435761u; h = (h ^ (uint)cz) * 2654435761u;
            h = (h ^ (uint)f) * 2654435761u; h ^= h >> 13; return (int)h; }
    }
}