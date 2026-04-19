using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Main state machine for the Cafeteria Lady boss fight.
///
/// REQUIRED ON SAME GAMEOBJECT:
///   NPCHealth (handles HP, death anim, CorpseLoot — no extra work needed)
///
/// DOES NOT REQUIRE:
///   NPCAI / NPCMovement — the boss uses direct scripted movement.
///
/// SETUP CHECKLIST:
///   1. Assign centerPos, leftCookPos, rightCookPos (empty Transforms in the scene).
///   2. Assign potTransform (the pot mesh/object whose Y scale grows).
///   3. Assign arenaCenter + arenaHalfExtents so the pot throw lands in the right area.
///   4. Assign bossHealthBar (your BossHealthBar UI prefab instance in the scene).
///   5. Assign bombPrefab (same arc projectile used by NPCBombAbility).
///   6. Assign slimeMinionPrefab + spawnPoints (one per vent exit).
///   7. Set up the Animator with triggers below (or leave blank to skip animations).
/// </summary>
[DisallowMultipleComponent]
public class CafeteriaLadyBoss : MonoBehaviour
{
    // ────────────────────────────────────────────────
    public enum BossState
    {
        Inactive,
        MovingToSide,
        Cooking,
        ReturningToCenter,
        AddingToPot,
        ThrowingPot,
        Dead
    }

    public BossState CurrentState { get; private set; } = BossState.Inactive;

    // ────────────────────────────────────────────────
    [Header("Core Refs")]
    public NPCHealth health;
    public Animator animator;
    public BossHealthBar bossHealthBar;
    [Tooltip("The hazard zone near the lady — deactivated when she dies so the corpse can be looted.")]
    public BossHazardZone hazardZone;
    [Tooltip("GameObjects to enable when the boss dies (e.g. reward chests, exit doors, cutscene triggers).")]
    public GameObject[] enableOnDeath;
    [Tooltip("GameObjects to disable when the boss dies (e.g. arena barriers, phase FX, intro blockers).")]
    public GameObject[] disableOnDeath;

    [Header("Positions (empty Transforms in scene)")]
    [Tooltip("Where she stands when not cooking — behind/in front of the pot.")]
    public Transform centerPos;
    [Tooltip("Left cooking station.")]
    public Transform leftCookPos;
    [Tooltip("Right cooking station.")]
    public Transform rightCookPos;
    [Tooltip("Where she stands to laugh and throw the pot.")]
    public Transform throwPos;
    [Tooltip("Optional waypoints she passes through when walking TO the throw position. "
           + "Use these to route her around obstacles. Leave empty to walk direct.")]
    public Transform[] throwPathWaypoints;
    [Tooltip("Optional waypoints she passes through when walking BACK from the throw position. "
           + "Leave empty to use throwPathWaypoints in reverse.")]
    public Transform[] returnPathWaypoints;

    [Header("Movement")]
    [Tooltip("World-units per second she slides between positions.")]
    public float moveSpeed = 2.5f;

    [Header("Timing")]
    [Tooltip("How long she 'cooks' at each side station before returning.")]
    public float cookDuration = 3.5f;
    [Tooltip("Seconds she spends doing the AddToPot animation.")]
    public float addToPotAnimDuration = 1.8f;
    [Tooltip("Seconds she spends doing the Throw animation (wind-up + release).")]
    public float throwAnimDuration = 1.5f;

    [Header("Pot")]
    [Tooltip("Transform of the pot's content object — its Y localScale is grown each trip.")]
    public Transform potTransform;
    [Tooltip("Y localScale when the pot is empty.")]
    public float potEmptyScale = 0f;
    [Tooltip("Y localScale when the pot is full (i.e., ready to throw).")]
    public float potFullScale = 1f;
    [Tooltip("Amount added to Y scale per completed cooking trip. 0.25 → 4 trips to fill.")]
    public float potFillPerTrip = 0.25f;

