using System.Collections.Generic;
using UnityEngine;

public class ChunkData
{
    public readonly int cx, cz, floor, size;
    public readonly bool[,] vWalls;
    public readonly bool[,] hWalls;
    public readonly List<RectInt> rooms = new List<RectInt>();
    public bool isPitRoom { get; private set; }
    public bool ceilingOpen { get; private set; }
    public PitField pitField;     // set if isPitRoom
    public PitField aboveField;   // set if ceilingOpen (the pit room directly above)

    readonly ChunkManager m;
    const uint VSALT = 0x9E3779B1, HSALT = 0x85EBCA77, SSALT = 0x27D4EB2F;

    public ChunkData(int cx, int cz, int floor, ChunkManager m)
    {
        this.cx = cx; this.cz = cz; this.floor = floor; this.m = m; this.size = m.chunkSize;

        vWalls = new bool[size + 1, size];
        hWalls = new bool[size, size + 1];

        for (int y = 0; y < size; y++) { vWalls[0, y] = true; vWalls[size, y] = true; }
        for (int x = 0; x < size; x++) { hWalls[x, 0] = true; hWalls[x, size] = true; }

        Random.State prev = Random.state;
        Random.InitState(ChunkSeed(m.worldSeed, cx, cz, floor));
        Divide(0, 0, size, size);
        AddOpenings(m.extraOpenings);
        Random.state = prev;

        CarveSeam(true,  0,    SeamSeed(m.worldSeed, cx,     cz, floor, VSALT));
        CarveSeam(true,  size, SeamSeed(m.worldSeed, cx + 1, cz, floor, VSALT));
        CarveSeam(false, 0,    SeamSeed(m.worldSeed, cx, cz,     floor, HSALT));
        CarveSeam(false, size, SeamSeed(m.worldSeed, cx, cz + 1, floor, HSALT));

        ComputePit();
    }

    void ComputePit()
    {
        if (m.floors < 2) return;
        if (floor >= 1 && IsPitFloor(floor))
        {
            isPitRoom = true;
            pitField = new PitField(m.worldSeed, cx, cz, floor, m);
            CarveFieldRoom(pitField);
        }
        if (IsPitFloor(floor + 1))
        {
            ceilingOpen = true;
            aboveField = new PitField(m.worldSeed, cx, cz, floor + 1, m);
            CarveLandings(aboveField);
        }
    }

    // turn the field footprint into a sealed room sitting inside the normal maze
    void CarveFieldRoom(PitField pf)
    {
        int x0 = pf.cellOX, z0 = pf.cellOZ, x1 = pf.cellOX + pf.cellW, z1 = pf.cellOZ + pf.cellH;

        for (int x = x0 + 1; x < x1; x++) for (int y = z0; y < z1; y++) vWalls[x, y] = false;
        for (int x = x0; x < x1; x++) for (int y = z0 + 1; y < z1; y++) hWalls[x, y] = false;

        for (int y = z0; y < z1; y++) { vWalls[x0, y] = true; vWalls[x1, y] = true; }
        for (int x = x0; x < x1; x++) { hWalls[x, z0] = true; hWalls[x, z1] = true; }

        var rng = new System.Random(ChunkSeed(m.worldSeed, cx, cz, floor) ^ 0x51ED2701);
        int doors = rng.Next(1, 3);
        for (int d = 0; d < doors; d++)
        {
            switch (rng.Next(0, 4))
            {
                case 0: vWalls[x0, z0 + rng.Next(0, pf.cellH)] = false; break;
                case 1: vWalls[x1, z0 + rng.Next(0, pf.cellH)] = false; break;
                case 2: hWalls[x0 + rng.Next(0, pf.cellW), z0] = false; break;
                default: hWalls[x0 + rng.Next(0, pf.cellW), z1] = false; break;
            }
        }
        rooms.Add(new RectInt(x0, z0, pf.cellW, pf.cellH));
    }

