using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Attached to the turret prefab at runtime by TurretAbility.
/// The prefab should be structured as:
///   TurretRoot
///     ├─ Base          (static, never moves)
///     ├─ Swivel        (rotates on Y-axis toward target)
///     │    └─ Head     (rotates on X-axis toward target; starts tilted up, eases to level on boot)
///
/// Assign the three transforms via TurretAbility → Init(), or wire them in the prefab Inspector.
/// </summary>
public class TurretController : MonoBehaviour
{
    // ─── Turret Sub-Parts ──────────────────────────────────────────────────────
    [Header("Turret Parts (assign in prefab or set via Init)")]
    [Tooltip("Rotates on the Y-axis to face the target.")]
    public Transform swivel;
    [Tooltip("Child of Swivel. Rotates on X-axis to tilt up/down. Starts at bootHeadAngle.")]
    public Transform head;

    // ─── Boot Sequence ─────────────────────────────────────────────────────────
    [Header("Boot-Up Sequence")]
    [Tooltip("Head X-angle at spawn (tilted up). Eases to 0 over bootDuration.")]
    public float bootHeadAngle    = 40f;
    [Tooltip("Seconds to ease the head from bootHeadAngle → 0.")]
    public float bootDuration     = 1.2f;

    // ─── Targeting ─────────────────────────────────────────────────────────────
    [Header("Targeting")]
    [Tooltip("How far the turret scans for hostile NPCs (Unity units).")]
    public float scanRadius       = 12f;
    [Tooltip("How often (seconds) to re-evaluate targets.")]
    public float scanInterval     = 0.35f;
    [Tooltip("Layer(s) that contain NPC colliders.")]
    public LayerMask npcLayer;
    [Tooltip("Layer(s) considered walls for LOS checks.")]
    public LayerMask wallLayer;
    [Tooltip("Height offset above pivot for LOS raycasts.")]
    public float losHeight        = 0.8f;

    // ─── Rotation ──────────────────────────────────────────────────────────────
    [Header("Rotation")]
    [Tooltip("Degrees per second the swivel rotates on Y.")]
    public float yawSpeed         = 180f;
    [Tooltip("Degrees per second the head tilts on X.")]
    public float pitchSpeed       = 90f;
    [Tooltip("Maximum downward pitch (degrees). Prevents the head clipping into the base.")]
    public float maxPitchDown     = 15f;

    // ─── Idle Scan ─────────────────────────────────────────────────────────────
    [Header("Idle Scan")]
    [Tooltip("Half-arc (degrees) the swivel sweeps left/right from its home direction.")]
    public float idleSweepArc      = 70f;
    [Tooltip("Degrees per second the swivel moves while scanning (usually slower than yawSpeed).")]
    public float idleSweepSpeed    = 40f;
    [Tooltip("Min seconds the turret pauses at each scan waypoint before choosing a new one.")]
    public float idlePauseMin      = 0.6f;
    [Tooltip("Max seconds the turret pauses at each scan waypoint before choosing a new one.")]
    public float idlePauseMax      = 1.6f;
    [Tooltip("Max degrees the head tilts up and down during the idle bob.")]
    public float idleHeadBobAngle  = 6f;
    [Tooltip("Full up-down cycles per second for the idle head bob.")]
    public float idleHeadBobSpeed  = 0.4f;

    // ─── Firing ────────────────────────────────────────────────────────────────
    [Header("Firing")]
    [Tooltip("Projectile prefab to fire. If left empty, falls back to the Nerd's projectile " +
             "assigned on PlayerAbilities (ctx.projectilePrefab).")]
    [SerializeField] private GameObject projectilePrefabOverride;
    [Tooltip("Seconds between shots.")]
    public float fireInterval     = 0.8f;
    [Tooltip("Base damage per projectile (scaled by PlayerStats if available).")]
    public int   baseDamage       = 8;
    [Tooltip("Degrees of tolerance between the swivel and the locked 8-direction before the " +
             "turret will fire. Keep this small (≤10°) so shots feel grid-aligned.")]
    public float aimTolerance     = 8f;
    [Tooltip("Transform from which projectiles are spawned (tip of barrel). " +
             "If null, falls back to the head transform.")]
    public Transform muzzle;