    [Header("Add To Pot")]
    [Tooltip("She will face this Transform when doing the AddToPot animation. "
           + "Drag in the pot or counter Transform so she looks at it.")]
    public Transform potFaceTarget;
    [Tooltip("Prefab dropped into the pot during AddToPot (e.g. a piece of meat/slime). "
           + "Should have a Rigidbody — auto-destroyed after dropLifetime seconds.")]
    public GameObject ingredientDropPrefab;
    [Tooltip("Spawn offset above the pot center.")]
    public Vector3 ingredientSpawnOffset = new Vector3(0f, 0.8f, 0f);
    [Tooltip("Seconds after spawning before the ingredient is destroyed.")]
    public float ingredientDropLifetime = 0.6f;
    [Tooltip("Seconds into the AddToPot animation before the ingredient is dropped.")]
    public float ingredientDropDelay = 0.4f;

    [Header("Pot Throw — Projectile")]
    [Tooltip("Bomb arc prefab (same as NPCBombAbility uses).")]
    public GameObject bombPrefab;
    public GameObject explosionVfxPrefab;
    [Tooltip("How fast the pot travels (seconds per tile).")]
    public float bombThrowTimePerTile = 0.08f;
    public float bombArcHeight = 2.5f;
    [Tooltip("The hand bone or socket Transform the projectile launches from. "
           + "If null, falls back to the pot on the counter.")]
    public Transform throwOrigin;

    [Header("Pot Throw — Landing AoE")]
    public int potAoERadius = 2;
    [Tooltip("Seconds to show the ground telegraph before the pot lands.")]
    public float potWindupSeconds = 1.2f;
    public float potAoETickInterval = 0.5f;
    public int potAoETickDamage = 18;
    [Range(0f, 0.95f)] public float potAoESlowPercent = 0.45f;
    public string slowStatusTag = "Slow";
    public Sprite slowStatusIcon;
    [Tooltip("Minimum clear tiles between any two AoE field centers. "
           + "Keeps navigation gaps open for the player.")]
    public int aoEGapTiles = 1;
    [Tooltip("Max attempts to find a non-overlapping landing spot before giving up.")]
    public int aoEPlacementAttempts = 12;

    [Header("Pot Throw — AoE Visuals")]
    public GameObject tileMarkerPrefab;
    public float tileSize = 1f;
    public float markerYOffset = 0.02f;
    public LayerMask groundMask = ~0;
    public LayerMask victimLayer;

    [Header("Player Arena (for pot targeting)")]
    [Tooltip("Drag in the ArenaBounds GameObject that marks the floor of the fight area.")]
    public ArenaBounds arenaBounds;

    [Header("Minion Spawning")]
    public GameObject slimeMinionPrefab;
    [Tooltip("One per ventilation shaft exit — where each slime is spawned.")]
    public Transform[] spawnPoints;
    [Tooltip("Waypoints through the vent interior, shared by all spawned slimes. "
           + "Must be scene objects — the prefab cannot hold these references itself.")]
    public Transform[] ventWaypoints;
    [Tooltip("Floor target where the slime lands after the arc. "
           + "If null, SlimeVentEmerge raycasts downward automatically.")]
    public Transform ventLandingTarget;
    [Tooltip("Minions spawned each time she cooks (Phase 1). Phase 2 doubles this.")]
    public int minionsPerWave = 2;
    [Tooltip("Seconds between each slime spawn. They must never spawn at the same time.")]
    public float spawnStaggerDelay = 4f;

    [Header("Phase 2 (enrage at low HP)")]
    [Tooltip("Below this HP fraction she enters Phase 2 — moves faster, more minions.")]
    public float phase2HpFraction = 0.5f;
    public float phase2MoveSpeedBonus = 1f;       // added to moveSpeed
    public float phase2CookDurationMultiplier = 0.75f;

