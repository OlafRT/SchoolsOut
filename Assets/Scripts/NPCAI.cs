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

    [Header("Chase Speed")]
    public float wanderSpeedMultiplier = 1f;    // idle/wander speed
    public float chaseSpeedMultiplier  = 2f;    // run speed when hostile

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

    bool wanderRunning = false;
    bool isAttacking = false;

    bool isStunned = false;
    float stunUntil = 0f;

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
        homeTile = Snap((home ? home.position : transform.position));
        //transform.position = homeTile;

        // Register starting tile
        Vector2Int myTile = new Vector2Int(
            Mathf.RoundToInt(homeTile.x / tileSize),
            Mathf.RoundToInt(homeTile.z / tileSize));
        //NPCTileRegistry.Register(myTile);

        runtimeHostility = startingHostility;
        if (!attackTarget && player) attackTarget = player.transform;
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
                StopAllCoroutines();
                wanderRunning = false;
                // DO NOT clear the mover path here; DoHostile() will set it this frame.
            }

            lastHostility = runtimeHostility;
        }
        switch (runtimeHostility)
        {
            case Hostility.Friendly: DoFriendly(); break;
            case Hostility.Neutral:  DoWander();   break;
            case Hostility.Hostile:  DoHostile();  break;
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
        var cols = Physics.OverlapSphere(transform.position, radius, npcSenseLayer, QueryTriggerInteraction.Ignore);
        float bestDist = float.MaxValue;

        foreach (var c in cols)
        {
            if (!c || c.gameObject == this.gameObject) continue;
            if (!c.TryGetComponent<NPCAI>(out var other)) continue;

            if (!WithinTiles(transform.position, other.transform.position, detectRadiusTiles)) continue;
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
                if (!attackTarget) attackTarget = other.transform;
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

        // Default target to player if hostile and nothing else picked
        if (runtimeHostility == Hostility.Hostile && !attackTarget && player) attackTarget = player.transform;
    }

    // Public property for other scripts
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
        StopAllCoroutines();
        if (mover) mover.SetExternalSpeedMultiplier(chaseSpeedMultiplier);
        wanderRunning = false;
        if (mover) mover.ClearPath();
        if (!player) player = GameObject.FindGameObjectWithTag("Player");
        BecomeHostile(aggroLeashSeconds, player ? player.transform : null);
        AlertAlliesOnAggro(allyAssistRadiusTiles, player ? player.transform : null);
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
        if (!player || mover == null || pathfinder == null) return;

        // --- Always log once per hostile tick so we can see what's happening indoors/outdoors
        var here    = transform.position;
        var tgtTile = Snap(player.transform.position);
        float distT = DistanceTiles(here, tgtTile);

        // debug log
        LogAI($"{name} HOSTILE TICK  distTiles={distT:0.00}  LOS={HasLineOfSight(player.transform.position)}");

        // --- Try to attack if we're close enough by grid OR physical reach
        bool closeByGrid    = distT <= 1.01f;
        bool closeByPhysics = Vector2.Distance(
            new Vector2(here.x, here.z),
            new Vector2(player.transform.position.x, player.transform.position.z)
        ) <= Mathf.Max(0.45f, 0.55f * tileSize);

        if (closeByGrid || closeByPhysics)
        {
            // melee cooldown is already enforced elsewhere in your code
            TryHit(player.transform);
            return;
        }

        // If we already have a path and we're mid-step, let the mover finish it this frame.
        if (mover.IsMoving) return;

        // --- UNCONDITIONAL CHASE WHILE HOSTILE ---
        // 1) Robust: full path to the player's tile; walk to last-1 so we stop adjacent.
        bool foundFull = pathfinder.TryFindPath(here, tgtTile, 2000, out var full, true);
        if (foundFull && full != null && full.Count >= 2)
        {
            // Drop the final (player) tile and give the WHOLE remaining path to the mover
            full.RemoveAt(full.Count - 1);
            mover.SetPath(full);                 // <-- use the solved path, no re-path
            return;
        }

        // 2) Greedy one-step: pick any legal neighbor that reduces distance
        var neigh = pathfinder.OpenNeighbors(here);
        if (neigh != null && neigh.Count > 0)
        {
            Vector3 best = here; float bestD = float.MaxValue;
            foreach (var n in neigh)
            {
                float d = DistanceTiles(n, tgtTile);
                if (d < bestD) { bestD = d; best = n; }
            }
            if (best != here)
            {
                TryPathTo(best);
                LogAI($"{name} CHASE -> greedy step {best}");
                return;
            }
        }

        // 3) Last-ditch: axis step toward the player if legal
        float dx = Mathf.Sign(tgtTile.x - here.x);
        float dz = Mathf.Sign(tgtTile.z - here.z);
        Vector3 candX = Snap(here + new Vector3(dx * tileSize, 0, 0));
        Vector3 candZ = Snap(here + new Vector3(0, 0, dz * tileSize));

        if (!pathfinder.IsBlocked(candX) && !pathfinder.IsEdgeBlocked(here, candX))
        {
            TryPathTo(candX);
            LogAI($"{name} CHASE -> axis X");
            return;
        }
        if (!pathfinder.IsBlocked(candZ) && !pathfinder.IsEdgeBlocked(here, candZ))
        {
            TryPathTo(candZ);
            LogAI($"{name} CHASE -> axis Z");
            return;
        }

        // If literally no legal step, clear so we can re-evaluate next tick
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

        if (animator && !string.IsNullOrEmpty(attackTriggerName))
            animator.SetTrigger(attackTriggerName);

        // wait for “hit moment”
        if (attackWindupSeconds > 0f)
            yield return new WaitForSeconds(attackWindupSeconds);

        if (tgt && tgt.TryGetComponent<IDamageable>(out var dmg))
            dmg.ApplyDamage(meleeDamage);

        // small recovery so it doesn’t instantly snap back into stepping
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