    // ─── Lifetime & Explosion ──────────────────────────────────────────────────
    [Header("Lifetime")]
    [Tooltip("Seconds until the turret self-destructs.")]
    public float lifetime         = 15f;
    public GameObject explosionVfxPrefab;
    public AudioClip  explosionSfx;
    [Range(0f, 2f)]    public float explosionSfxVolume = 1f;
    [Range(0.25f, 2f)] public float explosionSfxPitch  = 1f;
    [Tooltip("Radius of the farewell explosion damage (0 = no damage on death).")]
    public float deathBlastRadius = 0f;
    public int   deathBlastDamage = 0;

    // ─── Audio ─────────────────────────────────────────────────────────────────
    [Header("Audio")]
    public AudioClip shootSfx;
    [Range(0f, 2f)] public float shootVolume = 1f;
    [Range(0.25f, 2f)] public float shootPitch = 1f;

    // ─── Impact VFX (forwarded to ProjectileImpactVFX) ─────────────────────────
    [Header("Projectile Impact VFX")]
    public GameObject impactVfxPrefab;
    public AudioClip  impactSfx;
    [Range(0f, 2f)] public float impactVfxScale = 1f;
    public float impactVfxLife = 1.5f;

    // ─── Runtime state (set by TurretAbility.Init) ─────────────────────────────
    [HideInInspector] public PlayerAbilities ctx;   // gives us projectile prefab, layers, stats …

    // ─── Private ───────────────────────────────────────────────────────────────
    private bool      isBooting   = true;
    private bool      isReady     = false;   // false during boot; no shooting until ready
    private Transform currentTarget;
    private float     nextScanTime;
    private float     nextFireTime;
    private float     deathTime;

    // ─── Idle scan state ───────────────────────────────────────────────────────
    // World-space Y angle the turret spawned facing — sweep waypoints are relative to this.
    private float idleHomeYaw;
    // The world-space Y angle we're currently rotating toward.
    private float idleYawTarget;
    // Time.time after which we pick a new waypoint (0 = pick immediately).
    private float idlePauseUntil;
    // Whether we have arrived at the current waypoint and are in the pause phase.
    private bool  idleWaiting;