    [Header("Animation")]
    [Tooltip("Float parameter name for the blend tree (set to 1 while walking, 0 while idle).")]
    public string animSpeedParam = "Speed01";
    [Tooltip("Trigger-based animations — leave blank to skip.")]
    public string animCook     = "Cook";
    public string animAddToPot = "AddToPot";
    public string animThrow    = "Throw";
    public string animLaugh    = "Laugh";
    public string animEnrage   = "Enrage";
    [Tooltip("Seconds to play the laugh animation before throwing.")]
    public float laughDuration = 1.8f;

    [Header("Hand Props")]
    [Tooltip("Objects enabled while she is cooking (e.g. knife, spoon). All others are hidden.")]
    public GameObject[] cookingProps;
    [Tooltip("Objects enabled while she is adding to / throwing the pot (e.g. the pot mesh in her hand).")]
    public GameObject[] throwProps;
    [Tooltip("The pot that sits on the counter — hidden when she picks it up to throw.")]
    public GameObject counterPot;

    // ────────────────────────────────────────────────
    // Internal state
    float potCurrentScale;
    int   tripCount;
    bool  goLeft = true;
    bool  inPhase2;
    float effectiveMoveSpeed    => moveSpeed + (inPhase2 ? phase2MoveSpeedBonus : 0f);
    float effectiveCookDuration => cookDuration * (inPhase2 ? phase2CookDurationMultiplier : 1f);

    // Persistent AoE fields — cleared when the boss dies
    readonly List<GameObject> activeAoEFields = new();
    // Centres of placed fields for gap enforcement
    readonly List<Vector3> placedCenters = new();
    bool _throwPending;
    Vector3 _nextThrowTarget;
    Coroutine _spawnWaveCoroutine;
    // When non-null, LateUpdate smoothly rotates toward this every frame
    Transform _facingTarget;
    bool _lockFacing; // tracked so each cook can stop any still-running wave

    // ────────────────────────────────────────────────
    void Awake()
    {
        if (!health)   health   = GetComponent<NPCHealth>();
        if (!animator) animator = GetComponentInChildren<Animator>();

        ResetPot();
    }

    // ────────────────────────────────────────────────
    /// <summary>Called by BossTriggerZone when the player enters the kitchen.</summary>
    public void ActivateFight()
    {
        if (CurrentState != BossState.Inactive) return;
        if (health && health.IsDead) return;

        if (bossHealthBar) bossHealthBar.Show(health);

        StartCoroutine(BossLoop());
        // Slimes are now spawned during each cook cycle, not on a separate timer.
    }

