using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class NPCAI : MonoBehaviour, IStunnable
{
    #if UNITY_EDITOR
    public bool debugAI = true;
    #endif
    public enum Hostility { Friendly, Neutral, Hostile }
    
    [Header("Debug")]
    public bool showDebug = true;
    public Color gizmoLOSColorOK = Color.green;
    public Color gizmoLOSColorBlocked = Color.red;
    public bool debugChase = true;
    [Tooltip("If true, prints AI tick/chase logs to the Console.")]
    public bool logAI = false;

    [Header("Faction / Class")]
    public NPCFaction faction = NPCFaction.Jock;
    public string npcDisplayName = "Student";

    [Header("Basics")]
    public Hostility startingHostility = Hostility.Neutral; // default when calm
    public float tileSize = 1f;
    NPCAI.Hostility lastHostility;

    [Header("Auto Relations / Detection")]
    [Tooltip("If true, hostility can switch based on faction relations when enemies are visible.")]
    public bool autoRelations = true;
    [Tooltip("Tile radius to scan for faction-based relations each frame.")]
    public int detectRadiusTiles = 8;
    [Tooltip("Max Y separation (in tiles) before a target is ignored. Prevents NPCs on floors far above/below from aggroing.")]
    public int detectYRangeTiles = 4;
    [Tooltip("Layer of NPCs for scanning allies/enemies (set to your NPC layer, e.g., 'Target').")]
    public LayerMask npcSenseLayer = ~0;
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
    [Tooltip("How long (seconds) the NPC keeps searching after losing line of sight.")]
    public float chaseMemorySeconds = 6f;
    [Tooltip("Tile radius around the last known position the NPC explores while searching.")]
    public int searchRadiusTiles = 5;
    [Tooltip("Pause (seconds) between search waypoints.")]
    public float searchPauseMin = 0.3f;
    public float searchPauseMax = 0.8f;

    [Header("Chase Speed")]
    public float wanderSpeedMultiplier = 1f;    // idle/wander speed
    public float chaseSpeedMultiplier  = 2f;    // run speed when hostile

    [Header("Combat")]
    public float meleeCooldown = 1.8f;
    public int meleeDamage = 8;
    [Tooltip("Max Y difference (metres) allowed for a melee hit. Prevents hitting targets on floors above/below.")]
    public float attackYDeltaMax = 2.0f;
    [Tooltip("How fast (degrees/sec) the enemy rotates to face the target before attacking. 0 = instant snap.")]
    public float attackFacingSpeed = 720f;

    [Header("Refs")]
    public NPCMovement mover;
    public GridPathfinder pathfinder;
    public GameObject player; // assign at runtime if null
    public Animator animator; // optional animator

    [Header("Animation")]
    [Tooltip("Animator trigger to play when stunned.")]
    public string stunTriggerName = "Stun";

    [Header("Animation (Locomotion)")]
    [Tooltip("Float parameter used by a 0..1 blend tree (0=Idle, 1=Walk).")]
    public string locomotionSpeedParam = "Speed01";

    [Header("Animation (Locomotion)")]
    public string isMovingBool = "IsMoving";
    public string isRunningBool = "IsRunning";

    [Tooltip("Tiles/sec at which we consider it a 'walk'.")]
    public float walkTilesPerSecond = 2.0f;

    [Tooltip("Tiles/sec at which we consider it a 'run'.")]
    public float runTilesPerSecond = 4.0f;

    [Header("Animation (Combat)")]
    public string attackTriggerName = "Attack";
    public bool lockMovementDuringAttack = true;
    public float attackWindupSeconds = 0.15f;   // time before damage applies
    public float attackRecoverSeconds = 0.35f;  // time after damage before AI continues

    [Header("Animation (Idle Variants)")]
    public bool enableRandomIdleVariants = true;
    public float idleVariantMinSeconds = 6f;
    public float idleVariantMaxSeconds = 14f;
    public string talkTriggerName = "Talk";
    public string phoneTriggerName = "Phone";
    public string danceTriggerName = "Dance";
    [Range(0f, 1f)] public float talkChance = 0.45f;
    [Range(0f, 1f)] public float phoneChance = 0.35f;
    // remaining chance becomes Dance

    float stillTime = 0f;
    float nextIdleVariantAt = 0f;
    Coroutine attackRoutine;

    // ---- convenience
    public NPCFaction Faction => faction;
    public PlayerStats.AbilitySchool? FactionSchool => faction.ToAbilitySchool();

    // ---- state
    Hostility runtimeHostility;
    Hostility manualHostility;
    bool hasManualHostility = false;
    float manualHostilityUntil = 0f;

    Transform attackTarget; // can be player or another NPC

    float nextMeleeReady;
    Vector3 homeTile;
    NPCHealth hp;
    PlayerHealth playerHealth;

    bool wanderRunning = false;
    bool isAttacking = false;

    bool isStunned = false;
    float stunUntil = 0f;

    // ── LOS memory / search (inline — no coroutine) ───────────────────────────
    bool    hasLastKnownPos   = false;
    Vector3 lastKnownTargetPos;
    float   losLostAt         = float.NegativeInfinity;
    float   searchPauseUntil  = 0f;   // rate-limits waypoint picking when at last-known-pos

    // ── Flee override (used by NPCCallForBackup) ──────────────────────────────
    // When fleeActive is true, DoHostile() paths toward fleeTarget instead of the
    // player, but NPCAI.Update() keeps running so locomotion bools stay live.
    bool    fleeActive    = false;
    Vector3 fleeTarget    = Vector3.zero;

    // When aiSuspended is true, the hostility switch is skipped entirely (no
    // pathfinding decisions) but UpdateAnimatorLocomotion() still runs every frame.
    // Use this during the phone-call animation so the mover's stop naturally
    // clears IsMoving/IsRunning before the trigger fires.
    bool aiSuspended = false;

    void LogAI(string msg)
    {
        if (logAI) UnityEngine.Debug.Log(msg);
    }

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
        if (player) playerHealth = player.GetComponent<PlayerHealth>();
        homeTile = Snap((home ? home.position : transform.position));
        //transform.position = homeTile;

        // Register starting tile
        Vector2Int myTile = new Vector2Int(
            Mathf.RoundToInt(homeTile.x / tileSize),
            Mathf.RoundToInt(homeTile.z / tileSize));
        //NPCTileRegistry.Register(myTile);

        runtimeHostility = startingHostility;
        // Note: attackTarget is intentionally NOT defaulted to the player here.
        // DoHostile resolves the target each tick with a faction check.
        lastHostility = runtimeHostility;
    }

    void Update()
    {
        // Stun gate
        if (isStunned)
        {
            if (Time.time >= stunUntil)
            {
                isStunned = false;
                // fall through
            }
            else return;
        }

        // Update manual leash expiry
        if (hasManualHostility && manualHostilityUntil != float.PositiveInfinity && Time.time >= manualHostilityUntil)
        {
            hasManualHostility = false; // leash expired
        }

        // Determine hostility/target for this frame
        UpdateRuntimeHostilityAndTarget();

        // Kill any wander the instant we become Hostile
        if (runtimeHostility != lastHostility)
        {
            if (mover)
            {
                mover.SetExternalSpeedMultiplier(
                    runtimeHostility == Hostility.Hostile ? chaseSpeedMultiplier : wanderSpeedMultiplier
                );
            }
            if (runtimeHostility == Hostility.Hostile)
            {
                StopAICoroutines();
                // DO NOT clear the mover path here; DoHostile() will set it this frame.
            }

            lastHostility = runtimeHostility;
        }
        if (!aiSuspended)
        {
            switch (runtimeHostility)
            {
                case Hostility.Friendly: DoFriendly(); break;
                case Hostility.Neutral:  DoWander();   break;
                case Hostility.Hostile:  DoHostile();  break;
            }
        }

        // --- Locomotion param (Idle/Walk) ---
        //if (animator && !string.IsNullOrEmpty(locomotionSpeedParam))
        //{
        //    float s = (mover && mover.IsMoving) ? 1f : 0f;
        //    animator.SetFloat(locomotionSpeedParam, s);
        //}

        UpdateAnimatorLocomotion();
        UpdateIdleVariants();
        
        if (logAI && player)
        {
            bool los = HasLineOfSight(player.transform.position);
            float distTiles = DistanceTiles(transform.position, Snap(player.transform.position));
            LogAI($"{name}  H={CurrentHostility}  LOS={los}  distTiles={distTiles:0.00}  moving={(mover && mover.IsMoving)}");
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
        if (includePlayer && player && WithinTiles(transform.position, player.transform.position, detectRadiusTiles)
            && Mathf.Abs(transform.position.y - player.transform.position.y) <= detectYRangeTiles * tileSize
            && HasLineOfSight(player.transform.position))
        {
            var ps = player.GetComponent<PlayerStats>();
            if (ps && !(playerHealth && playerHealth.IsDead))   // don't aggro a dead player
            {
                NPCFaction pf = ps.playerClass == PlayerStats.PlayerClass.Jock ? NPCFaction.Jock : NPCFaction.Nerd;
                var rel = FactionRelations.GetRelation(this.faction, pf);
                if (rel == Hostility.Hostile) { sawHostile = true; attackTarget = player.transform;
                    lastKnownTargetPos = player.transform.position; hasLastKnownPos = true; losLostAt = float.NegativeInfinity; }
                else if (rel == Hostility.Friendly) sawFriendly = true;
            }
        }

        // 2) Consider other NPCs
        var cols = Physics.OverlapSphere(transform.position, radius, npcSenseLayer, QueryTriggerInteraction.Ignore);
        float bestDist = float.MaxValue;

        foreach (var c in cols)
        {
            if (!c || c.gameObject == this.gameObject) continue;
            if (!c.TryGetComponent<NPCAI>(out var other)) continue;

            if (!WithinTiles(transform.position, other.transform.position, detectRadiusTiles)) continue;
            if (Mathf.Abs(transform.position.y - other.transform.position.y) > detectYRangeTiles * tileSize) continue;
            if (!HasLineOfSight(other.transform.position)) continue;

            var rel = FactionRelations.GetRelation(this.faction, other.faction);

            // If we are friendly to the player and we see a hostile-to-player NPC, assist player → target that NPC.
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
                // Never reassign away from the player via passive scanning.
                // If a Jock is already chasing the player and spots a Nerd NPC,
                // it should keep chasing the player — only switch to an NPC target
                // when explicitly attacked (handled by OnDamagedByNPC / BecomeHostile).
                bool currentTargetIsPlayer = attackTarget != null && player != null
                                             && attackTarget == player.transform;
                if (!currentTargetIsPlayer)
                {
                    float d = (other.transform.position - transform.position).sqrMagnitude;
                    if (!attackTarget || d < (attackTarget.position - transform.position).sqrMagnitude)
                        attackTarget = other.transform;
                }
            }
            else if (rel == Hostility.Friendly)
            {
                sawFriendly = true;
            }
        }

        // Make assist STICK: enter manual hostile leash targeting that hostile NPC
        if (bestAssistTarget)
        {
            BecomeHostile(aggroLeashSeconds, bestAssistTarget);
            runtimeHostility = Hostility.Hostile;
            return;
        }

        // Priority: Hostile > Friendly > Neutral
        // Priority: Hostile > Friendly > fallback to starting hostility
        if (sawHostile)      runtimeHostility = Hostility.Hostile;
        else if (sawFriendly) runtimeHostility = Hostility.Friendly;
        else                 runtimeHostility = startingHostility; // make the npc return to start hostility

        // ── LOS memory: stay hostile for chaseMemorySeconds after losing sight ─
        if (!sawHostile && hasLastKnownPos && chaseMemorySeconds > 0f)
        {
            if (losLostAt < 0f) losLostAt = Time.time;
            if (Time.time - losLostAt < chaseMemorySeconds)
                runtimeHostility = Hostility.Hostile;
            else
                hasLastKnownPos = false;
        }
        else if (sawHostile)
            losLostAt = float.NegativeInfinity;

        // Default target to player if hostile and nothing else picked
        if (runtimeHostility == Hostility.Hostile && !attackTarget && player) attackTarget = player.transform;
    }

    // Public property for other scripts
    /// <summary>The current attack/chase target. May be a Jock NPC, not the player.</summary>
    public Transform AttackTarget => attackTarget;

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
        StopAICoroutines();
        if (mover) mover.SetExternalSpeedMultiplier(chaseSpeedMultiplier);
        if (mover) mover.ClearPath();
        if (!player) player = GameObject.FindGameObjectWithTag("Player");
        BecomeHostile(aggroLeashSeconds, player ? player.transform : null);
        AlertAlliesOnAggro(allyAssistRadiusTiles, player ? player.transform : null);
    }

    /// <summary>Called when hit by another NPC. Aggroes toward the attacker, never the player.</summary>
    public void OnDamagedByNPC(NPCAI attacker)
    {
        if (!attacker) return;
        StopAICoroutines();
        if (mover) mover.SetExternalSpeedMultiplier(chaseSpeedMultiplier);
        if (mover) mover.ClearPath();
        BecomeHostile(aggroLeashSeconds, attacker.transform);
        AlertAlliesOnAggro(allyAssistRadiusTiles, attacker.transform);
    }

    // Alert same-faction allies to aggro the given target (usually player)
    public void AlertAlliesOnAggro(int radiusTiles, Transform focus)
    {
        float r = Mathf.Max(1, radiusTiles) * Mathf.Max(0.01f, tileSize);
        var cols = Physics.OverlapSphere(transform.position, r, npcSenseLayer, QueryTriggerInteraction.Ignore);
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
        if (mover == null || pathfinder == null) return;
        if (isAttacking) return;

        // ── Flee override ─────────────────────────────────────────────────────
        if (fleeActive)
        {
            if (mover.IsMoving) return;
            if (pathfinder.TryFindPath(transform.position, fleeTarget, 2000, out var fleePath, goalPassable: true)
                && fleePath != null && fleePath.Count >= 2)
                mover.SetPath(fleePath);
            return;
        }
        // ─────────────────────────────────────────────────────────────────────

        // Resolve the actual target (may be an NPC, not the player)
        Transform target = attackTarget;

        // If our NPC target died, clear and re-evaluate next frame — do NOT default
        // to the player, or a friendly Nerd will snap to targeting the player.
        if (target != null && (player == null || target != player.transform))
        {
            var tgtHp = target.GetComponent<NPCHealth>();
            if (tgtHp != null && tgtHp.IsDead)
            {
                hasManualHostility = false;
                attackTarget       = null;
                runtimeHostility   = startingHostility;
                // Clear memory so the LOS-memory block doesn't keep us hostile
                // and accidentally send us after the player next frame.
                hasLastKnownPos    = false;
                losLostAt          = float.NegativeInfinity;
                searchPauseUntil   = 0f;
                if (mover) mover.ClearPath();
                return;
            }
        }

        // Fall back to player only when no explicit target is set AND this faction is hostile to the player
        if (!target && player)
        {
            var ps = player.GetComponent<PlayerStats>();
            if (ps != null)
            {
                NPCFaction pf = ps.playerClass == PlayerStats.PlayerClass.Jock ? NPCFaction.Jock : NPCFaction.Nerd;
                if (FactionRelations.GetRelation(this.faction, pf) == Hostility.Hostile)
                    target = player.transform;
            }
            else
            {
                target = player.transform; // no PlayerStats — default to targeting player
            }
        }
        if (!target) return;

        // Player died — drop aggro
        if (target == (player ? player.transform : null) && playerHealth && playerHealth.IsDead)
        {
            hasManualHostility = false;
            runtimeHostility = startingHostility;
            attackTarget = null;
            if (mover) mover.ClearPath();
            return;
        }

        var here = transform.position;
        bool hasLOS = HasLineOfSight(target.position);

        // Keep last-known-pos fresh every frame we can actually see the target
        if (hasLOS)
        {
            lastKnownTargetPos = target.position;
            hasLastKnownPos    = true;
            losLostAt          = float.NegativeInfinity;
        }

        // ── Choose what to path toward ────────────────────────────────────────
        // With LOS  → chase the real target.
        // Without LOS but memory active → head for last known pos, then sweep around it.
        // Without LOS and no memory → memory block already cleared hasLastKnownPos; we
        //   just fall through with tgtTile = target's actual position so the existing
        //   "no legal step" path handles the stuck case gracefully.
        Vector3 tgtTile;
        if (hasLOS || !hasLastKnownPos)
        {
            tgtTile = Snap(target.position);
        }
        else
        {
            Vector3 lastKnown = Snap(lastKnownTargetPos);
            if (DistanceTiles(here, lastKnown) > 1.5f)
            {
                // Still en route to last known pos — keep heading there
                tgtTile = lastKnown;
            }
            else
            {
                // Arrived at last known pos; sweep nearby tiles to search
                // searchPauseUntil rate-limits waypoint picking to once per pause window
                if (Time.time < searchPauseUntil)
                    return; // sit tight during the hesitation beat

                tgtTile = Snap(RandomReachableTileAround(lastKnownTargetPos, searchRadiusTiles, 10));
                searchPauseUntil = Time.time + Random.Range(searchPauseMin, searchPauseMax);
            }
        }

        float distT = DistanceTiles(here, tgtTile);
        LogAI($"{name} HOSTILE TICK  distTiles={distT:0.00}  LOS={hasLOS}");

        // Melee range check — only attempt to hit when we actually have LOS
        if (hasLOS)
        {
            bool closeByGrid    = distT <= 1.01f;
            bool closeByPhysics = Vector2.Distance(
                new Vector2(here.x, here.z),
                new Vector2(target.position.x, target.position.z)
            ) <= Mathf.Max(0.45f, 0.55f * tileSize);

            if (closeByGrid || closeByPhysics)
            {
                TryHit(target);
                return;
            }
        }

        // Mid-step — let the mover finish before we re-path
        if (mover.IsMoving) return;

        // --- PATH TO tgtTile (same cascade as direct chase) ---
        // 1) Full A* path; goalPassable so occupied tiles are accepted as goal
        bool foundFull = pathfinder.TryFindPath(here, tgtTile, 2000, out var full, true);
        if (foundFull && full != null && full.Count >= 2)
        {
            if (hasLOS) full.RemoveAt(full.Count - 1); // stop one tile short of live target
            mover.SetPath(full);
            return;
        }

        // 2) Greedy one-step toward tgtTile
        var neigh = pathfinder.OpenNeighbors(here);
        if (neigh != null && neigh.Count > 0)
        {
            Vector3 best = here; float bestD = float.MaxValue;
            foreach (var n in neigh)
            {
                float d = DistanceTiles(n, tgtTile);
                if (d < bestD) { bestD = d; best = n; }
            }
            if (best != here) { TryPathTo(best); LogAI($"{name} CHASE -> greedy step {best}"); return; }
        }

        // 3) Last-ditch axis step
        float dx = Mathf.Sign(tgtTile.x - here.x);
        float dz = Mathf.Sign(tgtTile.z - here.z);
        Vector3 candX = Snap(here + new Vector3(dx * tileSize, 0, 0));
        Vector3 candZ = Snap(here + new Vector3(0, 0, dz * tileSize));

        if (!pathfinder.IsBlocked(candX) && !pathfinder.IsEdgeBlocked(here, candX))
            { TryPathTo(candX); LogAI($"{name} CHASE -> axis X"); return; }
        if (!pathfinder.IsBlocked(candZ) && !pathfinder.IsEdgeBlocked(here, candZ))
            { TryPathTo(candZ); LogAI($"{name} CHASE -> axis Z"); return; }

        mover.ClearPath();
        LogAI($"{name} CHASE -> no legal step (cleared)");
    }


    void DoWander()
    {
        if (mover) mover.SetExternalSpeedMultiplier(wanderSpeedMultiplier);
        if (mover.IsMoving || wanderRunning) return;
        StartCoroutine(WanderRoutine());
    }

    IEnumerator WanderRoutine()
    {
        wanderRunning = true;
        float wait = Random.Range(idleTimeMin, idleTimeMax);
        float tEnd = Time.time + wait;
        while (Time.time < tEnd) { yield return null; }

        // If we became hostile (or anything not calm) while waiting, abort
        if (runtimeHostility != Hostility.Neutral && runtimeHostility != Hostility.Friendly)
        {
            wanderRunning = false;
            yield break;
        }

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

    void UpdateAnimatorLocomotion()
    {
        if (!animator || !mover) return;

        bool moving = mover.IsMoving && !isAttacking;

        // Current actual tiles/sec
        float tps = mover.tilesPerSecond * mover.EffectiveSpeedMultiplier();

        // Build a nice 0..1 value with a "walk midpoint" at 0.5
        float speed01 = 0f;
        bool running = false;

        if (moving)
        {
            if (tps <= walkTilesPerSecond)
            {
                // 0..walk -> 0..0.5
                speed01 = 0.5f * Mathf.Clamp01(tps / Mathf.Max(0.01f, walkTilesPerSecond));
            }
            else
            {
                // walk..run -> 0.5..1
                float denom = Mathf.Max(0.01f, (runTilesPerSecond - walkTilesPerSecond));
                float x = Mathf.Clamp01((tps - walkTilesPerSecond) / denom);
                speed01 = 0.5f + 0.5f * x;
            }

            // Running should basically mean: hostile chase speed OR high movement speed
            running = (runtimeHostility == Hostility.Hostile) || (tps >= (walkTilesPerSecond * 1.25f));
        }

        if (!string.IsNullOrEmpty(locomotionSpeedParam))
            animator.SetFloat(locomotionSpeedParam, speed01);

        if (!string.IsNullOrEmpty(isMovingBool))
            animator.SetBool(isMovingBool, moving);

        if (!string.IsNullOrEmpty(isRunningBool))
            animator.SetBool(isRunningBool, running);
    }

    void UpdateIdleVariants()
    {
        if (!enableRandomIdleVariants || !animator) return;

        // Only do fun idles when calm + not moving + not attacking + not stunned
        bool calm = (runtimeHostility == Hostility.Neutral || runtimeHostility == Hostility.Friendly);
        bool canIdleVariant = calm && !isAttacking && mover != null && !mover.IsMoving && !isStunned;

        if (!canIdleVariant)
        {
            stillTime = 0f;
            nextIdleVariantAt = 0f;
            return;
        }

        stillTime += Time.deltaTime;

        if (nextIdleVariantAt <= 0f)
            nextIdleVariantAt = Random.Range(idleVariantMinSeconds, idleVariantMaxSeconds);

        if (stillTime < nextIdleVariantAt) return;

        // pick one of (Talk/Phone/Dance)
        float r = Random.value;
        if (r < talkChance)
        {
            if (!string.IsNullOrEmpty(talkTriggerName)) animator.SetTrigger(talkTriggerName);
        }
        else if (r < talkChance + phoneChance)
        {
            if (!string.IsNullOrEmpty(phoneTriggerName)) animator.SetTrigger(phoneTriggerName);
        }
        else
        {
            if (!string.IsNullOrEmpty(danceTriggerName)) animator.SetTrigger(danceTriggerName);
        }

        stillTime = 0f;
        nextIdleVariantAt = Random.Range(idleVariantMinSeconds, idleVariantMaxSeconds);
    }


    // ======================
    //  Combat
    // ======================

    void TryHit(Transform tgt)
    {
        if (!tgt) return;

        // Y-axis gate: prevents hits through floors/ceilings
        if (Mathf.Abs(transform.position.y - tgt.position.y) > attackYDeltaMax) return;

        // LOS gate: no hitting through walls
        if (!HasLineOfSight(tgt.position)) return;

        // distance gates (keep yours)
        bool closeByGrid = DistanceTiles(transform.position, Snap(tgt.position)) <= 1.01f;
        Vector2 me = new Vector2(transform.position.x, transform.position.z);
        Vector2 him = new Vector2(tgt.position.x, tgt.position.z);
        float reach = Mathf.Max(0.45f, 0.55f * tileSize);
        bool closeByPhysics = Vector2.Distance(me, him) <= reach;

        if (!(closeByGrid || closeByPhysics)) return;

        // cooldown gate
        if (Time.time < nextMeleeReady) return;
        nextMeleeReady = Time.time + meleeCooldown;

        // start attack routine (anim + delayed damage)
        if (attackRoutine != null) StopCoroutine(attackRoutine);
        attackRoutine = StartCoroutine(MeleeRoutine(tgt));
    }

    IEnumerator MeleeRoutine(Transform tgt)
    {
        if (!tgt) yield break;

        isAttacking = true;

        if (lockMovementDuringAttack && mover)
            mover.HardStop();

        // Phase 1: rotate to face the target BEFORE the animation fires.
        // Spins until within 5 degrees, with a hard timeout so it never hangs.
        float facingTimeout = attackFacingSpeed <= 0f ? 0f : (180f / attackFacingSpeed) + 0.1f;
        float facingElapsed = 0f;
        while (facingElapsed < facingTimeout)
        {
            if (!tgt) break;
            Vector3 toTarget = tgt.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.0001f)
            {
                Quaternion desired = Quaternion.LookRotation(toTarget);
                if (attackFacingSpeed <= 0f)
                {
                    transform.rotation = desired;
                    break;
                }
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, desired, attackFacingSpeed * Time.deltaTime);
                if (Quaternion.Angle(transform.rotation, desired) < 5f)
                {
                    transform.rotation = desired;
                    break;
                }
            }
            facingElapsed += Time.deltaTime;
            yield return null;
        }

        // Phase 2: animation, windup, damage, recovery.
        if (animator && !string.IsNullOrEmpty(attackTriggerName))
            animator.SetTrigger(attackTriggerName);

        if (attackWindupSeconds > 0f)
            yield return new WaitForSeconds(attackWindupSeconds);

        if (tgt)
        {
            bool targetDead = false;
            if (player != null && tgt == player.transform)
                targetDead = (playerHealth != null && playerHealth.IsDead);
            else if (tgt.TryGetComponent<NPCHealth>(out var tgtNpcHp))
                targetDead = tgtNpcHp.IsDead;

            if (!targetDead)
            {
                // NPC targets: ApplyDamageFromNPC so they aggro US, not the player.
                // Player target: IDamageable.ApplyDamage as before.
                var tgtNpcHealth = (player == null || tgt != player.transform)
                    ? tgt.GetComponent<NPCHealth>() : null;

                if (tgtNpcHealth != null)
                    tgtNpcHealth.ApplyDamageFromNPC(meleeDamage, this);
                else if (tgt.TryGetComponent<IDamageable>(out var dmg))
                    dmg.ApplyDamage(meleeDamage);
            }
        }

        if (attackRecoverSeconds > 0f)
            yield return new WaitForSeconds(attackRecoverSeconds);

        isAttacking = false;
        attackRoutine = null;
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
        if (!pathfinder) return Vector3.positiveInfinity;

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
        float bestCost = float.MaxValue;

        foreach (var d in dirs)
        {
            Vector3 cand = centerTile + d;

            // quick reject: solid wall or NPC sitting there
            if (pathfinder.IsBlocked(cand)) continue;

            // IMPORTANT: only accept candidates we can actually path to
            if (pathfinder.TryFindPath(transform.position, cand, 2000, out var p) && p != null && p.Count > 0)
            {
                // prefer the cheapest/shortest path
                float cost = p.Count;
                if (cost < bestCost) { bestCost = cost; best = cand; }
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

    // ── Flee API (called by NPCCallForBackup) ─────────────────────────────────

    /// <summary>
    /// Makes DoHostile() path toward worldGoal instead of the player.
    /// NPCAI.Update() keeps running so locomotion, animation bools, and
    /// speed multipliers all stay live — movement looks natural (she runs).
    /// </summary>
    public void StartFlee(Vector3 worldGoal)
    {
        fleeTarget = worldGoal;
        fleeActive = true;
        // Make sure she runs while fleeing
        if (mover) mover.SetExternalSpeedMultiplier(chaseSpeedMultiplier);
    }

    /// <summary>Stop the flee redirect; DoHostile() resumes chasing the player.</summary>
    public void StopFlee()
    {
        fleeActive = false;
    }

    /// <summary>
    /// Suspends all pathfinding decisions (the hostility switch) while keeping
    /// UpdateAnimatorLocomotion() alive. Use this during the phone-call animation
    /// so that when the mover stops, IsMoving/IsRunning clear automatically and
    /// the Phone trigger fires into the correct Idle state.
    /// </summary>
    public void SuspendDecisions(bool suspend)
    {
        aiSuspended = suspend;
        if (suspend && mover) mover.HardStop();
    }
    // ─────────────────────────────────────────────────────────────────────────

    public void CancelAttack()
    {
        isAttacking = false;
        nextMeleeReady = Mathf.Max(nextMeleeReady, Time.time + 0.25f);
    }

    // Single choke-point for killing all AI coroutines.
    // MUST be used instead of bare StopAllCoroutines() so isAttacking and
    // wanderRunning are always kept consistent — a bare StopAllCoroutines()
    // mid-MeleeRoutine leaves isAttacking=true forever, permanently freezing
    // the NPC since DoHostile() exits immediately when isAttacking is set.
    void StopAICoroutines()
    {
        StopAllCoroutines();
        isAttacking   = false;
        attackRoutine = null;
        wanderRunning = false;
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
        StopAICoroutines();
        CancelAttack(); // also bumps nextMeleeReady so it can't instant-attack on unstun

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

    void OnDrawGizmosSelected()
    {
        if (!showDebug || player == null) return;

        // draw LOS ray at losHeight
        Vector3 a = transform.position + Vector3.up * losHeight;
        Vector3 b = player.transform.position + Vector3.up * losHeight;

        bool losClear = !Physics.Linecast(a, b, lineOfSightBlockers, QueryTriggerInteraction.Ignore);
        Gizmos.color = losClear ? gizmoLOSColorOK : gizmoLOSColorBlocked;
        Gizmos.DrawLine(a, b);

        // draw melee reach sphere we’ll use below
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(new Vector3(transform.position.x, 0, transform.position.z), Mathf.Max(0.45f, 0.55f * tileSize));

        // tiny label that shows hostility

        // Visualize adjacent candidates the AI tries
        Vector3 pc = Snap(player.transform.position);
        Vector3[] dirs = {
            new Vector3( tileSize,0,0),  new Vector3(-tileSize,0,0),
            new Vector3(0,0, tileSize),  new Vector3(0,0,-tileSize),
            new Vector3( tileSize,0, tileSize),  new Vector3(-tileSize,0, tileSize),
            new Vector3( tileSize,0,-tileSize),  new Vector3(-tileSize,0,-tileSize)
        };
        foreach (var d in dirs)
        {
            Vector3 cand = pc + d;
            bool blocked = pathfinder.IsBlocked(cand);
            Color c = blocked ? Color.red : Color.cyan;
            Gizmos.color = c;
            Gizmos.DrawWireCube(cand + Vector3.up * 0.05f, new Vector3(tileSize*0.9f, 0.01f, tileSize*0.9f));
        }
    #if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 1.8f, $"H: {runtimeHostility}");
    #endif
    }
}

// ---- Tile registry ----
public static class NPCTileRegistry
{
    // Ref-counted: multiple NPCs can share a tile without incorrectly clearing it
    // when only one of them leaves (e.g. two NPCs spawned on the same tile at startup).
    private static readonly Dictionary<Vector2Int, int> occupied = new();
    private static readonly HashSet<Vector2Int> reserved = new();

    public static bool IsBlocked(Vector2Int tile) => occupied.ContainsKey(tile) || reserved.Contains(tile);
    public static bool IsOccupied(Vector2Int tile) => occupied.ContainsKey(tile);

    public static void Register(Vector2Int tile)
    {
        occupied.TryGetValue(tile, out int count);
        occupied[tile] = count + 1;
    }

    public static void Unregister(Vector2Int tile)
    {
        if (!occupied.TryGetValue(tile, out int count)) return;
        if (count <= 1) occupied.Remove(tile);
        else            occupied[tile] = count - 1;
    }

    public static void Reserve(Vector2Int tile) => reserved.Add(tile);
    public static void Unreserve(Vector2Int tile) => reserved.Remove(tile);

    public static void CommitReservation(Vector2Int from, Vector2Int to)
    {
        reserved.Remove(to);
        Unregister(from);
        Register(to);
    }

    public static void ClearAll()
    {
        occupied.Clear();
        reserved.Clear();
    }
}