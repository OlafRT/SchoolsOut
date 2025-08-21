using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class NPCAI : MonoBehaviour, IStunnable
{
    public enum Hostility { Friendly, Neutral, Hostile }

    [Header("Faction / Class")]
    [Tooltip("Determines which abilities this NPC may use later, and can be used for faction logic.")]
    public NPCFaction faction = NPCFaction.Jock;
    [Tooltip("Optional display name for UI/plates.")]
    public string npcDisplayName = "Student";

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

    // ---- convenience for future abilities/UI ----
    public NPCFaction Faction => faction;
    public bool IsFaction(NPCFaction f) => faction == f;
    public PlayerStats.AbilitySchool? FactionSchool => faction.ToAbilitySchool();

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
            mover.ClearPath();
        }
        else if (CanSeePlayer())
        {
            // Move to a free adjacent tile near player (don’t stack on player tile)
            Vector3 targetTile = FindClosestAdjacentTile(playerTile);
            if (targetTile != Vector3.positiveInfinity) TryPathTo(targetTile);
        }
        else
        {
            if (!mover.IsMoving) ReturnHomeOrWander();
        }
    }

    Vector3 FindClosestAdjacentTile(Vector3 playerTile)
    {
        Vector3[] dirs =
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

        Vector3 best = Vector3.positiveInfinity;
        float bestDist = float.MaxValue;

        foreach (var d in dirs)
        {
            Vector3 cand = playerTile + d;
            if (pathfinder && !pathfinder.IsBlocked(cand))
            {
                float dist = DistanceTiles(transform.position, cand);
                if (dist < bestDist) { bestDist = dist; best = cand; }
            }
        }
        return best;
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
        // stop attack anim trigger here if you add one
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
    public void ApplyStun(float seconds)
    {
        float dur = Mathf.Max(0f, seconds);
        if (dur <= 0f) return;

        isStunned = true;
        stunUntil = Mathf.Max(stunUntil, Time.time + dur);

        if (mover) mover.HardStop();
        StopAllCoroutines();
        wanderRunning = false;
        CancelAttack();

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

    public NPCAI.Hostility GetHostilityTowards(NPCAI other)
    {
        return FactionRelations.GetRelation(this.faction, other.faction);
    }

    public NPCAI.Hostility GetHostilityTowardsPlayer(PlayerStats.AbilitySchool playerSchool)
    {
        // Convert player school to an NPCFaction for relation lookup
        NPCFaction pseudoFaction = playerSchool == PlayerStats.AbilitySchool.Jock
            ? NPCFaction.Jock
            : NPCFaction.Nerd;
        return FactionRelations.GetRelation(this.faction, pseudoFaction);
    }
}

// ---- existing registry stays as-is (unchanged) ----
public static class NPCTileRegistry
{
    private static readonly HashSet<Vector2Int> occupied = new();
    private static readonly HashSet<Vector2Int> reserved = new();

    public static bool IsBlocked(Vector2Int tile) => occupied.Contains(tile) || reserved.Contains(tile);
    public static bool IsOccupied(Vector2Int tile) => occupied.Contains(tile);

    public static void Register(Vector2Int tile) => occupied.Add(tile);
    public static void Unregister(Vector2Int tile) => occupied.Remove(tile);

    public static void Reserve(Vector2Int tile) => reserved.Add(tile);
    public static void Unreserve(Vector2Int tile) => reserved.Remove(tile);

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