    // ────────────────────────────────────────────────
    //   MAIN BOSS LOOP
    // ────────────────────────────────────────────────
    IEnumerator BossLoop()
    {
        while (true)
        {
            if (IsDead()) yield break;
            CheckPhaseTransition();

            // ── Move to a cooking side ──────────────────
            Transform side = goLeft ? leftCookPos : rightCookPos;
            goLeft = !goLeft;

            yield return StartCoroutine(MoveTo(side));
            if (IsDead()) yield break;

            // ── Cook + spawn slimes ──────────────────────
            CurrentState = BossState.Cooking;
            PlayAnim(animCook);
            // Stop any still-running wave from last cook before starting the new one
            if (_spawnWaveCoroutine != null) StopCoroutine(_spawnWaveCoroutine);
            _spawnWaveCoroutine = StartCoroutine(SpawnWave());
            yield return new WaitForSeconds(effectiveCookDuration);
            if (IsDead()) yield break;

            // ── Return to center ─────────────────────────
            yield return StartCoroutine(MoveTo(centerPos));
            if (IsDead()) yield break;

            // ── Add to pot ───────────────────────────────
            CurrentState = BossState.AddingToPot;

            // Lock rotation toward the pot for the entire animation via LateUpdate
            _facingTarget = potFaceTarget ? potFaceTarget : potTransform;

            PlayAnim(animAddToPot);

            if (ingredientDropPrefab && potTransform)
                StartCoroutine(DropIngredient());

            yield return new WaitForSeconds(addToPotAnimDuration);
            _facingTarget = null; // release facing lock
            if (IsDead()) yield break;

            FillPot();

            // ── Throw if full ─────────────────────────────
            if (IsPotFull())
            {
                CurrentState = BossState.ThrowingPot;

                // Pick up pot as she leaves the counter — hide the counter pot,
                // show the hand pot. She's now carrying it to the throw spot.
                if (counterPot) counterPot.SetActive(false);
                SetHandProps(throwProps);

                // Walk to throw position via intermediate waypoints (avoids clipping)
                Transform throwDest = throwPos ? throwPos : centerPos;
                yield return StartCoroutine(MoveAlongPath(throwPathWaypoints, throwDest));
                if (IsDead()) yield break;

                // Show telegraph and start laugh window
                _nextThrowTarget = FindClearLandingSpot();
                _throwPending = true;
                StartCoroutine(ShowThrowTelegraph(_nextThrowTarget));

                PlayAnim(animLaugh);
                yield return new WaitForSeconds(laughDuration);
                if (IsDead()) yield break;

                // Throw anim — AnimEvent_ReleasePot fires the projectile on the exact frame
                PlayAnim(animThrow);
                yield return new WaitUntil(() => !_throwPending || IsDead());
                if (IsDead()) yield break;

                // Walk back to center — use return path (or reverse of throw path)
                ResetPot();
                Transform[] returnPath = (returnPathWaypoints != null && returnPathWaypoints.Length > 0)
                    ? returnPathWaypoints
                    : ReverseWaypoints(throwPathWaypoints);
                yield return StartCoroutine(MoveAlongPath(returnPath, centerPos));

                // Put the counter pot back once she's returned
                SetHandProps(null);
                if (counterPot) counterPot.SetActive(true);
            }
        }
    }

    // ────────────────────────────────────────────────
    //   MOVEMENT
    // ────────────────────────────────────────────────
    IEnumerator MoveTo(Transform target)
    {
        if (!target) yield break;

        CurrentState = BossState.MovingToSide;
        _facingTarget = null; // stop any LateUpdate facing lock while moving
        SetSpeed(1f);

        Vector3 start = transform.position;
        Vector3 end   = target.position;
        end.y = start.y;

        float dist = Vector3.Distance(start, end);
        if (dist < 0.01f)
        {
            SetSpeed(0f);
            yield break;
        }

        float duration = dist / Mathf.Max(0.1f, effectiveMoveSpeed);
        float t = 0f;

        Vector3 dir = end - start; dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);

        while (t < 1f)
        {
            if (IsDead()) yield break;
            t += Time.deltaTime / duration;
            transform.position = Vector3.Lerp(start, end, Mathf.Clamp01(t));
            yield return null;
        }