    // clear a small open pocket under each connecting shaft so you don't drop into a wall
    void CarveLandings(PitField pf)
    {
        float cs = m.cellSize;
        for (int c = 0; c < pf.cols; c++)
            for (int r = 0; r < pf.rows; r++)
            {
                if (!pf.IsConnecting(c, r)) continue;
                int x0 = Mathf.Clamp(Mathf.FloorToInt(pf.ColStart(c) / cs), 0, size - 1);
                int x1 = Mathf.Clamp(Mathf.FloorToInt((pf.ColStart(c) + pf.colW[c]) / cs), 0, size - 1);
                int z0 = Mathf.Clamp(Mathf.FloorToInt(pf.RowStart(r) / cs), 0, size - 1);
                int z1 = Mathf.Clamp(Mathf.FloorToInt((pf.RowStart(r) + pf.pitH) / cs), 0, size - 1);
                for (int x = x0; x <= x1 + 1; x++)
                    for (int z = z0; z <= z1; z++)
                        if (x > 0 && x < size) vWalls[x, z] = false;
                for (int x = x0; x <= x1; x++)
                    for (int z = z0; z <= z1 + 1; z++)
                        if (z > 0 && z < size) hWalls[x, z] = false;
            }
    }

    // top-down greedy resolution -> never two pit rooms directly stacked
    bool IsPitFloor(int f)
    {
        if (f < 1 || f >= m.floors) return false;
        bool prevIsPit = false;
        for (int k = m.floors - 1; k >= f; k--)
        {
            bool isPit = (k >= 1) && Roll(k) && !prevIsPit;
            if (k == f) return isPit;
            prevIsPit = isPit;
        }
        return false;
    }

    bool Roll(int f)
    {
        var rng = new System.Random(SeamSeed(m.worldSeed, cx, cz, f, SSALT));
        return rng.NextDouble() < m.pitChance;
    }

    void CarveSeam(bool vertical, int line, int seamSeed)
    {
        var rng = new System.Random(seamSeed);
        for (int k = 0; k < m.seamOpenings; k++)
        {
            int pos = rng.Next(0, size);
            if (vertical) vWalls[line, pos] = false;
            else          hWalls[pos, line] = false;
        }
    }

    static int ChunkSeed(int s, int cx, int cz, int floor)
    {
        unchecked { uint h = (uint)s * 2654435761u;
            h = (h ^ (uint)cx) * 2654435761u; h = (h ^ (uint)cz) * 2654435761u;
            h = (h ^ (uint)floor) * 2654435761u; h ^= h >> 13; return (int)h; }
    }
    static int SeamSeed(int s, int a, int b, int floor, uint salt)
    {
        unchecked { uint h = ((uint)s ^ salt) * 2654435761u;
            h = (h ^ (uint)a) * 2654435761u; h = (h ^ (uint)b) * 2654435761u;
            h = (h ^ (uint)floor) * 2654435761u; h ^= h >> 13; return (int)h; }
    }

    void Divide(int x0, int y0, int x1, int y1)
    {
        int w = x1 - x0, h = y1 - y0;
        bool canV = w >= m.minRoom * 2, canH = h >= m.minRoom * 2;
        if ((!canV && !canH) || (w <= m.maxRoom && h <= m.maxRoom && Random.value < m.roomStopChance))
        { rooms.Add(new RectInt(x0, y0, w, h)); return; }

        bool vertical = (canV && canH) ? (w >= h) : canV;
        if (vertical)
        {
            int sx = Random.Range(x0 + m.minRoom, x1 - m.minRoom + 1);
            for (int y = y0; y < y1; y++) vWalls[sx, y] = true;
            for (int d = 0; d < m.doorsPerWall; d++) vWalls[sx, Random.Range(y0, y1)] = false;
            Divide(x0, y0, sx, y1); Divide(sx, y0, x1, y1);
        }
        else
        {
            int sy = Random.Range(y0 + m.minRoom, y1 - m.minRoom + 1);
            for (int x = x0; x < x1; x++) hWalls[x, sy] = true;
            for (int d = 0; d < m.doorsPerWall; d++) hWalls[Random.Range(x0, x1), sy] = false;
            Divide(x0, y0, x1, sy); Divide(x0, sy, x1, y1);
        }
    }

    void AddOpenings(int count)
    {
        for (int i = 0; i < count; i++)
            if (Random.value < 0.5f) { vWalls[Random.Range(1, size), Random.Range(0, size)] = false; }
            else                     { hWalls[Random.Range(0, size), Random.Range(1, size)] = false; }
    }
}