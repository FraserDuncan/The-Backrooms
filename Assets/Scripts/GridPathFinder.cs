using System.Collections.Generic;
using UnityEngine;

public class GridPathfinder
{
    readonly int cells;
    readonly float cellSize;
    readonly bool[,] vWalls, hWalls;

    public GridPathfinder(int cells, float cellSize, bool[,] vWalls, bool[,] hWalls)
    {
        this.cells = cells; this.cellSize = cellSize;
        this.vWalls = vWalls; this.hWalls = hWalls;
    }

    public Vector2Int WorldToCell(Vector3 w) => new Vector2Int(
        Mathf.Clamp(Mathf.FloorToInt(w.x / cellSize), 0, cells - 1),
        Mathf.Clamp(Mathf.FloorToInt(w.z / cellSize), 0, cells - 1));

    public Vector3 CellToWorld(Vector2Int c, float y = 0f) =>
        new Vector3((c.x + 0.5f) * cellSize, y, (c.y + 0.5f) * cellSize);

    bool CanMove(Vector2Int from, Vector2Int to)
    {
        int dx = to.x - from.x, dy = to.y - from.y;
        if (dx == 1 && dy == 0) return !vWalls[from.x + 1, from.y];   // east
        if (dx == -1 && dy == 0) return !vWalls[from.x, from.y];      // west
        if (dx == 0 && dy == 1) return !hWalls[from.x, from.y + 1];   // north
        if (dx == 0 && dy == -1) return !hWalls[from.x, from.y];      // south
        return false;
    }

    static readonly Vector2Int[] dirs =
        { new(1,0), new(-1,0), new(0,1), new(0,-1) };

    static int H(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

    public List<Vector2Int> FindPath(Vector2Int start, Vector2Int goal)
    {
        if (start == goal) return new List<Vector2Int> { start };

        var open = new List<Vector2Int> { start };
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        var g = new Dictionary<Vector2Int, int> { [start] = 0 };
        var f = new Dictionary<Vector2Int, int> { [start] = H(start, goal) };

        while (open.Count > 0)
        {
            Vector2Int current = open[0];
            int bestF = f[current];
            for (int i = 1; i < open.Count; i++)
                if (f.TryGetValue(open[i], out int fi) && fi < bestF) { current = open[i]; bestF = fi; }

            if (current == goal) return Reconstruct(cameFrom, current);
            open.Remove(current);

            foreach (var d in dirs)
            {
                var n = current + d;
                if (n.x < 0 || n.x >= cells || n.y < 0 || n.y >= cells) continue;
                if (!CanMove(current, n)) continue;

                int tentative = g[current] + 1;
                if (!g.TryGetValue(n, out int gn) || tentative < gn)
                {
                    cameFrom[n] = current;
                    g[n] = tentative;
                    f[n] = tentative + H(n, goal);
                    if (!open.Contains(n)) open.Add(n);
                }
            }
        }
        return null; // unreachable
    }

    List<Vector2Int> Reconstruct(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int current)
    {
        var path = new List<Vector2Int> { current };
        while (cameFrom.ContainsKey(current)) { current = cameFrom[current]; path.Add(current); }
        path.Reverse();
        return path;
    }
}