using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class NPCAI : MonoBehaviour, IStunnable
{
    public enum Hostility { Friendly, Neutral, Hostile }

    [Header("Basics")]
    public Hostility hostility = Hostility.Neutral;
    public float tileSize = 1f;

    [Header("Territory")]
    public Transform home;
    public Transform wanderTo;
    public int wanderRadiusTiles = 4;
    public float idleTimeMin = 1.0f;
    public float idleTimeMax = 3.0f;

    [Header("Perception")]
    public int aggroRangeTiles = 6;
    public LayerMask lineOfSightBlockers = ~0;
    public float losHeight = 0.8f;

    [Header("Combat")]
    public float meleeCooldown = 1.8f;
    public int meleeDamage = 8;

    [Header("Refs")]
    public NPCMovement mover;
    public GridPathfinder pathfinder;
    public GameObject player; // assign at runtime if null
    public Animator animator; // optional animator

    [Header("Animation")]
    [Tooltip("Animator trigger to play when stunned.")]
    public string stunTriggerName = "Stun";

    // state
    float nextMeleeReady;
    Vector3 homeTile;
    NPCHealth hp;

    // extra state
    bool wanderRunning = false;
    bool isAttacking = false;

    // stun state
    bool isStunned = false;
    float stunUntil = 0f;

    void Awake()
    {
        if (!mover) mover = GetComponent<NPCMovement>();
        if (!pathfinder) pathfinder = FindAnyObjectByType<GridPathfinder>();
        if (!animator) animator = GetComponentInChildren<Animator>();
        hp = GetComponent<NPCHealth>();
    }

    void Start()
    {
        if (!player) player = GameObject.FindGameObjectWithTag("Player");
        homeTile = Snap((home ? home.position : transform.position));
        transform.position = homeTile;

        // Mark initial tile
        Vector2Int myTile = new Vector2Int(Mathf.RoundToInt(homeTile.x / tileSize),
                                        Mathf.RoundToInt(homeTile.z / tileSize));
        NPCTileRegistry.Register(myTile);
    }

    void Update()
    {
        // Stun gate
        if (isStunned)
        {
            if (Time.time >= stunUntil)
            {
                isStunned = false;
                DecideNextAction(); // resume AI
            }
            return; // fully frozen while stunned
        }

        if (!player) return;

        switch (hostility)
        {
            case Hostility.Friendly:
                DoFriendly();
                break;

            case Hostility.Neutral:
                DoWander();
                break;

            case Hostility.Hostile:
                DoHostile();
                break;
        }
    }

    // ---------- Behaviours ----------
    void DoFriendly() => DoWander();

    void DoHostile()
    {
        Vector3 playerTile = Snap(player.transform.position);

        // Already adjacent → attack (don’t move into player’s tile)
        if (DistanceTiles(transform.position, playerTile) <= 1.01f)
        {
            if (!isAttacking && Time.time >= nextMeleeReady)
            {
                isAttacking = true;
                nextMeleeReady = Time.time + meleeCooldown;
                TryHitPlayer();
                isAttacking = false;
            }
            // stop moving when adjacent
            mover.ClearPath();
        }
        else if (CanSeePlayer())
        {
            // Instead of moving INTO player, move to a free adjacent tile
            Vector3 targetTile = FindClosestAdjacentTile(playerTile);
            if (targetTile != Vector3.positiveInfinity)
            {
                TryPathTo(targetTile);
            }
        }
        else
        {
            if (!mover.IsMoving) ReturnHomeOrWander();
        }
    }

    Vector3 FindClosestAdjacentTile(Vector3 playerTile)
    {
        Vector3[] directions = new Vector3[]
        {
            new Vector3( tileSize, 0, 0),
            new Vector3(-tileSize, 0, 0),
            new Vector3(0, 0,  tileSize),
            new Vector3(0, 0, -tileSize),
            new Vector3( tileSize, 0,  tileSize),
            new Vector3(-tileSize, 0,  tileSize),
            new Vector3( tileSize, 0, -tileSize),
            new Vector3(-tileSize, 0, -tileSize)
        };

        Vector3 bestTile = Vector3.positiveInfinity;
        float bestDist = float.MaxValue;

        foreach (var dir in directions)
        {
            Vector3 candidate = playerTile + dir;
            if (pathfinder && !pathfinder.IsBlocked(candidate))
            {
                float d = DistanceTiles(transform.position, candidate);
                if (d < bestDist)
                {
                    bestDist = d;
                    bestTile = candidate;
                }
            }
        }

        return bestTile;
    }


    void DoWander()
    {
        if (mover.IsMoving || wanderRunning) return;
        StartCoroutine(WanderRoutine());
    }

    IEnumerator WanderRoutine()
    {
        wanderRunning = true;
        float wait = Random.Range(idleTimeMin, idleTimeMax);
        float tEnd = Time.time + wait;
        while (Time.time < tEnd) { yield return null; }

        var target = RandomReachableTileAround(homeTile, wanderRadiusTiles, 30);
        TryPathTo(target);

        yield return null;
        wanderRunning = false;
    }

    void ReturnHomeOrWander()
    {
        if (DistanceTiles(transform.position, homeTile) > 0.1f) TryPathTo(homeTile);
        else DoWander();
    }

    // ---------- Combat helpers ----------
    void TryHitPlayer()
    {
        if (!player) return;
        Vector3 pt = Snap(player.transform.position);
        if (DistanceTiles(transform.position, pt) <= 1.01f)
        {
            if (player.TryGetComponent<IDamageable>(out var dmg))
                dmg.ApplyDamage(meleeDamage);
        }
    }

    // ---------- Path / LOS ----------
    void TryPathTo(Vector3 worldTarget)
    {
        if (!pathfinder) return;
        if (pathfinder.TryFindPath(transform.position, worldTarget, 2000, out var path))
            mover.SetPath(path);
    }

    bool CanSeePlayer()
    {
        if (!player) return false;
        Vector3 a = transform.position + Vector3.up * losHeight;
        Vector3 b = player.transform.position + Vector3.up * losHeight;

        if (DistanceTiles(a, b) > aggroRangeTiles) return false;
        if (Physics.Linecast(a, b, lineOfSightBlockers, QueryTriggerInteraction.Ignore)) return false;

        return hostility == Hostility.Hostile;
    }

    public void BecomeHostile()
    {
        hostility = Hostility.Hostile;
        DecideNextAction();
    }

    public void HardStop()
    {
        if (mover) mover.ClearPath();
    }

    public void CancelAttack()
    {
        isAttacking = false;
        nextMeleeReady = Mathf.Max(nextMeleeReady, Time.time + 0.25f);
        // TODO: stop attack animation trigger if you add one
    }

    public void OnKnockbackEnd()
    {
        DecideNextAction();
    }

    public void DecideNextAction()
    {
        if (!player) return;

        if (hostility == Hostility.Hostile)
        {
            if (CanSeePlayer()) TryPathTo(Snap(player.transform.position));
            else ReturnHomeOrWander();
        }
        else
        {
            DoWander();
        }
    }

    // ---------- Stun (IStunnable) ----------
    /// <summary>
    /// Freeze this NPC for 'seconds'. Cancels movement and attacks, plays a stun animation trigger if assigned.
    /// </summary>
    public void ApplyStun(float seconds)
    {
        float dur = Mathf.Max(0f, seconds);
        if (dur <= 0f) return;

        isStunned = true;
        stunUntil = Mathf.Max(stunUntil, Time.time + dur);

        // STOP movement *immediately* (don’t let it finish the current step)
        if (mover) mover.HardStop();

        // stop wander coroutine if running
        StopAllCoroutines();
        wanderRunning = false;

        // cancel attack with tiny recovery
        CancelAttack();

        // Play stun animation trigger (placeholder)
        if (animator && !string.IsNullOrEmpty(stunTriggerName))
            animator.SetTrigger(stunTriggerName);
    }

    // ---------- Utils ----------
    Vector3 Snap(Vector3 p)
    {
        return new Vector3(
            Mathf.Round(p.x / tileSize) * tileSize,
            p.y,
            Mathf.Round(p.z / tileSize) * tileSize
        );
    }

    float DistanceTiles(Vector3 a, Vector3 b)
    {
        return Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.z - b.z)) / Mathf.Max(0.0001f, tileSize);
    }

    Vector3 RandomReachableTileAround(Vector3 center, int radius, int attempts)
    {
        for (int i = 0; i < attempts; i++)
        {
            int dx = Random.Range(-radius, radius + 1);
            int dz = Random.Range(-radius, radius + 1);
            Vector3 t = center + new Vector3(dx * tileSize, 0f, dz * tileSize);
            if (!pathfinder || !pathfinder.IsBlocked(t)) return t;
        }
        return center;
    }
}

public static class NPCTileRegistry
{
    private static readonly HashSet<Vector2Int> occupied = new();
    private static readonly HashSet<Vector2Int> reserved = new();

    /// <summary> A tile is blocked if it is currently occupied or reserved. </summary>
    public static bool IsBlocked(Vector2Int tile) => occupied.Contains(tile) || reserved.Contains(tile);

    /// <summary> Backwards-compat: returns only whether a tile is occupied. </summary>
    public static bool IsOccupied(Vector2Int tile) => occupied.Contains(tile);

    // Occupied
    public static void Register(Vector2Int tile) => occupied.Add(tile);
    public static void Unregister(Vector2Int tile) => occupied.Remove(tile);

    // Reserved
    public static void Reserve(Vector2Int tile) => reserved.Add(tile);
    public static void Unreserve(Vector2Int tile) => reserved.Remove(tile);

    /// <summary>
    /// Finalize a move: free 'from', occupy 'to', and clear any reservation on 'to'.
    /// </summary>
    public static void CommitReservation(Vector2Int from, Vector2Int to)
    {
        reserved.Remove(to);
        occupied.Remove(from);
        occupied.Add(to);
    }

    public static void ClearAll()
    {
        occupied.Clear();
        reserved.Clear();
    }
}