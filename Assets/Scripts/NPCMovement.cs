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

    // Occupancy tracking
    Vector2Int currentTile;
    bool hasReservedDest;
    Vector2Int reservedDestTile;

    // ---------- Speed modifiers (slows / buffs) ----------
    struct SpeedMod { public float mult; public float until; }
    readonly List<SpeedMod> speedMods = new();

    public void ApplySlow(float percent, float seconds)
    {
        // percent = 0.4 → multiplier 0.6
        float mult = Mathf.Clamp01(1f - Mathf.Clamp01(percent));
        ApplySpeedMultiplier(mult, seconds);
    }

    public void ApplySpeedMultiplier(float multiplier, float seconds)
    {
        float m = Mathf.Clamp(multiplier, 0.05f, 5f);
        float until = Time.time + Mathf.Max(0.01f, seconds);
        speedMods.Add(new SpeedMod { mult = m, until = until });
    }

    float CurrentSpeedMultiplier()
    {
        float now = Time.time;
        float m = 1f;
        for (int i = speedMods.Count - 1; i >= 0; i--)
        {
            if (speedMods[i].until <= now) speedMods.RemoveAt(i);
        }
        for (int i = 0; i < speedMods.Count; i++)
        {
            m *= speedMods[i].mult; // multiplicative stacking
        }
        return Mathf.Clamp(m, 0.05f, 5f);
    }
    // -----------------------------------------------------

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

    public void ClearPath()
    {
        currentPath.Clear();
    }

    public void SetPath(List<Vector3> worldCenters)
    {
        if (isForcedMove) return;

        currentPath.Clear();
        if (worldCenters == null || worldCenters.Count == 0) return;

        Vector3 selfSnap = Snap(transform.position);
        for (int i = 0; i < worldCenters.Count; i++)
        {
            Vector3 step = Snap(worldCenters[i]);
            if (i == 0 && step == selfSnap) continue;
            currentPath.Enqueue(step);
        }

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

    void TryStepNext()
    {
        if (isStepping || isForcedMove || currentPath.Count == 0) return;

        Vector3 next = currentPath.Peek();
        Vector2Int nextTile = WorldToTile(next);

        if (NPCTileRegistry.IsBlocked(nextTile) || (pathfinder && pathfinder.IsBlocked(next)))
            return;

        NPCTileRegistry.Reserve(nextTile);
        hasReservedDest = true;
        reservedDestTile = nextTile;

        StartCoroutine(StepTo(next));
    }

    IEnumerator StepTo(Vector3 end)
    {
        isStepping = true;

        Vector3 start = transform.position;
        end = Snap(end);
        Vector2Int destTile = WorldToTile(end);

        // face 8-way
        Vector3 dir = (end - start); dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
        {
            Vector3 dir8 = SnapDirTo8(dir);
            transform.rotation = Quaternion.LookRotation(dir8, Vector3.up);
        }

        // progress-based step so slows affect mid-step
        float progress = 0f;
        while (progress < 1f)
        {
            float tps = Mathf.Max(0.01f, tilesPerSecond * CurrentSpeedMultiplier());
            progress += Time.deltaTime * tps;                 // tiles per second normalized to 1 per step
            transform.position = Vector3.Lerp(start, end, Mathf.Clamp01(progress));
            yield return null;
        }
        transform.position = end;

        if (hasReservedDest && reservedDestTile == destTile)
        {
            NPCTileRegistry.CommitReservation(currentTile, destTile);
            hasReservedDest = false;
        }
        else
        {
            NPCTileRegistry.Unregister(currentTile);
            NPCTileRegistry.Register(destTile);
        }
        currentTile = destTile;

        if (currentPath.Count > 0) currentPath.Dequeue();

        if (pauseBetweenSteps > 0f) yield return new WaitForSeconds(pauseBetweenSteps);

        isStepping = false;
        TryStepNext();
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
            float tps = Mathf.Max(0.01f, tilesPerSecond * CurrentSpeedMultiplier());
            t += Time.deltaTime / (1f / tps); // keep similar feel
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
