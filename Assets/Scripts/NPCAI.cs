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
    public Hostility startingHostility = Hostility.Neutral; // default when calm
    public float tileSize = 1f;

    [Header("Auto Relations / Detection")]
    [Tooltip("If true, hostility can switch based on faction relations when enemies are visible.")]
    public bool autoRelations = true;
    [Tooltip("Tile radius to scan for faction-based relations each frame.")]
    public int detectRadiusTiles = 8;
    [Tooltip("Layer of NPCs for scanning allies/enemies.")]
    public LayerMask npcLayer = ~0;
    [Tooltip("Also consider the player for relations.")]
    public bool includePlayer = true;

    [Header("Assist / Aggro Propagation")]
    [Tooltip("Same-faction allies within this many tiles will aggro when one is hit by the player.")]
    public int allyAssistRadiusTiles = 8;
    [Tooltip("How long (seconds) manual hostility persists before re-evaluating auto relations.")]
    public float aggroLeashSeconds = 6f;

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

    // ---- convenience
    public NPCFaction Faction => faction;
    public PlayerStats.AbilitySchool? FactionSchool => faction.ToAbilitySchool();

    // ---- state
    Hostility runtimeHostility;
    Hostility manualHostility;
    bool hasManualHostility = false;
    float manualHostilityUntil = 0f;

    Transform attackTarget; // who we are hostile toward; can be player or another NPC

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

        // Register starting tile
        Vector2Int myTile = new Vector2Int(
            Mathf.RoundToInt(homeTile.x / tileSize),
            Mathf.RoundToInt(homeTile.z / tileSize));
        NPCTileRegistry.Register(myTile);

        runtimeHostility = startingHostility;
        if (!attackTarget && player) attackTarget = player.transform;
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

        // Update manual leash
        if (hasManualHostility && manualHostilityUntil != float.PositiveInfinity && Time.time >= manualHostilityUntil)
        {
            hasManualHostility = false; // leash expired
        }

        // Determine hostility/target for this frame
        UpdateRuntimeHostilityAndTarget();

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
    //  Hostility + Target
    // ======================

    void UpdateRuntimeHostilityAndTarget()
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

        runtimeHostility = startingHostility;
        Transform bestAssistTarget = null;

        float radius = Mathf.Max(1, detectRadiusTiles) * Mathf.Max(0.01f, tileSize);
        bool sawHostile = false;
        bool sawFriendly = false;

        // 1) Consider player
        if (includePlayer && player && WithinTiles(transform.position, player.transform.position, detectRadiusTiles) && HasLineOfSight(player.transform.position))
        {
            var ps = player.GetComponent<PlayerStats>();
            if (ps)
            {
                NPCFaction pf = ps.playerClass == PlayerStats.PlayerClass.Jock ? NPCFaction.Jock : NPCFaction.Nerd;
                var rel = FactionRelations.GetRelation(this.faction, pf);
                if (rel == Hostility.Hostile) { sawHostile = true; attackTarget = player.transform; }
                else if (rel == Hostility.Friendly) sawFriendly = true;
            }
        }

        // 2) Consider other NPCs
        var cols = Physics.OverlapSphere(transform.position, radius, npcLayer, QueryTriggerInteraction.Ignore);
        float bestDist = float.MaxValue;

        foreach (var c in cols)
        {
            if (!c || c.gameObject == this.gameObject) continue;
            if (!c.TryGetComponent<NPCAI>(out var other)) continue;

            if (!WithinTiles(transform.position, other.transform.position, detectRadiusTiles)) continue;
            if (!HasLineOfSight(other.transform.position)) continue;

            var rel = FactionRelations.GetRelation(this.faction, other.faction);

            // If we are friendly to the player, and we see an NPC whose faction is hostile to the player, assist player by attacking that NPC.
            if (includePlayer && player)
            {
                var ps = player.GetComponent<PlayerStats>();
                if (ps)
                {
                    NPCFaction pf = ps.playerClass == PlayerStats.PlayerClass.Jock ? NPCFaction.Jock : NPCFaction.Nerd;
                    var otherVsPlayer = FactionRelations.GetRelation(other.faction, pf);
                    bool weAreFriendlyToPlayer = (FactionRelations.GetRelation(this.faction, pf) == Hostility.Friendly);

                    if (weAreFriendlyToPlayer && otherVsPlayer == Hostility.Hostile)
                    {
                        float d = (other.transform.position - transform.position).sqrMagnitude;
                        if (d < bestDist) { bestDist = d; bestAssistTarget = other.transform; }
                    }
                }
            }

            if (rel == Hostility.Hostile)
            {
                sawHostile = true;
                if (!attackTarget) attackTarget = other.transform;
            }
            else if (rel == Hostility.Friendly)
            {
                sawFriendly = true;
            }
        }

        // Assist player if applicable
        if (bestAssistTarget)
        {
            runtimeHostility = Hostility.Hostile;
            attackTarget = bestAssistTarget;
            return;
        }

        // Priority: Hostile > Friendly > Neutral
        if (sawHostile) runtimeHostility = Hostility.Hostile;
        else if (sawFriendly) runtimeHostility = Hostility.Friendly;
        else runtimeHostility = Hostility.Neutral;

        // Default target to player if hostile and nothing else picked
        if (runtimeHostility == Hostility.Hostile && !attackTarget && player) attackTarget = player.transform;
    }

    // Public property for other scripts (e.g., health)
    public Hostility CurrentHostility
    {
        get
        {
            if (hasManualHostility) return manualHostility;
            return runtimeHostility;
        }
    }

    // Manual hostility override (with leash)
    public void BecomeHostile(float seconds = -1f, Transform target = null)
    {
        hasManualHostility = true;
        manualHostility = Hostility.Hostile;
        manualHostilityUntil = (seconds > 0f) ? Time.time + seconds : float.PositiveInfinity;
        if (target) attackTarget = target;
    }

    // Called by NPCHealth when damaged by the PLAYER
    public void OnDamagedByPlayer()
    {
        if (!player) player = GameObject.FindGameObjectWithTag("Player");
        BecomeHostile(aggroLeashSeconds, player ? player.transform : null);
        AlertAlliesOnAggro(allyAssistRadiusTiles, player ? player.transform : null);
    }

    // Alert same-faction allies to aggro the given target (usually player)
    public void AlertAlliesOnAggro(int radiusTiles, Transform focus)
    {
        float r = Mathf.Max(1, radiusTiles) * Mathf.Max(0.01f, tileSize);
        var cols = Physics.OverlapSphere(transform.position, r, npcLayer, QueryTriggerInteraction.Ignore);
        foreach (var c in cols)
        {
            if (!c || c.gameObject == this.gameObject) continue;
            if (!c.TryGetComponent<NPCAI>(out var ally)) continue;
            if (ally.faction != this.faction) continue;
            if (!ally.HasLineOfSight(this.transform.position)) continue;

            ally.BecomeHostile(ally.aggroLeashSeconds, focus);
        }
    }

    // ======================
    //  Behaviours
    // ======================

    void DoFriendly() => DoWander();

    void DoHostile()
    {
        Transform tgt = attackTarget ? attackTarget : (player ? player.transform : null);
        if (!tgt) { DoWander(); return; }

        Vector3 tgtTile = Snap(tgt.position);

        if (DistanceTiles(transform.position, tgtTile) <= 1.01f)
        {
            if (!isAttacking && Time.time >= nextMeleeReady)
            {
                isAttacking = true;
                nextMeleeReady = Time.time + meleeCooldown;
                TryHit(tgt);
                isAttacking = false;
            }
            mover.ClearPath();
        }
        else
        {
            if (WithinTiles(transform.position, tgt.position, aggroRangeTiles) && HasLineOfSight(tgt.position))
            {
                Vector3 targetTile = FindClosestAdjacentTile(tgtTile);
                if (targetTile != Vector3.positiveInfinity) TryPathTo(targetTile);
            }
            else
            {
                if (!mover.IsMoving) ReturnHomeOrWander();
            }
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
    //  Combat
    // ======================

    void TryHit(Transform tgt)
    {
        if (!tgt) return;
        Vector3 tt = Snap(tgt.position);
        if (DistanceTiles(transform.position, tt) <= 1.01f)
        {
            if (tgt.TryGetComponent<IDamageable>(out var dmg))
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

    bool HasLineOfSight(Vector3 worldTarget)
    {
        Vector3 a = transform.position + Vector3.up * losHeight;
        Vector3 b = worldTarget + Vector3.up * losHeight;
        return !Physics.Linecast(a, b, lineOfSightBlockers, QueryTriggerInteraction.Ignore);
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
        // will re-evaluate next Update
    }

    public void DecideNextAction() { /* behavior re-evaluated each Update */ }

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

    bool WithinTiles(Vector3 a, Vector3 b, int tiles)
    {
        float dx = Mathf.Abs(a.x - b.x);
        float dz = Mathf.Abs(a.z - b.z);
        float cheb = Mathf.Max(dx, dz) / Mathf.Max(0.01f, tileSize);
        return cheb <= tiles + 0.001f;
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

// ---- Tile registry (same as before) ----
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