    // ══════════════════════════════════════════════════════════════════════════
    //  Init (called by TurretAbility right after Instantiate)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Wire up the PlayerAbilities context and override any serialised fields that
    /// TurretAbility wants to control centrally.
    /// </summary>
    public void Init(
        PlayerAbilities playerCtx,
        float overrideLifetime    = -1f,
        int   overrideBaseDamage  = -1,
        float overrideScanRadius  = -1f)
    {
        ctx = playerCtx;

        if (overrideLifetime   > 0f) lifetime   = overrideLifetime;
        if (overrideBaseDamage > 0)  baseDamage  = overrideBaseDamage;
        if (overrideScanRadius > 0f) scanRadius  = overrideScanRadius;

        // If TurretAbility didn't assign wall/npc layers, borrow from ctx
        if (wallLayer.value == 0 && ctx != null) wallLayer = ctx.wallLayer;
        if (npcLayer.value  == 0 && ctx != null) npcLayer  = ctx.targetLayer;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Unity lifecycle
    // ══════════════════════════════════════════════════════════════════════════

    void Awake()
    {
        // Auto-find sub-parts if not wired in the prefab
        if (!swivel) swivel = transform.Find("Swivel");
        if (swivel && !head) head = swivel.Find("Head");
        if (!muzzle && head) muzzle = head.Find("Muzzle");
    }

    void Start()
    {
        deathTime    = Time.time + Mathf.Max(0.1f, lifetime);
        nextScanTime = Time.time + scanInterval;
        nextFireTime = Time.time + bootDuration + fireInterval;   // first shot only after boot

        // Record the direction the turret is facing at spawn as the "home" for idle sweeping.
        idleHomeYaw    = swivel ? swivel.eulerAngles.y : transform.eulerAngles.y;
        idleYawTarget  = idleHomeYaw;
        idlePauseUntil = 0f;
        idleWaiting    = false;

        // Kick off boot animation
        if (head) StartCoroutine(BootHeadSequence());
        else      isReady = true;  // no head — skip boot
    }

    void Update()
    {
        // ── Lifetime check ────────────────────────────────────────────────────
        if (Time.time >= deathTime)
        {
            Explode();
            return;
        }

        if (!isReady) return;   // still booting

        // ── Target scan ───────────────────────────────────────────────────────
        if (Time.time >= nextScanTime)
        {
            nextScanTime = Time.time + scanInterval;
            currentTarget = FindBestHostileTarget();
        }

        // ── Track & fire ──────────────────────────────────────────────────────
        if (currentTarget)
        {
            // Swivel rotates smoothly toward the real target position (visual)
            TrackTarget(currentTarget.position);

            // But the projectile always travels in one of the 8 grid-aligned directions.
            // We fire only once the swivel is close enough to that locked direction.
            Vector3 dir8 = SnapDirTo8(currentTarget.position - transform.position);
            if (dir8 != Vector3.zero && Time.time >= nextFireTime && IsAimedAtDir(dir8))
            {
                nextFireTime = Time.time + Mathf.Max(0.05f, fireInterval);
                FireProjectile(dir8);
            }
        }
        else
        {
            DoIdleScan();
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Boot sequence
    // ══════════════════════════════════════════════════════════════════════════

    IEnumerator BootHeadSequence()
    {
        isReady  = false;
        isBooting = true;

        // Force starting angle
        if (head)
            head.localEulerAngles = new Vector3(bootHeadAngle, 0f, 0f);

        float elapsed = 0f;
        float dur = Mathf.Max(0.01f, bootDuration);

        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t  = Mathf.Clamp01(elapsed / dur);
            float angle = Mathf.Lerp(bootHeadAngle, 0f, EaseOutCubic(t));

            if (head) head.localEulerAngles = new Vector3(angle, 0f, 0f);
            yield return null;
        }

        if (head) head.localEulerAngles = Vector3.zero;
        isBooting = false;
        isReady   = true;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Target scanning
    // ══════════════════════════════════════════════════════════════════════════

    Transform FindBestHostileTarget()
    {
        // We scan using a generous mask — no NPC layer set? use everything.
        int mask = (npcLayer.value != 0) ? npcLayer.value : ~0;

        Collider[] cols = Physics.OverlapSphere(
            transform.position + Vector3.up * losHeight,
            scanRadius, mask, QueryTriggerInteraction.Ignore);

        Transform best    = null;
        float     bestDst = float.MaxValue;

        foreach (var col in cols)
        {
            // Must have NPCAI and be currently hostile
            if (!col.TryGetComponent<NPCAI>(out var npc)) continue;

            // NOTE: NPCAI exposes CurrentHostility as a public property.
            // If your version uses a different name, update this check.
            if (npc.CurrentHostility != NPCAI.Hostility.Hostile) continue;

            // Line-of-sight check
            if (!HasLineOfSight(col.bounds.center)) continue;

            float dst = Vector3.Distance(transform.position, col.transform.position);
            if (dst < bestDst) { bestDst = dst; best = col.transform; }
        }

        return best;
    }

    bool HasLineOfSight(Vector3 targetCenter)
    {
        Vector3 origin = (muzzle ? muzzle.position : transform.position + Vector3.up * losHeight);
        Vector3 dir    = (targetCenter - origin);
        float   dist   = dir.magnitude;
        if (dist < 0.01f) return true;

        int mask = (wallLayer.value != 0) ? wallLayer.value : LayerMask.GetMask("Wall");
        return !Physics.Raycast(origin, dir / dist, dist, mask, QueryTriggerInteraction.Ignore);
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Idle Scan
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Called every Update when there is no live target.
    /// The swivel picks random waypoints within ±idleSweepArc of the home direction,
    /// rotates to each one slowly, pauses briefly, then picks the next.
    /// The head bobs up and down on a sine wave throughout.
    /// </summary>
    void DoIdleScan()
    {
        // ── Swivel (Y) — waypoint sweep ──────────────────────────────────────
        if (swivel)
        {
            float currentYaw = NormalizeAngle(swivel.eulerAngles.y);
            float delta      = Mathf.Abs(Mathf.DeltaAngle(currentYaw, idleYawTarget));

            if (!idleWaiting)
            {
                // Rotate toward the current waypoint
                if (delta > 0.5f)
                {
                    float step     = idleSweepSpeed * Time.deltaTime;
                    float newYaw   = Mathf.MoveTowardsAngle(currentYaw, idleYawTarget, step);
                    swivel.rotation = Quaternion.Euler(0f, newYaw, 0f);
                }
                else
                {
                    // Arrived — snap exactly and begin the pause
                    swivel.rotation = Quaternion.Euler(0f, idleYawTarget, 0f);
                    idleWaiting     = true;
                    idlePauseUntil  = Time.time + Random.Range(
                        Mathf.Max(0f, idlePauseMin),
                        Mathf.Max(idlePauseMin, idlePauseMax));
                }
            }
            else if (Time.time >= idlePauseUntil)
            {
                // Pause over — pick a new random waypoint within the sweep arc.
                // Bias slightly: avoid choosing the same side twice in a row by
                // weighting toward the opposite half of the arc.
                float arc       = Mathf.Max(1f, idleSweepArc);
                float offsetPrev = NormalizeAngle(idleYawTarget - idleHomeYaw);
                // Random offset, but push it toward the opposite side
                float sign      = (offsetPrev >= 0f) ? -1f : 1f;
                float magnitude = Random.Range(arc * 0.25f, arc);
                idleYawTarget   = idleHomeYaw + sign * magnitude;
                idleWaiting     = false;
            }
        }

        // ── Head (X) — continuous sine bob ───────────────────────────────────
        if (head)
        {
            // Sine wave: oscillates between 0 and +idleHeadBobAngle (looking slightly down)
            float bob = idleHeadBobAngle * 0.5f *
                        (1f - Mathf.Cos(Time.time * idleHeadBobSpeed * Mathf.PI * 2f));

            Vector3 e = head.localEulerAngles;
            // Bob on X; normalise so MoveTowards doesn't spin the long way round
            float cur  = NormalizeAngle(e.x);
            float next = Mathf.MoveTowards(cur, bob, pitchSpeed * Time.deltaTime);
            head.localEulerAngles = new Vector3(next, e.y, e.z);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Rotation (swivel = Y, head = X)
    // ══════════════════════════════════════════════════════════════════════════

    void TrackTarget(Vector3 targetPos)
    {
        // ── Swivel (Y) ───────────────────────────────────────────────────────
        if (swivel)
        {
            Vector3 flatDir = targetPos - swivel.position;
            flatDir.y = 0f;
            if (flatDir.sqrMagnitude > 0.0001f)
            {
                Quaternion desired = Quaternion.LookRotation(flatDir, Vector3.up);
                swivel.rotation    = Quaternion.RotateTowards(
                    swivel.rotation, desired, yawSpeed * Time.deltaTime);
            }
        }

        // ── Head (X) ─────────────────────────────────────────────────────────
        if (head)
        {
            Transform pivot  = muzzle ? muzzle : head;
            Vector3   toTgt  = targetPos - pivot.position;
            float     hDist  = new Vector3(toTgt.x, 0f, toTgt.z).magnitude;
            float     desiredPitch = -Mathf.Atan2(toTgt.y, hDist) * Mathf.Rad2Deg;
            desiredPitch           = Mathf.Clamp(desiredPitch, -maxPitchDown, 0f);

            Vector3 localEuler = head.localEulerAngles;
            float   cur        = NormalizeAngle(localEuler.x);
            float   next       = Mathf.MoveTowards(cur, desiredPitch, pitchSpeed * Time.deltaTime);
            head.localEulerAngles = new Vector3(next, localEuler.y, localEuler.z);
        }
    }

    /// Returns true when the swivel's forward is within aimTolerance degrees of the
    /// supplied pre-snapped 8-direction, meaning we're aligned enough to fire.
    bool IsAimedAtDir(Vector3 dir8)
    {
        if (!swivel) return true;
        if (dir8.sqrMagnitude < 0.0001f) return false;
        float angle = Vector3.Angle(new Vector3(swivel.forward.x, 0f, swivel.forward.z), dir8);
        return angle <= aimTolerance;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Firing
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Spawns a projectile in <paramref name="dir8"/>, which must already be one of the
    /// 8 grid-aligned unit directions produced by SnapDirTo8.
    /// </summary>
    void FireProjectile(Vector3 dir8)
    {
        // Resolve which prefab to use: inspector override → ctx fallback
        GameObject prefab = projectilePrefabOverride
                            ? projectilePrefabOverride
                            : (ctx != null ? ctx.projectilePrefab : null);
        if (!prefab) return;

        Transform spawnTf = muzzle ? muzzle : (head ? head : transform);
        Vector3   origin  = spawnTf.position;

        // ── Damage ────────────────────────────────────────────────────────────
        bool didCrit = false;
        int  final   = (ctx != null && ctx.stats != null)
            ? ctx.stats.ComputeDamage(baseDamage, PlayerStats.AbilitySchool.Nerd, true, out didCrit)
            : baseDamage;

        // ── Projectile ────────────────────────────────────────────────────────
        var go = Object.Instantiate(prefab, origin,
                                    Quaternion.LookRotation(dir8, Vector3.up));

        var rb = go.GetComponent<Rigidbody>() ?? go.AddComponent<Rigidbody>();
        rb.isKinematic            = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.interpolation          = RigidbodyInterpolation.Interpolate;
        rb.detectCollisions       = true;

        var sph = go.GetComponent<SphereCollider>() ?? go.AddComponent<SphereCollider>();
        sph.isTrigger = false;
        if (sph.radius < 0.08f) sph.radius = 0.08f;

        float range = scanRadius + 2f;
        LayerMask impactLayers = ctx != null
            ? (ctx.targetLayer | ctx.wallLayer | ctx.groundLayer)
            : ~0;

        var proj = go.GetComponent<StraightProjectile>() ?? go.AddComponent<StraightProjectile>();
        proj.Init(
            direction:    dir8,
            speed:        ctx != null ? ctx.projectileSpeed : 12f,
            maxDistance:  range,
            hitLayers:    impactLayers,
            damageLayers: ctx != null ? ctx.targetLayer : impactLayers,
            damage:       final,
            tileSize:     ctx != null ? ctx.tileSize : 1f,
            wasCrit:      didCrit
        );

        var vfx = go.GetComponent<ProjectileImpactVFX>() ?? go.AddComponent<ProjectileImpactVFX>();
        vfx.Configure(impactVfxPrefab, impactSfx, impactVfxScale, impactVfxLife, false, impactLayers);

        // ── Muzzle audio ──────────────────────────────────────────────────────
        if (shootSfx) PlayOneShotAt(origin, shootSfx, shootVolume, shootPitch);
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Death / Explosion
    // ══════════════════════════════════════════════════════════════════════════

    void Explode()
    {
        Vector3 pos = transform.position;

        if (explosionVfxPrefab)
            Instantiate(explosionVfxPrefab, pos + Vector3.up * 0.1f, Quaternion.identity);

        // Explosion SFX — must use PlayOneShotAt (not AudioSource.PlayClipAtPoint shorthand)
        // so we get the same pitch/volume control as every other sound in this script.
        PlayOneShotAt(pos, explosionSfx, explosionSfxVolume, explosionSfxPitch);

        // Optional death-blast damage
        if (ctx != null && deathBlastRadius > 0f && deathBlastDamage > 0)
            ctx.DamageTileScaled(pos, deathBlastRadius, deathBlastDamage,
                                 PlayerStats.AbilitySchool.Nerd, false);

        Destroy(gameObject);
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Utilities
    // ══════════════════════════════════════════════════════════════════════════

    /// Remaps an angle from 0..360 into -180..180 so Mathf.MoveTowards works correctly.
    static float NormalizeAngle(float a)
    {
        while (a >  180f) a -= 360f;
        while (a < -180f) a += 360f;
        return a;
    }

    /// Mirrors PlayerAbilities.SnapDirTo8 — returns the nearest of the 8 cardinal/diagonal
    /// unit directions on the XZ plane. Returns Vector3.zero if the input is too small.
    static Vector3 SnapDirTo8(Vector3 v)
    {
        v.y = 0f;
        if (v.sqrMagnitude < 0.0001f) return Vector3.zero;
        float ang  = Mathf.Atan2(v.z, v.x) * Mathf.Rad2Deg;
        if (ang < 0f) ang += 360f;
        int step = Mathf.RoundToInt(ang / 45f) % 8;
        return step switch
        {
            0 => new Vector3( 1, 0,  0),
            1 => new Vector3( 1, 0,  1).normalized,
            2 => new Vector3( 0, 0,  1),
            3 => new Vector3(-1, 0,  1).normalized,
            4 => new Vector3(-1, 0,  0),
            5 => new Vector3(-1, 0, -1).normalized,
            6 => new Vector3( 0, 0, -1),
            _ => new Vector3( 1, 0, -1).normalized,
        };
    }

    static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);

    void PlayOneShotAt(Vector3 pos, AudioClip clip, float volume, float pitch)
    {
        if (!clip) return;
        var go = new GameObject("OneShotAudio_Turret");
        go.transform.position = pos;
        var a = go.AddComponent<AudioSource>();
        a.clip         = clip;
        a.volume       = Mathf.Clamp01(volume);
        a.pitch        = Mathf.Clamp(pitch, 0.25f, 2f);
        a.spatialBlend = 1f;
        a.rolloffMode  = AudioRolloffMode.Linear;
        a.maxDistance  = 30f;
        a.Play();
        Destroy(go, clip.length / Mathf.Max(0.01f, a.pitch) + 0.1f);
    }
}