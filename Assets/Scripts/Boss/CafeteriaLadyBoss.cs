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

    [Header("Positions (empty Transforms in scene)")]
    [Tooltip("Where she stands when not cooking — behind/in front of the pot.")]
    public Transform centerPos;
    [Tooltip("Left cooking station.")]
    public Transform leftCookPos;
    [Tooltip("Right cooking station.")]
    public Transform rightCookPos;

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

    [Header("Pot Throw — Projectile")]
    [Tooltip("Bomb arc prefab (same as NPCBombAbility uses).")]
    public GameObject bombPrefab;
    public GameObject explosionVfxPrefab;
    [Tooltip("How fast the pot travels (seconds per tile).")]
    public float bombThrowTimePerTile = 0.08f;
    public float bombArcHeight = 2.5f;

    [Header("Pot Throw — Landing AoE")]
    public int potAoERadius = 2;
    public float potAoEDuration = 6f;
    public float potAoETickInterval = 0.5f;
    public int potAoETickDamage = 18;
    [Range(0f, 0.95f)] public float potAoESlowPercent = 0.45f;
    public string slowStatusTag = "Slow";
    public Sprite slowStatusIcon;

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
    [Tooltip("Seconds after fight starts before the first wave.")]
    public float firstWaveDelay = 4f;
    [Tooltip("Seconds between subsequent waves.")]
    public float waveCooldown = 12f;
    [Tooltip("Minions spawned per wave (Phase 1). Phase 2 doubles this.")]
    public int minionsPerWave = 2;
    [Tooltip("Seconds between each individual slime spawn within a wave. "
           + "Give them time to clear the vent before the next one enters.")]
    public float spawnStaggerDelay = 4f;

    [Header("Phase 2 (enrage at low HP)")]
    [Tooltip("Below this HP fraction she enters Phase 2 — moves faster, more minions.")]
    public float phase2HpFraction = 0.5f;
    public float phase2MoveSpeedBonus = 1f;       // added to moveSpeed
    public float phase2CookDurationMultiplier = 0.75f;

    [Header("Animation Triggers (leave blank to skip)")]
    public string animWalk     = "Walk";
    public string animIdle     = "Idle";
    public string animCook     = "Cook";
    public string animAddToPot = "AddToPot";
    public string animThrow    = "Throw";
    public string animEnrage   = "Enrage";

    // ────────────────────────────────────────────────
    // Internal state
    float potCurrentScale;
    int   tripCount;
    bool  goLeft = true;
    bool  inPhase2;
    float effectiveMoveSpeed  => moveSpeed + (inPhase2 ? phase2MoveSpeedBonus : 0f);
    float effectiveCookDuration => cookDuration * (inPhase2 ? phase2CookDurationMultiplier : 1f);

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
        StartCoroutine(MinionWaveLoop());
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

            // ── Cook ────────────────────────────────────
            CurrentState = BossState.Cooking;
            PlayAnim(animCook);
            yield return new WaitForSeconds(effectiveCookDuration);
            if (IsDead()) yield break;

            // ── Return to center ─────────────────────────
            yield return StartCoroutine(MoveTo(centerPos));
            if (IsDead()) yield break;

            // ── Add to pot ───────────────────────────────
            CurrentState = BossState.AddingToPot;
            PlayAnim(animAddToPot);
            yield return new WaitForSeconds(addToPotAnimDuration);
            if (IsDead()) yield break;

            FillPot();

            // ── Throw if full ─────────────────────────────
            if (IsPotFull())
            {
                CurrentState = BossState.ThrowingPot;
                PlayAnim(animThrow);
                yield return new WaitForSeconds(throwAnimDuration * 0.4f); // brief wind-up
                ThrowPot();
                yield return new WaitForSeconds(throwAnimDuration * 0.6f);
                ResetPot();
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
        PlayAnim(animWalk);

        Vector3 start = transform.position;
        Vector3 end   = target.position;
        end.y = start.y; // keep vertical position

        float dist = Vector3.Distance(start, end);
        if (dist < 0.01f)
        {
            PlayAnim(animIdle);
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
        PlayAnim(animIdle);
    }

    // ────────────────────────────────────────────────
    //   POT MANAGEMENT
    // ────────────────────────────────────────────────
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
    void ThrowPot()
    {
        if (!bombPrefab) return;

        // Random target inside the player arena
        if (!arenaBounds)
        {
            Debug.LogWarning("[CafeteriaLadyBoss] arenaBounds is not assigned — pot throw skipped.");
            return;
        }
        Vector3 target = arenaBounds.GetRandomPoint();

        Vector3 origin = (potTransform ? potTransform.position : transform.position)
                         + Vector3.up * 0.5f;
        Vector3 dest   = target + Vector3.up * 0.5f;

        float distTiles = Mathf.Max(1f, Vector3.Distance(origin, dest) / Mathf.Max(0.01f, tileSize));
        float arcDur    = bombThrowTimePerTile * distTiles;

        var bomb = Instantiate(bombPrefab, origin, Quaternion.identity);
        var arc  = bomb.GetComponent<BombProjectileArc>() ?? bomb.AddComponent<BombProjectileArc>();

        // Capture for lambda
        Vector3 capturedTarget = target;

        arc.Init(
            start:           origin,
            end:             dest,
            duration:        arcDur,
            arcHeight:       bombArcHeight,
            groundMask:      groundMask,
            explosionPrefab: explosionVfxPrefab,
            onExplode: (_landPos) =>
            {
                var go    = new GameObject("BossPotAoEField");
                var field = go.AddComponent<BombAoEFieldHostile>();
                field.Init(
                    center:       capturedTarget,
                    tileSize:     tileSize,
                    markerPrefab: tileMarkerPrefab,
                    markerYOffset: markerYOffset,
                    groundMask:   groundMask,
                    victimsLayer: victimLayer,
                    radiusTiles:  potAoERadius,
                    duration:     potAoEDuration,
                    tickInterval: potAoETickInterval,
                    tickDamage:   potAoETickDamage,
                    slowPercent:  potAoESlowPercent,
                    slowTag:      slowStatusTag,
                    slowIcon:     slowStatusIcon
                );
            }
        );
    }

    // ────────────────────────────────────────────────
    //   MINION WAVES
    // ────────────────────────────────────────────────
    IEnumerator MinionWaveLoop()
    {
        yield return new WaitForSeconds(firstWaveDelay);

        while (true)
        {
            if (IsDead()) yield break;
            yield return StartCoroutine(SpawnWave());
            yield return new WaitForSeconds(waveCooldown);
        }
    }

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

            // Strip "(Clone)" from the nameplate.
            go.name = slimeMinionPrefab.name;

            // No waypoint injection needed here.
            // SlimeVentEmerge.Start() calls FindAnyObjectByType<CafeteriaLadyBoss>()
            // and pulls ventWaypoints + ventLandingTarget directly from this component.

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
            // NPCHealth handles the rest: death anim, CorpseLoot, XP.
        }
    }

    // ────────────────────────────────────────────────
    void PlayAnim(string trigger)
    {
        if (!animator || string.IsNullOrEmpty(trigger)) return;
        animator.SetTrigger(trigger);
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
