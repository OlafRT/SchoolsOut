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
        Vector3 origin = snapped + Vector3.up * Mathf.Max(0.3f, checkHalfExtents.y * 0.8f);
        return Physics.CheckBox(origin, checkHalfExtents, Quaternion.identity, obstacleLayer, QueryTriggerInteraction.Ignore);
    }

    public bool IsTileBlocked(Vector3 worldCenter) => IsBlocked(worldCenter);

    public bool IsEdgeBlocked(Vector3 fromWorld, Vector3 toWorld)
    {
        Vector3 a = Snap(fromWorld);
        Vector3 b = Snap(toWorld);

        // Mid-edge between A and B
        Vector3 mid    = (a + b) * 0.5f + Vector3.up * Mathf.Max(0.3f, checkHalfExtents.y * 0.8f);

        // Edge-aligned check box (slimmer along travel axis)
        Vector3 half = checkHalfExtents;
        if (Mathf.Abs(a.x - b.x) > Mathf.Abs(a.z - b.z))
            half.z *= 0.35f; // moving horizontally → slim on Z
        else if (Mathf.Abs(a.z - b.z) > 0f)
            half.x *= 0.35f; // moving vertically → slim on X
        // diagonal: keep default; the corner test below will gate it

        return Physics.CheckBox(mid, half, Quaternion.identity, obstacleLayer, QueryTriggerInteraction.Ignore);
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
        Vector3 c = Snap(center);

        // 4-way first
        Vector3[] card =
        {
            c + new Vector3( t,0, 0),
            c + new Vector3(-t,0, 0),
            c + new Vector3( 0,0, t),
            c + new Vector3( 0,0,-t),
        };

        foreach (var n in card)
            if (!IsTileBlocked(n) && !IsEdgeBlocked(c, n))
                yield return n;

        // Diagonals — only if BOTH adjacent cardinals are free
        (Vector3 diag, Vector3 sideA, Vector3 sideB)[] diags =
        {
            (c + new Vector3( t,0, t), c + new Vector3( t,0, 0), c + new Vector3( 0,0, t)),
            (c + new Vector3(-t,0, t), c + new Vector3(-t,0, 0), c + new Vector3( 0,0, t)),
            (c + new Vector3( t,0,-t), c + new Vector3( t,0, 0), c + new Vector3( 0,0,-t)),
            (c + new Vector3(-t,0,-t), c + new Vector3(-t,0, 0), c + new Vector3( 0,0,-t)),
        };

        foreach (var d in diags)
        {
            if (IsTileBlocked(d.diag)) continue;
            // corner rule: both orthogonals must be free AND their edges must be free
            if (IsTileBlocked(d.sideA) || IsTileBlocked(d.sideB)) continue;
            if (IsEdgeBlocked(c, d.sideA) || IsEdgeBlocked(c, d.sideB)) continue;
            // final: also ensure diagonal edge itself has no thin wall
            if (IsEdgeBlocked(c, d.diag)) continue;

            yield return d.diag;
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
    public bool TryFindPath(
    Vector3 startWorld,
    Vector3 goalWorld,
    int maxNodes,
    out List<Vector3> path,
    bool goalPassable = false)
    {
        path = null;

        Vector3 start = Snap(startWorld);
        Vector3 goal  = Snap(goalWorld);

        // Only hard-reject a blocked goal when we are NOT allowed to treat it as passable
        if (!goalPassable && IsBlocked(goal)) return false;

        var open   = new SimplePriorityQueue<Vector3>();
        var came   = new Dictionary<Vector3, Vector3>();
        var g      = new Dictionary<Vector3, float>();
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

            // *** NEW: if the goal is treated as passable, we can stop when we are ADJACENT to it. ***
            if (current == goal || (goalPassable && ChebyshevTiles(current, goal) <= 1f))
            {
                // reconstruct to 'current'
                var core = Reconstruct(came, start, current);

                // append the goal so callers that trim the last node keep behavior identical
                if (core.Count == 0 || core[^1] != goal)
                    core.Add(goal);

                path = core;
                return true;
            }

            closed.Add(current);
            expansions++;

            foreach (var n in Neighbors(current))
            {
                if (closed.Contains(n)) continue;

                // Normal neighbor rules already exclude blocked/illegal edges;
                // we don't need to special-case the goal here any more.

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

    // helper used above
    float ChebyshevTiles(Vector3 a, Vector3 b)
    {
        float dx = Mathf.Abs(a.x - b.x) / Mathf.Max(0.0001f, tileSize);
        float dz = Mathf.Abs(a.z - b.z) / Mathf.Max(0.0001f, tileSize);
        return Mathf.Max(dx, dz);
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

    // Public helper: open, legal neighbors from a world position (snapped internally)
    public List<Vector3> OpenNeighbors(Vector3 center)
    {
        var list = new List<Vector3>();
        float t = tileSize;
        Vector3 c = Snap(center);

        Vector3[] card =
        {
            c + new Vector3( t,0, 0),
            c + new Vector3(-t,0, 0),
            c + new Vector3( 0,0, t),
            c + new Vector3( 0,0,-t),
        };

        foreach (var n in card)
            if (!IsTileBlocked(n) && !IsEdgeBlocked(c, n))
                list.Add(n);

        (Vector3 diag, Vector3 sideA, Vector3 sideB)[] diags =
        {
            (c + new Vector3( t,0, t), c + new Vector3( t,0, 0), c + new Vector3( 0,0, t)),
            (c + new Vector3(-t,0, t), c + new Vector3(-t,0, 0), c + new Vector3( 0,0, t)),
            (c + new Vector3( t,0,-t), c + new Vector3( t,0, 0), c + new Vector3( 0,0,-t)),
            (c + new Vector3(-t,0,-t), c + new Vector3(-t,0, 0), c + new Vector3( 0,0,-t)),
        };

        foreach (var d in diags)
        {
            if (IsTileBlocked(d.diag)) continue;
            if (IsTileBlocked(d.sideA) || IsTileBlocked(d.sideB)) continue;
            if (IsEdgeBlocked(c, d.sideA) || IsEdgeBlocked(c, d.sideB)) continue;
            if (IsEdgeBlocked(c, d.diag)) continue;
            list.Add(d.diag);
        }

        return list;
    }
}