        transform.position = end;
        SetSpeed(0f);
    }

    /// <summary>Moves through a series of waypoints in order, then to the final destination.</summary>
    IEnumerator MoveAlongPath(Transform[] waypoints, Transform destination)
    {
        if (waypoints != null)
        {
            foreach (var wp in waypoints)
            {
                if (!wp || IsDead()) yield break;
                yield return StartCoroutine(MoveTo(wp));
            }
        }
        if (destination) yield return StartCoroutine(MoveTo(destination));
    }

    Transform[] ReverseWaypoints(Transform[] waypoints)
    {
        if (waypoints == null || waypoints.Length == 0) return null;
        var rev = new Transform[waypoints.Length];
        for (int i = 0; i < waypoints.Length; i++)
            rev[i] = waypoints[waypoints.Length - 1 - i];
        return rev;
    }
    void FillPot()
    {
        tripCount++;
        potCurrentScale = Mathf.Clamp(
            potEmptyScale + tripCount * potFillPerTrip,
            potEmptyScale,
            potFullScale);

        ApplyPotScale(potCurrentScale);
    }

    bool IsPotFull() => potCurrentScale >= potFullScale - 0.001f;

    void ResetPot()
    {
        tripCount        = 0;
        potCurrentScale  = potEmptyScale;
        ApplyPotScale(potEmptyScale);
    }

    void ApplyPotScale(float yScale)
    {
        if (!potTransform) return;
        Vector3 s = potTransform.localScale;
        s.y = yScale;
        potTransform.localScale = s;
    }

    // ────────────────────────────────────────────────
    //   POT THROW
    // ────────────────────────────────────────────────

    /// <summary>Shows ground telegraph during the laugh phase so the player sees it coming.</summary>
    IEnumerator ShowThrowTelegraph(Vector3 target)
    {
        if (!tileMarkerPrefab) yield break;

        float groundY = SampleGroundY(target);
        var markers = new List<GameObject>();

        foreach (var c in GetDiamondTiles(target, potAoERadius))
        {
            Vector3 pos = c; pos.y = groundY + markerYOffset;
            var m = Instantiate(tileMarkerPrefab, pos, Quaternion.identity);
            TintMarker(m, new Color(1f, 0.3f, 0.05f, 0.9f));
            if (!m.TryGetComponent<TileMarker>(out var tm)) tm = m.AddComponent<TileMarker>();
            tm.Init(laughDuration + throwAnimDuration + 1f, tileSize);
            markers.Add(m);
        }

        // Stay alive until the throw is done, then clean up
        yield return new WaitUntil(() => !_throwPending || IsDead());
        yield return new WaitForSeconds(0.3f);
        foreach (var m in markers) if (m) Destroy(m);
    }

    /// <summary>
    /// Fires the projectile from the hand bone on the exact throw frame.
    /// No internal windup — telegraph was already shown during the laugh.
    /// </summary>
    IEnumerator ThrowPotRoutine()
    {
        if (!bombPrefab || !arenaBounds)
        {
            Debug.LogWarning("[CafeteriaLadyBoss] bombPrefab or arenaBounds not assigned — throw skipped.");
            yield break;
        }

        Vector3 target = _nextThrowTarget;

        Vector3 origin  = (throwOrigin ? throwOrigin.position
                          : potTransform ? potTransform.position
                          : transform.position) + Vector3.up * 0.1f;
        Vector3 dest    = target + Vector3.up * 0.5f;
        float distTiles = Mathf.Max(1f, Vector3.Distance(origin, dest) / Mathf.Max(0.01f, tileSize));
        float arcDur    = bombThrowTimePerTile * distTiles;

        var bomb = Instantiate(bombPrefab, origin, Quaternion.identity);
        var arc  = bomb.GetComponent<BombProjectileArc>() ?? bomb.AddComponent<BombProjectileArc>();

        Vector3 capturedTarget = target;
        arc.Init(
            start:           origin,
            end:             dest,
            duration:        arcDur,
            arcHeight:       bombArcHeight,
            groundMask:      groundMask,
            explosionPrefab: explosionVfxPrefab,
            onExplode: (_) =>
            {
                var go    = new GameObject("BossPotAoEField");
                var field = go.AddComponent<BombAoEFieldHostile>();
                field.Init(
                    center:        capturedTarget,
                    tileSize:      tileSize,
                    markerPrefab:  tileMarkerPrefab,
                    markerYOffset: markerYOffset,
                    groundMask:    groundMask,
                    victimsLayer:  victimLayer,
                    radiusTiles:   potAoERadius,
                    duration:      99999f,
                    tickInterval:  potAoETickInterval,
                    tickDamage:    potAoETickDamage,
                    slowPercent:   potAoESlowPercent,
                    slowTag:       slowStatusTag,
                    slowIcon:      slowStatusIcon
                );
                activeAoEFields.Add(go);
                placedCenters.Add(capturedTarget);
            }
        );

        // Wait for arc travel time before signalling done
        yield return new WaitForSeconds(arcDur + 0.1f);
    }

    /// <summary>
    /// Find an arena point that has at least aoEGapTiles clear distance from
    /// all existing AoE field edges. Falls back to a random point if no clear
    /// spot is found within aoEPlacementAttempts tries.
    /// </summary>
    Vector3 FindClearLandingSpot()
    {
        float minCenterDist = (potAoERadius * 2 + aoEGapTiles) * tileSize;

        for (int attempt = 0; attempt < aoEPlacementAttempts; attempt++)
        {
            Vector3 candidate = arenaBounds.GetRandomPoint();
            bool clear = true;

            foreach (var existing in placedCenters)
            {
                float d = Vector3.Distance(
                    new Vector3(candidate.x, 0, candidate.z),
                    new Vector3(existing.x,  0, existing.z));
                if (d < minCenterDist) { clear = false; break; }
            }

            if (clear) return candidate;
        }

        // No clear spot found — just use a random point
        return arenaBounds.GetRandomPoint();
    }

    void ClearAllAoEFields()
    {
        foreach (var go in activeAoEFields)
            if (go) Destroy(go);
        activeAoEFields.Clear();
        placedCenters.Clear();
    }

    // ────────────────────────────────────────────────
    //   HAND PROPS + ANIMATION EVENTS
    // ────────────────────────────────────────────────
    void SetHandProps(GameObject[] propsToEnable)
    {
        if (cookingProps != null) foreach (var p in cookingProps) if (p) p.SetActive(false);
        if (throwProps   != null) foreach (var p in throwProps)   if (p) p.SetActive(false);
        if (propsToEnable != null)
            foreach (var p in propsToEnable) if (p) p.SetActive(true);
    }

    /// <summary>
    /// Wire this to an Animation Event on the Throw clip at the exact frame she releases the pot.
    /// This fires the projectile. Prop visibility is handled by the coroutine, not this event.
    /// </summary>
    public void AnimEvent_ReleasePot()
    {
        // Hide hand pot at the moment of release — she's just thrown it
        SetHandProps(null);
        StartCoroutine(ThrowAndClearPending());
    }

    IEnumerator ThrowAndClearPending()
    {
        yield return StartCoroutine(ThrowPotRoutine());
        _throwPending = false;
    }

    /// <summary>
    /// Optional: wire to Cook clip to show cooking prop at the right frame.
    /// </summary>
    public void AnimEvent_PickUpCookingProp() => SetHandProps(cookingProps);

    /// <summary>
    /// Optional: wire to Cook clip to hide cooking prop at the right frame.
    /// </summary>
    public void AnimEvent_PutDownCookingProp() => SetHandProps(null);

    // ────────────────────────────────────────────────
    //   MINION SPAWNING (triggered each cook cycle)
    // ────────────────────────────────────────────────
    IEnumerator SpawnWave()
    {
        if (!slimeMinionPrefab) yield break;
        if (spawnPoints == null || spawnPoints.Length == 0) yield break;

        int count = inPhase2 ? minionsPerWave * 2 : minionsPerWave;

        for (int i = 0; i < count; i++)
        {
            if (IsDead()) yield break;

            var pt = spawnPoints[Random.Range(0, spawnPoints.Length)];
            if (!pt) continue;

            var go = Instantiate(slimeMinionPrefab, pt.position, pt.rotation);
            go.name = slimeMinionPrefab.name;

            // Always stagger between slimes — they must never spawn simultaneously
            if (i < count - 1)
                yield return new WaitForSeconds(spawnStaggerDelay);
        }
    }

    // ────────────────────────────────────────────────
    //   PHASE TRANSITION
    // ────────────────────────────────────────────────
    void CheckPhaseTransition()
    {
        if (inPhase2 || !health) return;
        float frac = (float)health.currentHP / Mathf.Max(1, health.maxHP);
        if (frac <= phase2HpFraction)
        {
            inPhase2 = true;
            PlayAnim(animEnrage);
            Debug.Log("[CafeteriaLadyBoss] Entering Phase 2 — enraged!");
        }
    }

    // ────────────────────────────────────────────────
    //   DEATH DETECTION
    // ────────────────────────────────────────────────
    bool IsDead() => health && health.IsDead;

    void Update()
    {
        if (CurrentState != BossState.Dead && IsDead())
        {
            CurrentState = BossState.Dead;
            StopAllCoroutines();
            if (bossHealthBar) bossHealthBar.Hide();
            SetHandProps(null);
            if (counterPot) counterPot.SetActive(true);
            ClearAllAoEFields();
            // Deactivate the hazard zone so the player can safely loot the corpse
            if (hazardZone) hazardZone.Deactivate();

            if (enableOnDeath  != null) foreach (var go in enableOnDeath)  if (go) go.SetActive(true);
            if (disableOnDeath != null) foreach (var go in disableOnDeath) if (go) go.SetActive(false);
        }
    }

    // ────────────────────────────────────────────────
    IEnumerator DropIngredient()
    {
        yield return new WaitForSeconds(ingredientDropDelay);
        if (!ingredientDropPrefab || !potTransform || IsDead()) yield break;

        Vector3 spawnPos = potTransform.position + ingredientSpawnOffset;
        var obj = Instantiate(ingredientDropPrefab, spawnPos, Random.rotation);

        // Ensure it has a Rigidbody so gravity pulls it into the pot
        if (!obj.GetComponent<Rigidbody>())
        {
            var rb = obj.AddComponent<Rigidbody>();
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        Destroy(obj, ingredientDropLifetime);
    }

    void PlayAnim(string trigger)
    {
        if (!animator || string.IsNullOrEmpty(trigger)) return;
        animator.SetTrigger(trigger);
    }

    void SetSpeed(float speed)
    {
        if (!animator || string.IsNullOrEmpty(animSpeedParam)) return;
        animator.SetFloat(animSpeedParam, speed);
    }

    void LateUpdate()
    {
        // Smoothly hold rotation toward _facingTarget every frame.
        // LateUpdate runs after the Animator, so root motion can't override it.
        if (!_facingTarget || IsDead()) return;
        Vector3 dir = _facingTarget.position - transform.position; dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return;
        Quaternion target = Quaternion.LookRotation(dir.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, 360f * Time.deltaTime);
    }

    // ────────────────────────────────────────────────
    //   HELPERS
    // ────────────────────────────────────────────────
    float SampleGroundY(Vector3 at)
    {
        Vector3 origin = at + Vector3.up * 10f;
        if (Physics.Raycast(origin, Vector3.down, out var hit, 40f, groundMask, QueryTriggerInteraction.Ignore))
            return hit.point.y;
        return at.y;
    }

    IEnumerable<Vector3> GetDiamondTiles(Vector3 center, int radius)
    {
        Vector3 snapped = new Vector3(
            Mathf.Round(center.x / tileSize) * tileSize,
            center.y,
            Mathf.Round(center.z / tileSize) * tileSize);

        for (int dx = -radius; dx <= radius; dx++)
        {
            int maxDz = radius - Mathf.Abs(dx);
            for (int dz = -maxDz; dz <= maxDz; dz++)
                yield return new Vector3(snapped.x + dx * tileSize, snapped.y, snapped.z + dz * tileSize);
        }
    }

    static void TintMarker(GameObject m, Color col)
    {
        if (!m) return;
        if (m.TryGetComponent<Renderer>(out var rend) && rend.material) { rend.material.color = col; return; }
        if (m.TryGetComponent<UnityEngine.UI.Image>(out var img)) { img.color = col; return; }
        var childR = m.GetComponentInChildren<Renderer>();
        if (childR && childR.material) childR.material.color = col;
    }

    // ────────────────────────────────────────────────
    void OnDrawGizmos()
    {
        // Arena gizmo is drawn by ArenaBounds itself.
        // Draw lines from boss to cooking stations for quick visual reference.
        Gizmos.color = new Color(1f, 0.8f, 0f, 0.7f);
        if (leftCookPos)  Gizmos.DrawLine(transform.position, leftCookPos.position);
        if (rightCookPos) Gizmos.DrawLine(transform.position, rightCookPos.position);
        if (centerPos)    Gizmos.DrawWireSphere(centerPos.position, 0.2f);
    }
}
