using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class NPCAI : MonoBehaviour, IStunnable
{
    public enum Hostility { Friendly, Neutral, Hostile }

    [Header("Faction / Class")]
    public NPCFaction faction = NPCFaction.Jock;
    public string npcDisplayName = "Student";

    [Header("Basics")]
    public Hostility startingHostility = Hostility.Neutral; // inspector default
    public float tileSize = 1f;

    [Header("Auto Hostility (Faction relationships)")]
    [Tooltip("If true, hostility will switch at runtime based on faction relations when enemies are visible.")]
    public bool autoRelations = true;
    [Tooltip("How many tiles away we can see enemies and react (before LOS check).")]
    public int detectRadiusTiles = 8;
    [Tooltip("Layer containing other NPCs for detection.")]
    public LayerMask npcLayer = ~0; // set to your NPC layer
    [Tooltip("Also consider the player as a potential target using their class (Jock/Nerd).")]
    public bool includePlayer = true;

    [Header("Territory")]
    public Transform home;
    public Transform wanderTo;
    public int wanderRadiusTiles = 4;
    public float idleTimeMin = 1.0f;
    public float idleTimeMax = 3.0f;

    [Header("Perception / LOS")]
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

    // ---- convenience for abilities/UI
    public NPCFaction Faction => faction;
    public bool IsFaction(NPCFaction f) => faction == f;
    public PlayerStats.AbilitySchool? FactionSchool => faction.ToAbilitySchool();

    // ---- state
    Hostility runtimeHostility;       // what we actually use for behavior each frame
    Hostility manualHostility;        // sticky hostility set by script (e.g., BecomeHostile on hit)
    bool hasManualHostility = false;  // if true, overrides autoRelations
    float nextMeleeReady;
    Vector3 homeTile;
    NPCHealth hp;

    bool wanderRunning = false;
    bool isAttacking = false;

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

        // Initial tile register
        Vector2Int myTile = new Vector2Int(
            Mathf.RoundToInt(homeTile.x / tileSize),
            Mathf.RoundToInt(homeTile.z / tileSize));
        NPCTileRegistry.Register(myTile);

        // seed runtime hostility
        runtimeHostility = startingHostility;
    }

    void Update()
    {
        // Stun gate
        if (isStunned)
        {
            if (Time.time >= stunUntil)
            {
                isStunned = false;
                DecideNextAction();
            }
            return;
        }

        // Determine hostility for this frame
        UpdateRuntimeHostility();

        switch (runtimeHostility)
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

    // ======================
    //  Auto-hostility logic
    // ======================

    void UpdateRuntimeHostility()
    {
        if (hasManualHostility)
        {
            runtimeHostility = manualHostility;
            return;
        }

        if (!autoRelations)
        {
            runtimeHostility = startingHostility;
            return;
        }

        // Scan for enemies within detect radius (tiles -> meters)
        float radius = Mathf.Max(1, detectRadiusTiles) * Mathf.Max(0.01f, tileSize);
        Hostility result = startingHostility;

        bool sawHostile = false;
        bool sawFriendly = false;

        // 1) Consider the Player
        if (includePlayer && player)
        {
            if (WithinTiles(transform.position, player.transform.position, detectRadiusTiles)
                && HasLineOfSight(player.transform.position))
            {
                var ps = player.GetComponent<PlayerStats>();
                if (ps)
                {
                    // Convert player school to pseudo-faction for relation
                    NPCFaction pf = ps.playerClass == PlayerStats.PlayerClass.Jock ? NPCFaction.Jock : NPCFaction.Nerd;
                    var rel = FactionRelations.GetRelation(this.faction, pf);
                    if (rel == Hostility.Hostile) sawHostile = true;
                    else if (rel == Hostility.Friendly) sawFriendly = true;
                }
            }
        }

        // 2) Consider other NPCs
        var cols = Physics.OverlapSphere(transform.position, radius, npcLayer, QueryTriggerInteraction.Ignore);
        foreach (var c in cols)
        {
            if (!c || c.gameObject == this.gameObject) continue;
            if (!c.TryGetComponent<NPCAI>(out var other)) continue;

            if (!WithinTiles(transform.position, other.transform.position, detectRadiusTiles)) continue;
            if (!HasLineOfSight(other.transform.position)) continue;

            var rel = FactionRelations.GetRelation(this.faction, other.faction);
            if (rel == Hostility.Hostile) sawHostile = true;
            else if (rel == Hostility.Friendly) sawFriendly = true;

            // Early out if we’ve seen a hostile already
            if (sawHostile) break;
        }

        // Priority: Hostile > Friendly > Neutral
        if (sawHostile) result = Hostility.Hostile;
        else if (sawFriendly) result = Hostility.Friendly;
        else result = Hostility.Neutral;

        runtimeHostility = result;
    }

    bool WithinTiles(Vector3 a, Vector3 b, int tiles)
    {
        float dx = Mathf.Abs(a.x - b.x);
        float dz = Mathf.Abs(a.z - b.z);
        float cheb = Mathf.Max(dx, dz) / Mathf.Max(0.01f, tileSize);
        return cheb <= tiles + 0.001f;
    }

    bool HasLineOfSight(Vector3 worldTarget)
    {
        Vector3 a = transform.position + Vector3.up * losHeight;
        Vector3 b = worldTarget + Vector3.up * losHeight;
        return !Physics.Linecast(a, b, lineOfSightBlockers, QueryTriggerInteraction.Ignore);
    }

    // ======================
    //  Behaviours
    // ======================

    void DoFriendly() => DoWander();

    void DoHostile()
    {
        if (!player) { DoWander(); return; }

        Vector3 pTile = Snap(player.transform.position);

        if (DistanceTiles(transform.position, pTile) <= 1.01f)
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
            Vector3 targetTile = FindClosestAdjacentTile(pTile);
            if (targetTile != Vector3.positiveInfinity) TryPathTo(targetTile);
        }
        else
        {
            if (!mover.IsMoving) ReturnHomeOrWander();
        }
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

    // ======================
    //  Combat helpers
    // ======================

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

    // ======================
    //  Path / LOS
    // ======================

    void TryPathTo(Vector3 worldTarget)
    {
        if (!pathfinder) return;
        if (pathfinder.TryFindPath(transform.position, worldTarget, 2000, out var path))
            mover.SetPath(path);
    }

    bool CanSeePlayer()
    {
        if (!player) return false;
        if (!WithinTiles(transform.position, player.transform.position, aggroRangeTiles)) return false;
        return HasLineOfSight(player.transform.position) && runtimeHostility == Hostility.Hostile;
    }

    Vector3 FindClosestAdjacentTile(Vector3 centerTile)
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
            Vector3 cand = centerTile + d;
            if (pathfinder && !pathfinder.IsBlocked(cand))
            {
                float dist = DistanceTiles(transform.position, cand);
                if (dist < bestDist) { bestDist = dist; best = cand; }
            }
        }
        return best;
    }

    // ======================
    //  Public controls
    // ======================

    public void BecomeHostile()
    {
        hasManualHostility = true;
        manualHostility = Hostility.Hostile;
        DecideNextAction();
    }

    public void BecomeNeutral()
    {
        hasManualHostility = true;
        manualHostility = Hostility.Neutral;
        DecideNextAction();
    }

    public void ClearManualHostility()
    {
        hasManualHostility = false;
    }

    public void HardStop()
    {
        if (mover) mover.ClearPath();
    }

    public void CancelAttack()
    {
        isAttacking = false;
        nextMeleeReady = Mathf.Max(nextMeleeReady, Time.time + 0.25f);
    }

    public void OnKnockbackEnd()
    {
        DecideNextAction();
    }

    public void DecideNextAction()
    {
        // Called after state changes (e.g., stun end, manual hostility changes)
        // Behavior branch in Update() uses runtimeHostility, which we recompute each frame.
    }

    // Expose current hostility used for behavior this frame.
    // If manual override is set (e.g., BecomeHostile on hit), it takes precedence.
    public Hostility CurrentHostility
    {
        get
        {
            if (hasManualHostility) return manualHostility;
            return runtimeHostility;
        }
    }

    // ======================
    //  Stun (IStunnable)
    // ======================

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

    // ======================
    //  Utils
    // ======================

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

// ---- Tile registry (unchanged from your version) ----
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
