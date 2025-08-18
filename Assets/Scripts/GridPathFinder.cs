using UnityEngine;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class GridPathfinder : MonoBehaviour
{
    [Header("Grid / Collision")]
    public float tileSize = 1f;
    public LayerMask obstacleLayer = ~0;
    public Vector3 checkHalfExtents = new Vector3(0.4f, 0.6f, 0.4f);

    /// <summary>
    /// Returns true if a world grid-center is blocked by walls OR by an NPC occupying that tile.
    /// </summary>
    public bool IsBlocked(Vector3 worldCenter)
    {
        // Snap to grid -> convert to tile coordinates for occupancy check
        Vector3 snapped = Snap(worldCenter);
        Vector2Int tile = new Vector2Int(
            Mathf.RoundToInt(snapped.x / tileSize),
            Mathf.RoundToInt(snapped.z / tileSize));

        // 🔹 Treat NPC-occupied tiles as blocked
        if (NPCTileRegistry.IsOccupied(tile))
            return true;

        // 🔹 World collision (walls, etc.)
        Vector3 origin = snapped + Vector3.up * checkHalfExtents.y;
        return Physics.CheckBox(origin, checkHalfExtents, Quaternion.identity, obstacleLayer, QueryTriggerInteraction.Ignore);
    }

    public Vector3 Snap(Vector3 p)
    {
        return new Vector3(
            Mathf.Round(p.x / tileSize) * tileSize,
            p.y,
            Mathf.Round(p.z / tileSize) * tileSize
        );
    }

    // 8-neighbors (diagonals allowed). If you want 4-way, remove the diagonal entries.
    IEnumerable<Vector3> Neighbors(Vector3 center)
    {
        float t = tileSize;
        Vector3[] dirs =
        {
            new Vector3( t,0, 0), new Vector3(-t,0, 0),
            new Vector3( 0,0, t), new Vector3( 0,0,-t),
            new Vector3( t,0, t), new Vector3(-t,0, t),
            new Vector3( t,0,-t), new Vector3(-t,0,-t),
        };

        foreach (var d in dirs)
        {
            Vector3 n = center + d;
            if (!IsBlocked(n)) yield return n;
        }
    }

    // Chebyshev distance (tiles) for 8-way
    float Heuristic(Vector3 a, Vector3 b)
    {
        float dx = Mathf.Abs(a.x - b.x) / Mathf.Max(0.0001f, tileSize);
        float dz = Mathf.Abs(a.z - b.z) / Mathf.Max(0.0001f, tileSize);
        return Mathf.Max(dx, dz);
    }

    /// <summary>
    /// A* search over grid centers (snapped). Returns centers including the goal.
    /// </summary>
    public bool TryFindPath(Vector3 startWorld, Vector3 goalWorld, int maxNodes, out List<Vector3> path)
    {
        path = null;

        Vector3 start = Snap(startWorld);
        Vector3 goal  = Snap(goalWorld);

        // Quick reject: goal blocked (by wall or occupied)
        if (IsBlocked(goal)) return false;

        // A* structures
        var open = new SimplePriorityQueue<Vector3>();
        var came = new Dictionary<Vector3, Vector3>();
        var g    = new Dictionary<Vector3, float>();
        var inOpen = new HashSet<Vector3>();
        var closed = new HashSet<Vector3>();

        g[start] = 0f;
        open.Enqueue(start, 0f);
        inOpen.Add(start);

        int expansions = 0;

        while (open.Count > 0 && expansions < maxNodes)
        {
            var current = open.Dequeue();
            inOpen.Remove(current);
            if (current == goal)
            {
                path = Reconstruct(came, start, goal);
                return true;
            }

            closed.Add(current);
            expansions++;

            foreach (var n in Neighbors(current))
            {
                if (closed.Contains(n)) continue;

                float step = Vector3.Distance(current, n) / Mathf.Max(0.0001f, tileSize); // 1 or 1.414
                float tentative = g[current] + step;

                if (!g.TryGetValue(n, out float oldG) || tentative < oldG)
                {
                    g[n] = tentative;
                    came[n] = current;
                    float f = tentative + Heuristic(n, goal);

                    if (!inOpen.Contains(n))
                    {
                        open.Enqueue(n, f);
                        inOpen.Add(n);
                    }
                    else
                    {
                        open.UpdatePriority(n, f);
                    }
                }
            }
        }

        return false;
    }

    List<Vector3> Reconstruct(Dictionary<Vector3, Vector3> came, Vector3 start, Vector3 goal)
    {
        var list = new List<Vector3>();
        var cur = goal;
        list.Add(cur);
        while (came.ContainsKey(cur))
        {
            cur = came[cur];
            list.Add(cur);
        }
        list.Reverse();
        return list;
    }

    // --- tiny updatable priority queue ---
    class SimplePriorityQueue<T>
    {
        readonly List<(T item, float pri)> items = new();
        public int Count => items.Count;

        public void Enqueue(T item, float pri) { items.Add((item, pri)); }

        public T Dequeue()
        {
            int bi = 0; float bp = items[0].pri;
            for (int i = 1; i < items.Count; i++)
                if (items[i].pri < bp) { bp = items[i].pri; bi = i; }
            var it = items[bi].item;
            items.RemoveAt(bi);
            return it;
        }

        public void UpdatePriority(T item, float pri)
        {
            // simple linear search; fine for small grids
            for (int i = 0; i < items.Count; i++)
            {
                if (EqualityComparer<T>.Default.Equals(items[i].item, item))
                {
                    items[i] = (item, pri);
                    return;
                }
            }
            // not found, add
            Enqueue(item, pri);
        }
    }
}