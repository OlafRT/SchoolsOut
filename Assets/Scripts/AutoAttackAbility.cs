using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class AutoAttackAbility : MonoBehaviour
{
    [Header("Input")]
    public KeyCode toggleAutoAttackKey = KeyCode.R;

    [Header("Weapon")]
    public float weaponAttackInterval = 2.5f;
    public int weaponDamage = 10;
    public int weaponRangeTiles = 6;          // used by Nerd (projectile)

    [Header("Requirements")]
    [Tooltip("We check this to see if the player actually has a weapon equipped.")]
    public EquipmentState equipment;
    [Tooltip("Toast shown if player tries to attack with no weapon equipped.")]
    public ScreenToast toastIfNoWeapon;
    [Tooltip("Message to show if they try R without a weapon.")]
    public string needWeaponMessage = "I must have a weapon equipped to do that.";

    #region Animation
    [Header("Animation (set both explicitly)")]
    [SerializeField] private Animator nerdAnimator;
    [SerializeField] private Animator jockAnimator;

    [SerializeField] private string nerdThrowTrigger = "Throw";       // Nerd ranged
    [SerializeField] private string jockAutoTrigger  = "AutoAttack";  // Jock melee
    [SerializeField] private float eventFailSafeSeconds = 1.0f;       // both paths
    #endregion

    [Header("Impact VFX (Nerd projectile)")]
    // Public so WeaponEquipBridge can hot-swap the VFX/SFX per weapon at runtime.
    public GameObject nerdImpactVfx;
    public AudioClip  nerdImpactSfx;
    [SerializeField] private float vfxScale = 1f;
    [SerializeField] private float vfxLife = 2f;
    [SerializeField] private bool parentVfxToHit = false;
    [SerializeField] private bool vfxUseSweepRaycast = true;
    [SerializeField] private float vfxSweepRadius = 0.08f;

    [Header("Telegraph (Nerd)")]
    [SerializeField] private bool telegraphNerdPreImpactPreview = true;

    [Header("Telegraph (Jock)")]
    [SerializeField] private bool telegraphJockPreImpactPreview = true;
    [SerializeField] private float telegraphRefreshInterval = 0.05f;

    [Header("Jock Melee Audio/VFX")]
    [SerializeField] private AudioClip jockSwingWhooshSfx;
    [Range(0f, 1f)] [SerializeField] private float jockWhooshVolume = 1f;

    [SerializeField] private AudioClip jockImpactSfx;
    [Range(0f, 1f)] [SerializeField] private float jockImpactVolume = 1f;

    [SerializeField] private GameObject jockImpactVfx;
    [SerializeField] private float jockImpactVfxLife = 1.5f;
    [SerializeField] private float jockImpactVfxScale = 1f;

    // -------------------------------------------------------------------------
    // Active weapon profile — set at runtime by WeaponEquipBridge.
    // HideInInspector keeps the Inspector clean; the bridge owns these values.
    // -------------------------------------------------------------------------
    [HideInInspector] public WeaponShotPattern activePattern         = WeaponShotPattern.Single;
    [HideInInspector] public string            activeAnimTrigger     = ""; // empty = use nerdThrowTrigger
    [HideInInspector] public int               spreadShots           = 3;
    [HideInInspector] public float             spreadAngleDegrees    = 45f;
    [HideInInspector] public float             spreadRangeMultiplier   = 0.6f;
    [HideInInspector] public float             spreadDamageMultiplier  = 1f;
    [HideInInspector] public int               burstCount            = 3;
    [HideInInspector] public float             burstDelay            = 0.1f;

    [System.Serializable]
    public class EmissiveSlot
    {
        public Renderer renderer;
        public int materialIndex = 0;

        // runtime
        [System.NonSerialized] private MaterialPropertyBlock _block;

        public void EnsureBlock()
        {
            if (_block == null) _block = new MaterialPropertyBlock();
        }

        public void ApplyEmission(Color emissionColor)
        {
            if (!renderer) return;
            EnsureBlock();
            _block.SetColor("_EmissionColor", emissionColor);
            renderer.SetPropertyBlock(_block, materialIndex);
        }

        public void DisableEmission()
        {
            ApplyEmission(Color.black);
        }
    }

    [Header("Nerd Auto-Attack Emissive Pulse")]
    [SerializeField] private bool enableNerdEmissivePulse = true;
    [SerializeField] private EmissiveSlot[] nerdEmissiveSlots;
    [SerializeField] private Color nerdEmissionColor = Color.cyan;
    [SerializeField] [Range(0f, 8f)] private float nerdEmissionMax = 2.0f;
    [SerializeField] [Range(0.1f, 5f)] private float nerdEmissionPeriod = 1.0f;
    [SerializeField] [Range(0f, 1f)] private float nerdEmissionPhaseOffset = 0f;

    private PlayerAbilities ctx;
    private float attackTimer;

    public bool AutoAttackEnabled { get; private set; } = false;
    public bool IsSuppressedByOtherAbilities { get; set; }

    // ---------- pending (Nerd ranged) ----------
    private struct PendingRanged
    {
        public bool has;
        public Vector3 dir8;
        public int stepX, stepZ;
        public int finalDamage;
        public bool wasCrit;
        public float timeoutAt;
        public float nextTelegraphAt;
    }
    private PendingRanged pendingRanged;

    // ---------- pending (Jock melee) ----------
    private struct PendingMelee
    {
        public bool has;
        public int finalDamage;
        public float timeoutAt;
        public float nextTelegraphAt;
    }
    private PendingMelee pendingMelee;

    void Awake()
    {
        ctx = GetComponent<PlayerAbilities>();

        // Shut down immediately when the player dies — stops the attack timer,
        // kills any in-flight burst coroutine, and clears pending state so no
        // queued shot can fire during the death animation.
        var health = GetComponent<PlayerHealth>();
        if (health) health.OnDied += HandlePlayerDied;

        // Try to auto-find animators if not wired
        if (!nerdAnimator || !jockAnimator)
        {
            var anims = GetComponentsInChildren<Animator>(true);
            foreach (var a in anims)
            {
                var n = a.gameObject.name.ToLowerInvariant();
                if (!nerdAnimator && (n.Contains("nerd") || n.Contains("mage") || n.Contains("ranged")))
                    nerdAnimator = a;
                else if (!jockAnimator && (n.Contains("jock") || n.Contains("melee") || n.Contains("warrior")))
                    jockAnimator = a;
            }
        }
    }

    void OnDestroy()
    {
        var health = GetComponent<PlayerHealth>();
        if (health) health.OnDied -= HandlePlayerDied;
    }

    void HandlePlayerDied()
    {
        // Turn off auto-attack mode and clear the emissive pulse.
        AutoAttackEnabled = false;
        NerdEmission_ForceOffImmediate();

        // Kill any burst coroutine that is mid-sequence — without this a
        // burst that started on the last attack cycle would keep firing
        // projectiles through the death animation.
        StopAllCoroutines();

        // Clear pending state so the failsafe in CheckFailSafes() doesn't
        // fire a shot on the next frame before the component is fully dormant.
        pendingRanged.has = false;
        pendingMelee.has  = false;
    }

    void Update()
    {
        // Hard-stop if the player is dead — handles the edge case where OnDied
        // fires mid-frame and Update still runs once more before the component
        // is disabled by PlayerHealth.Die().
        var ph = GetComponent<PlayerHealth>();
        if (ph != null && ph.IsDead) return;

        // 1. Handle R key (toggle auto attack on/off)
        if (Input.GetKeyDown(toggleAutoAttackKey))
        {
            if (AutoAttackEnabled)
            {
                // turning it off is always allowed
                AutoAttackEnabled = false;
                NerdEmission_ForceOffImmediate();
            }
            else
            {
                // trying to turn it ON -> must have weapon
                if (HasWeaponEquipped())
                {
                    AutoAttackEnabled = true;
                }
                else
                {
                    AutoAttackEnabled = false;
                    NerdEmission_ForceOffImmediate();
                    if (toastIfNoWeapon)
                        toastIfNoWeapon.Show(needWeaponMessage, Color.red);
                }
            }
        }

        // 2. If it's on but we lost our weapon (unequipped mid combat), shut it off
        if (AutoAttackEnabled && !HasWeaponEquipped())
        {
            AutoAttackEnabled = false;
            NerdEmission_ForceOffImmediate();
            if (toastIfNoWeapon)
                toastIfNoWeapon.Show(needWeaponMessage, Color.red);
        }

        // 3. If we're not allowed to attack, just keep failsafes cleaned up
        if (!AutoAttackEnabled || IsSuppressedByOtherAbilities)
        {
            CheckFailSafes();
            return;
        }

        // 4. Attack pacing
        attackTimer -= Time.deltaTime;
        if (attackTimer <= 0f)
        {
            attackTimer = Mathf.Max(0.05f, weaponAttackInterval);
            DoAutoAttack();
        }

        // 5. Telegraph previews (live aiming during wind-up)
        if (pendingMelee.has && telegraphJockPreImpactPreview && Time.time >= pendingMelee.nextTelegraphAt)
        {
            var tilesNow = BuildMeleeTilesFromCurrent();
            ctx.TelegraphOnce(tilesNow);
            pendingMelee.nextTelegraphAt = Time.time + Mathf.Max(0.01f, telegraphRefreshInterval);
        }

        if (pendingRanged.has && telegraphNerdPreImpactPreview && Time.time >= pendingRanged.nextTelegraphAt)
        {
            var (sx, sz) = ctx.StepFromDir8(ctx.SnapDirTo8(transform.forward));
            if (sx != 0 || sz != 0)
            {
                TelegraphForCurrentPattern(sx, sz);
            }
            pendingRanged.nextTelegraphAt = Time.time + Mathf.Max(0.01f, telegraphRefreshInterval);
        }

        NerdEmission_Update();
        CheckFailSafes();
    }

    // =========================================================================
    // Telegraph helpers
    // =========================================================================

    /// <summary>
    /// Shows tile markers appropriate for the current shot pattern.
    /// Spread shows the three forward-facing 8-directions; Single / Burst show
    /// the straight path as usual.
    /// </summary>
    void TelegraphForCurrentPattern(int sx, int sz)
    {
        if (activePattern == WeaponShotPattern.Spread)
        {
            int     shots          = Mathf.Max(1, spreadShots);
            int     effectiveRange = Mathf.Max(1, Mathf.RoundToInt(weaponRangeTiles * spreadRangeMultiplier));
            float   halfArc        = spreadAngleDegrees * 0.5f;
            float   angleStep      = shots > 1 ? spreadAngleDegrees / (shots - 1) : 0f;
            Vector3 centerDir      = new Vector3(sx, 0, sz).normalized;
            Vector3 basePos        = ctx.Snap(transform.position);

            // Build each pellet's path by stepping along its actual float angle and
            // snapping each tile individually. This lets paths that start in the same
            // snapped direction naturally fan out after a tile or two, giving a proper
            // visual cone regardless of how many pellets there are.
            //
            // Dedup key = (firstTile x,z  +  lastTile x,z) so truly identical paths
            // collapse, but paths that diverge mid-way stay distinct.
            var seen = new HashSet<(int, int, int, int)>();

            for (int i = 0; i < shots; i++)
            {
                float   yAngle    = -halfArc + angleStep * i;
                Vector3 pelletDir = (Quaternion.Euler(0f, yAngle, 0f) * centerDir).normalized;

                var tiles = new List<Vector3>(effectiveRange);
                for (int step = 1; step <= effectiveRange; step++)
                    tiles.Add(ctx.Snap(basePos + pelletDir * ctx.tileSize * step));

                Vector3 first = tiles[0];
                Vector3 last  = tiles[tiles.Count - 1];
                var key = (
                    Mathf.RoundToInt(first.x / ctx.tileSize),
                    Mathf.RoundToInt(first.z / ctx.tileSize),
                    Mathf.RoundToInt(last.x  / ctx.tileSize),
                    Mathf.RoundToInt(last.z  / ctx.tileSize)
                );

                if (seen.Add(key))
                    ctx.TelegraphOnce(tiles);
            }
        }
        else
        {
            ctx.TelegraphOnce(ctx.GetRangedPathTiles(sx, sz, weaponRangeTiles));
        }
    }

    // =========================================================================
    // Emissive pulse
    // =========================================================================

    void NerdEmission_Update()
    {
        if (!enableNerdEmissivePulse || nerdEmissiveSlots == null || nerdEmissiveSlots.Length == 0)
            return;

        bool shouldPulse = AutoAttackEnabled
                        && !IsSuppressedByOtherAbilities
                        && ctx
                        && ctx.playerClass == PlayerAbilities.PlayerClass.Nerd;

        if (!shouldPulse)
        {
            for (int i = 0; i < nerdEmissiveSlots.Length; i++)
                nerdEmissiveSlots[i]?.DisableEmission();
            return;
        }

        float t = nerdEmissionPeriod <= 0.0001f ? 1f
                : (Time.time / Mathf.Max(0.0001f, nerdEmissionPeriod) + nerdEmissionPhaseOffset);

        float pulse01 = 0.5f * (1f + Mathf.Sin(t * Mathf.PI * 2f));
        float intensity = pulse01 * Mathf.Max(0f, nerdEmissionMax);

        Color emi = nerdEmissionColor * intensity;

        for (int i = 0; i < nerdEmissiveSlots.Length; i++)
            nerdEmissiveSlots[i]?.ApplyEmission(emi);
    }

    void NerdEmission_ForceOffImmediate()
    {
        if (nerdEmissiveSlots == null) return;
        for (int i = 0; i < nerdEmissiveSlots.Length; i++)
            nerdEmissiveSlots[i]?.DisableEmission();
    }

    // =========================================================================
    // DoAutoAttack
    // =========================================================================

    void DoAutoAttack()
    {
        if (!ctx) return;

        // Extra safety: if somehow we got here without a weapon, abort
        if (!HasWeaponEquipped())
        {
            AutoAttackEnabled = false;
            NerdEmission_ForceOffImmediate();
            if (toastIfNoWeapon)
                toastIfNoWeapon.Show(needWeaponMessage, Color.red);
            return;
        }

        // --- JOCK melee ---
        if (ctx.playerClass == PlayerAbilities.PlayerClass.Jock)
        {
            int final = ctx.stats
                ? ctx.stats.ComputeDamage(weaponDamage, PlayerStats.AbilitySchool.Jock, true, out _)
                : weaponDamage;

            pendingMelee.has         = true;
            pendingMelee.finalDamage = final;
            pendingMelee.timeoutAt   = Time.time + eventFailSafeSeconds;
            pendingMelee.nextTelegraphAt = 0f;

            var anim = ActiveAnimator;
            if (anim && !string.IsNullOrEmpty(jockAutoTrigger)) anim.SetTrigger(jockAutoTrigger);
            else AnimEvent_FireJockAutoAttack();
            return;
        }

        // --- NERD ranged ---
        if (ctx.playerClass == PlayerAbilities.PlayerClass.Nerd)
        {
            bool didCrit = false;
            int final = ctx.stats
                ? ctx.stats.ComputeDamage(weaponDamage, PlayerStats.AbilitySchool.Nerd, true, out didCrit)
                : weaponDamage;

            pendingRanged.has         = true;
            pendingRanged.finalDamage = final;
            pendingRanged.wasCrit     = didCrit;
            pendingRanged.timeoutAt   = Time.time + eventFailSafeSeconds;
            pendingRanged.nextTelegraphAt = 0f;

            var anim = ActiveAnimator;
            string trigger = !string.IsNullOrEmpty(activeAnimTrigger) ? activeAnimTrigger : nerdThrowTrigger;
            if (anim && !string.IsNullOrEmpty(trigger)) anim.SetTrigger(trigger);
            else AnimEvent_FireAutoAttack();
        }
    }

    // =========================================================================
    // Animation Event hooks
    // =========================================================================

    public void AnimEvent_FireAutoAttack()
    {
        if (!pendingRanged.has || ctx.playerClass != PlayerAbilities.PlayerClass.Nerd) return;

        Vector3 dir8 = ctx.SnapDirTo8(transform.forward);
        var (sx, sz) = ctx.StepFromDir8(dir8);
        if (sx == 0 && sz == 0)
        {
            pendingRanged.has = false;
            return;
        }

        // Telegraph the path(s) if not previewing live
        if (!telegraphNerdPreImpactPreview)
            TelegraphForCurrentPattern(sx, sz);

        if (ctx.projectilePrefab)
        {
            int   damage    = pendingRanged.finalDamage;
            bool  wasCrit   = pendingRanged.wasCrit;
            float fullRange = weaponRangeTiles * ctx.tileSize;

            switch (activePattern)
            {
                case WeaponShotPattern.Single:
                    FireProjectileInDirection(dir8, damage, fullRange, wasCrit);
                    break;

                case WeaponShotPattern.Spread:
                    FireSpread(dir8, damage, fullRange, wasCrit);
                    break;

                case WeaponShotPattern.Burst:
                    // Snapshot damage/crit into locals so the coroutine stays valid
                    // even if pendingRanged is cleared. Direction is re-sampled each
                    // shot inside FireBurst so rotating mid-burst steers correctly.
                    StartCoroutine(FireBurst(damage, fullRange, wasCrit));
                    break;
            }
        }

        pendingRanged.has = false;
    }

    // =========================================================================
    // Shot-pattern helpers
    // =========================================================================

    /// <summary>
    /// Fires pellets fanned out across <c>spreadAngleDegrees</c>.
    /// Damage is divided evenly per pellet so total potential DPS is
    /// comparable to a single shot.
    /// Range is reduced by <c>spreadRangeMultiplier</c>.
    /// </summary>
    void FireSpread(Vector3 centerDir, int totalDamage, float fullRange, bool wasCrit)
    {
        int   shots         = Mathf.Max(1, spreadShots);
        float range         = fullRange * Mathf.Clamp(spreadRangeMultiplier, 0.1f, 1f);
        int   damagePerShot = Mathf.Max(1, Mathf.RoundToInt(totalDamage * spreadDamageMultiplier));

        float halfArc   = spreadAngleDegrees * 0.5f;
        float angleStep = shots > 1 ? spreadAngleDegrees / (shots - 1) : 0f;

        for (int i = 0; i < shots; i++)
        {
            float yAngle = -halfArc + angleStep * i;
            Vector3 pelletDir = Quaternion.Euler(0f, yAngle, 0f) * centerDir;
            FireProjectileInDirection(pelletDir, damagePerShot, range, wasCrit);
        }
    }

    /// <summary>
    /// Fires <c>burstCount</c> projectiles one after another with
    /// <c>burstDelay</c> seconds between each shot, all in the same direction.
    /// </summary>
    IEnumerator FireBurst(int damage, float range, bool wasCrit)
    {
        int count = Mathf.Max(1, burstCount);
        for (int i = 0; i < count; i++)
        {
            if (ctx && ctx.projectilePrefab)
            {
                // Re-sample facing every shot so rotating the player mid-burst
                // steers subsequent projectiles correctly.
                Vector3 currentDir = ctx.SnapDirTo8(transform.forward);
                FireProjectileInDirection(currentDir, damage, range, wasCrit);
            }

            if (i < count - 1)
                yield return new WaitForSeconds(Mathf.Max(0.01f, burstDelay));
        }
    }

    /// <summary>
    /// Core projectile spawner. All shot patterns ultimately call this.
    /// </summary>
    void FireProjectileInDirection(Vector3 dir, int damage, float range, bool wasCrit)
    {
        if (!ctx || !ctx.projectilePrefab) return;

        Vector3 normDir = dir.normalized;
        if (normDir.sqrMagnitude < 0.0001f) return;

        Vector3 spawn = ctx.Snap(transform.position) + normDir * (ctx.tileSize * 0.1f);

        var go = Instantiate(ctx.projectilePrefab, spawn, Quaternion.LookRotation(normDir, Vector3.up));

        var rb = go.GetComponent<Rigidbody>() ?? go.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.detectCollisions = true;

        var sph = go.GetComponent<SphereCollider>() ?? go.AddComponent<SphereCollider>();
        sph.isTrigger = false;
        if (sph.radius < 0.08f) sph.radius = 0.08f;

        var p = go.GetComponent<StraightProjectile>() ?? go.AddComponent<StraightProjectile>();

        LayerMask impactLayers = ctx.targetLayer | ctx.wallLayer | ctx.groundLayer;

        p.Init(
            direction:   normDir,
            speed:       ctx.projectileSpeed,
            maxDistance: range,
            hitLayers:   impactLayers,
            damageLayers: ctx.targetLayer,
            damage:      damage,
            tileSize:    ctx.tileSize,
            wasCrit:     wasCrit
        );

        var vfx = go.GetComponent<ProjectileImpactVFX>() ?? go.AddComponent<ProjectileImpactVFX>();
        vfx.Configure(nerdImpactVfx, nerdImpactSfx, vfxScale, vfxLife, parentVfxToHit, impactLayers);
        vfx.ConfigureSweep(vfxUseSweepRaycast, impactLayers, vfxSweepRadius, false);
    }

    // =========================================================================

    public void AnimEvent_JockSwingWhoosh()
    {
        if (ctx.playerClass != PlayerAbilities.PlayerClass.Jock) return;
        if (!jockSwingWhooshSfx) return;

        Vector3 at = transform.position + Vector3.up * 1.2f;
        AudioSource.PlayClipAtPoint(jockSwingWhooshSfx, at, jockWhooshVolume);
    }

    public void AnimEvent_FireJockAutoAttack()
    {
        if (!pendingMelee.has || ctx.playerClass != PlayerAbilities.PlayerClass.Jock) return;

        var tilesNow = BuildMeleeTilesFromCurrent();

        bool hitAny = false;
        Vector3 firstHitPos = Vector3.zero;
        float r = ctx.tileSize * 0.45f;
        foreach (var c in tilesNow)
        {
            var cols = Physics.OverlapSphere(c + Vector3.up * 0.4f, r, ctx.targetLayer, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < cols.Length; i++)
            {
                var col = cols[i];
                if (col.TryGetComponent<IDamageable>(out _))
                {
                    hitAny = true;
                    firstHitPos = col.ClosestPoint(c);
                    goto DoneScan;
                }
            }
        }
    DoneScan:

        foreach (var c in tilesNow)
            ctx.DamageTileScaled(c, r, pendingMelee.finalDamage, PlayerStats.AbilitySchool.Jock, false);

        if (hitAny)
        {
            if (jockImpactSfx)
                AudioSource.PlayClipAtPoint(jockImpactSfx, firstHitPos, jockImpactVolume);

            if (jockImpactVfx)
            {
                var vfxGo = Instantiate(jockImpactVfx, firstHitPos, Quaternion.identity);
                vfxGo.transform.localScale *= jockImpactVfxScale;
                if (jockImpactVfxLife > 0f) Destroy(vfxGo, jockImpactVfxLife);
            }
        }

        pendingMelee.has = false;
    }

    // =========================================================================
    // Helpers / failsafes
    // =========================================================================

    void CheckFailSafes()
    {
        if (pendingRanged.has && Time.time >= pendingRanged.timeoutAt) AnimEvent_FireAutoAttack();
        if (pendingMelee.has  && Time.time >= pendingMelee.timeoutAt)  AnimEvent_FireJockAutoAttack();
    }

    bool HasWeaponEquipped()
    {
        // Weapon slot must NOT be null
        if (!equipment) return false;
        var w = equipment.Get(EquipSlot.Weapon);
        return (w != null && w.template != null);
    }

    Animator ActiveAnimator =>
        ctx && ctx.playerClass == PlayerAbilities.PlayerClass.Jock ? jockAnimator : nerdAnimator;

    List<Vector3> BuildMeleeTilesFromCurrent()
    {
        Vector3 dir8 = ctx.SnapDirTo8(transform.forward);
        var (sx, sz) = ctx.StepFromDir8(dir8);

        var tiles = new List<Vector3>(3);
        Vector3 self = ctx.Snap(transform.position);
        bool diagonal = (sx != 0 && sz != 0);

        if (!diagonal)
        {
            Vector3 f = new Vector3(sx, 0, sz);
            Vector3 r = new Vector3(f.z, 0, -f.x); // right = 90°
            Vector3 rowCenter = self + f * ctx.tileSize;
            tiles.Add(rowCenter - r * ctx.tileSize);
            tiles.Add(rowCenter);
            tiles.Add(rowCenter + r * ctx.tileSize);
        }
        else
        {
            Vector3 baseTile = self + new Vector3(sx, 0, sz) * ctx.tileSize;
            tiles.Add(baseTile);
            tiles.Add(baseTile + new Vector3(sx, 0, 0) * ctx.tileSize);
            tiles.Add(baseTile + new Vector3(0, 0, sz) * ctx.tileSize);
        }

        return tiles;
    }
}
