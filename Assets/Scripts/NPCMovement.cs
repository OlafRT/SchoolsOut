using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class NPCMovement : MonoBehaviour
{
    [Header("Grid")]
    public float tileSize = 1f;

    [Header("Speed")]
    [Tooltip("Tiles per second. 2.0 ≈ comfy walk; 3.0 ≈ jog; 5.0 ≈ sprint.")]
    public float tilesPerSecond = 2.0f;
    [Tooltip("Optional pause between tiles to give a step-y feel.")]
    public float pauseBetweenSteps = 0.0f;

    [Header("Refs")]
    public GridPathfinder pathfinder;

    bool isStepping;
    bool isForcedMove; // true while being knocked back / forced
    readonly Queue<Vector3> currentPath = new();

    // Occupancy tracking / reservations
    Vector2Int currentTile;
    bool hasReservedDest;
    Vector2Int reservedDestTile;

    // ---------- Speed modifiers ----------
    [SerializeField] private float externalSpeedMult = 1f;   // set by AI (walk/run)

    struct SpeedMod { public float mult; public float until; }
    readonly List<SpeedMod> speedMods = new();               // timed mods (slows/haste)
    readonly Dictionary<object, float> auraMods = new();     // auras keyed by source

    public void ApplySlow(float percent, float seconds)
    {
        float mult = Mathf.Clamp01(1f - Mathf.Clamp01(percent));
        ApplySpeedMultiplier(mult, seconds);
    }

    public void SetExternalSpeedMultiplier(float m)
    {
        externalSpeedMult = Mathf.Max(0.05f, m);
    }
    public float CurrentSpeedMultiplier() => externalSpeedMult;

    public void ApplySpeedMultiplier(float multiplier, float seconds)
    {
        float m = Mathf.Clamp(multiplier, 0.05f, 5f);
        float until = Time.time + Mathf.Max(0.01f, seconds);
        speedMods.Add(new SpeedMod { mult = m, until = until });
    }

    /// <summary>Set or update an aura (persists until cleared). multiplier=1 removes.</summary>
    public void SetAuraMultiplier(object sourceKey, float multiplier)
    {
        if (sourceKey == null) return;
        float m = Mathf.Clamp(multiplier, 0.05f, 5f);
        if (Mathf.Approximately(m, 1f)) { auraMods.Remove(sourceKey); return; }
        auraMods[sourceKey] = m;
    }

    /// <summary>Remove an aura for a given source.</summary>
    public void ClearAura(object sourceKey)
    {
        if (sourceKey == null) return;
        auraMods.Remove(sourceKey);
    }

    // Single source of truth
    public float EffectiveSpeedMultiplier()
    {
        float now = Time.time;

        // prune expired timed mods
        for (int i = speedMods.Count - 1; i >= 0; i--)
            if (speedMods[i].until <= now) speedMods.RemoveAt(i);

        float m = externalSpeedMult;

        // auras (persist)
        foreach (var kv in auraMods) m *= kv.Value;

        // timed mods
        for (int i = 0; i < speedMods.Count; i++) m *= speedMods[i].mult;

        return Mathf.Clamp(m, 0.05f, 5f);
    }
    // ------------------------------------

    void Awake()
    {
        if (!pathfinder) pathfinder = FindAnyObjectByType<GridPathfinder>();

        // Snap to grid and register initial tile
        Vector3 snap = Snap(transform.position);
        transform.position = snap;
        currentTile = WorldToTile(snap);
        NPCTileRegistry.Register(currentTile);
    }

    void OnDestroy()
    {
        if (hasReservedDest) NPCTileRegistry.Unreserve(reservedDestTile);
        NPCTileRegistry.Unregister(currentTile);
    }

    public void ClearPath() => currentPath.Clear();

    IEnumerator StepTo(Vector3 worldNext)
    {
        isStepping = true;

        // We reserved this before calling StepTo()
        // worldNext is already snapped by SetPath()

        Vector3 start = transform.position;
        Vector3 end   = Snap(worldNext);
        Vector2Int destTile = WorldToTile(end);

        // Face movement direction (8-dir snapped so visuals look consistent)
        Vector3 dir = (end - start); dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
        {
            Vector3 dir8 = SnapDirTo8(dir);
            transform.rotation = Quaternion.LookRotation(dir8, Vector3.up);
        }

        // Temporarily take control from physics while stepping
        Rigidbody rb = GetComponent<Rigidbody>();
        bool hadRB = rb != null;
        bool prevKinematic = false;
        if (hadRB)
        {
            prevKinematic = rb.isKinematic;
            rb.isKinematic = true;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Step duration based on tilesPerSecond with your active multipliers
        float distTiles = Vector2.Distance(
            new Vector2(start.x, start.z),
            new Vector2(end.x,   end.z)
        ) / Mathf.Max(0.0001f, tileSize);

        float t = 0f;
        float secondsPerTile = 1f / Mathf.Max(0.01f, tilesPerSecond * EffectiveSpeedMultiplier());
        float duration = Mathf.Max(0.01f, distTiles * secondsPerTile);

        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            transform.position = Vector3.Lerp(start, end, Mathf.Clamp01(t));
            yield return null;
        }
        transform.position = end;

        if (hadRB) rb.isKinematic = prevKinematic;

        // Release reservation & update occupancy
        if (hasReservedDest)
        {
            NPCTileRegistry.Unreserve(reservedDestTile);
            hasReservedDest = false;
        }

        // Move registry from currentTile -> destTile
        NPCTileRegistry.Unregister(currentTile);
        NPCTileRegistry.Register(destTile);
        currentTile = destTile;

        // Optional “step-y” feel
        if (pauseBetweenSteps > 0f)
            yield return new WaitForSeconds(pauseBetweenSteps);

        isStepping = false;

        // Continue along the queued path if any
        // (TryStepNext will peek the next node and repeat)
        TryStepNext();
    }

    public void SetPath(List<Vector3> worldCenters)
    {
        if (isForcedMove) return;

        // Reset any previous plan
        currentPath.Clear();

        if (worldCenters == null || worldCenters.Count == 0) return;

        // Snap our current position to the grid (center of the tile we occupy)
        float t = pathfinder ? pathfinder.tileSize : 1f;
        Vector3 here = new Vector3(
            Mathf.Round(transform.position.x / t) * t,
            transform.position.y,
            Mathf.Round(transform.position.z / t) * t
        );

        // Build a snapped, de-duplicated version of the incoming path
        // (keeps only center-to-center steps)
        List<Vector3> snapped = new List<Vector3>(worldCenters.Count + 1);
        Vector3 last = new Vector3(float.NaN, float.NaN, float.NaN);
        for (int i = 0; i < worldCenters.Count; i++)
        {
            Vector3 s = new Vector3(
                Mathf.Round(worldCenters[i].x / t) * t,
                here.y, // keep Y consistent
                Mathf.Round(worldCenters[i].z / t) * t
            );
            if (!float.IsNaN(last.x) && (s - last).sqrMagnitude < 0.0001f) continue; // skip duplicates
            snapped.Add(s);
            last = s;
        }

        // Ensure the path starts at our snapped tile.
        // If the first node isn't our tile, prepend 'here' so the first edge is center->center.
        if (snapped.Count == 0 || (snapped[0] - here).sqrMagnitude > 0.0001f)
            snapped.Insert(0, here);

        // If there's nowhere to go beyond our current tile, bail.
        if (snapped.Count < 2) return;

        // Enqueue steps starting AFTER our current tile
        for (int i = 1; i < snapped.Count; i++)
            currentPath.Enqueue(snapped[i]);

        // Start moving toward the first step
        TryStepNext();
    }

    public void MoveOneStepToward(Vector3 worldTarget)
    {
        if (isForcedMove) return;
        if (!pathfinder) return;

        if (pathfinder.TryFindPath(transform.position, worldTarget, 500, out var p))
        {
            if (p.Count > 1) SetPath(new List<Vector3> { p[0], p[1] });
        }
    }

    void Update()
    {
        if (!isStepping && !isForcedMove && currentPath.Count > 0)
            TryStepNext();
    }

    bool TryStepNext()
    {
        if (currentPath == null || currentPath.Count == 0) return false;

        Vector3 next = currentPath.Peek(); // don't dequeue yet

        // Snap our current position to the tile center for edge tests
        float t = pathfinder ? pathfinder.tileSize : 1f;
        Vector3 here = new Vector3(
            Mathf.Round(transform.position.x / t) * t,
            transform.position.y,
            Mathf.Round(transform.position.z / t) * t
        );

        Vector2Int nextTile = WorldToTile(next);

        // If next tile is blocked, clear so AI can replan immediately
        if (pathfinder && pathfinder.IsBlocked(next))
        {
            currentPath.Clear();
            return false;
        }

        // Use SNAPPED 'here' for edge check (prevents corner false negatives)
        if (pathfinder && pathfinder.IsEdgeBlocked(here, next))
        {
            currentPath.Clear();
            return false;
        }

        // Reserve destination tile before moving
        NPCTileRegistry.Reserve(nextTile);
        hasReservedDest  = true;
        reservedDestTile = nextTile;

        StartCoroutine(StepTo(next));
        return true;
    }

    // ---------------------------
    // Knockback / Forced movement
    // ---------------------------
    public void KnockbackTo(Vector3 worldDestination, float duration)
    {
        StopAllCoroutines();
        isStepping = false;

        if (hasReservedDest)
        {
            NPCTileRegistry.Unreserve(reservedDestTile);
            hasReservedDest = false;
        }

        if (TryGetComponent<NPCAI>(out var ai))
            ai.CancelAttack();

        StartCoroutine(ForcedMove(worldDestination, duration));
    }

    public void HardStop()
    {
        StopAllCoroutines();
        isStepping = false;
        isForcedMove = false;

        if (hasReservedDest)
        {
            NPCTileRegistry.Unreserve(reservedDestTile);
            hasReservedDest = false;
        }

        Vector3 snap = Snap(transform.position);
        transform.position = snap;

        Vector2Int snappedTile = WorldToTile(snap);
        if (snappedTile != currentTile)
        {
            NPCTileRegistry.Unregister(currentTile);
            NPCTileRegistry.Register(snappedTile);
            currentTile = snappedTile;
        }
    }

    IEnumerator ForcedMove(Vector3 worldDestination, float duration)
    {
        isForcedMove = true;

        Vector3 start = transform.position;
        Vector3 end = Snap(worldDestination);
        Vector2Int destTile = WorldToTile(end);

        Vector3 dir = (end - start); dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
        {
            Vector3 dir8 = SnapDirTo8(dir);
            transform.rotation = Quaternion.LookRotation(dir8, Vector3.up);
        }

        Rigidbody rb = GetComponent<Rigidbody>();
        bool hadRB = rb != null;
        bool prevKinematic = false;
        if (hadRB)
        {
            prevKinematic = rb.isKinematic;
            rb.isKinematic = true;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        float t = 0f;
        duration = Mathf.Max(0.01f, duration);
        while (t < 1f)
        {
            float tps = Mathf.Max(0.01f, tilesPerSecond * EffectiveSpeedMultiplier());
            t += Time.deltaTime / (1f / tps);
            transform.position = Vector3.Lerp(start, end, Mathf.Clamp01(t));
            yield return null;
        }
        transform.position = end;

        if (hadRB) rb.isKinematic = prevKinematic;

        if (hasReservedDest)
        {
            NPCTileRegistry.Unreserve(reservedDestTile);
            hasReservedDest = false;
        }

        NPCTileRegistry.Unregister(currentTile);
        NPCTileRegistry.Register(destTile);
        currentTile = destTile;

        isForcedMove = false;

        if (TryGetComponent<NPCAI>(out var ai))
            ai.OnKnockbackEnd();

        TryStepNext();
    }

    public bool IsMoving => isForcedMove || isStepping || currentPath.Count > 0;

    public Vector3 Snap(Vector3 p) =>
        new Vector3(Mathf.Round(p.x / tileSize) * tileSize, p.y, Mathf.Round(p.z / tileSize) * tileSize);

    Vector2Int WorldToTile(Vector3 world) =>
        new Vector2Int(Mathf.RoundToInt(world.x / tileSize), Mathf.RoundToInt(world.z / tileSize));

    public static Vector3 SnapDirTo8(Vector3 v)
    {
        v.y = 0f;
        float a = Mathf.Atan2(v.z, v.x) * Mathf.Rad2Deg; if (a < 0f) a += 360f;
        int step = Mathf.RoundToInt(a / 45f) % 8;
        return step switch
        {
            0 => new Vector3(1, 0, 0),
            1 => new Vector3(1, 0, 1).normalized,
            2 => new Vector3(0, 0, 1),
            3 => new Vector3(-1, 0, 1).normalized,
            4 => new Vector3(-1, 0, 0),
            5 => new Vector3(-1, 0, -1).normalized,
            6 => new Vector3(0, 0, -1),
            _ => new Vector3(1, 0, -1).normalized,
        };
    }
}
