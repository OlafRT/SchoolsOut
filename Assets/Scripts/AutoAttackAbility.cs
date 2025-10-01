using UnityEngine;
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

    #region Animation
    [Header("Animation (set both explicitly)")]
    [SerializeField] private Animator nerdAnimator;
    [SerializeField] private Animator jockAnimator;

    [SerializeField] private string nerdThrowTrigger = "Throw";       // Nerd ranged
    [SerializeField] private string jockAutoTrigger  = "AutoAttack";  // Jock melee
    [SerializeField] private float eventFailSafeSeconds = 1.0f;       // both paths
    #endregion

    [Header("Impact VFX (Nerd projectile)")]
    [SerializeField] private GameObject nerdImpactVfx;
    [SerializeField] private AudioClip nerdImpactSfx;
    [SerializeField] private float vfxScale = 1f;
    [SerializeField] private float vfxLife = 2f;
    [SerializeField] private bool parentVfxToHit = false;
    [SerializeField] private bool vfxUseSweepRaycast = true;
    [SerializeField] private float vfxSweepRadius = 0.08f;

    [Header("Telegraph (Nerd)")]
    [Tooltip("While the attack is winding up, show a preview that follows your current aim.")]
    [SerializeField] private bool telegraphNerdPreImpactPreview = true;

    [Header("Telegraph (Jock)")]
    [Tooltip("While the attack is winding up, show a preview that follows your current aim.")]
    [SerializeField] private bool telegraphJockPreImpactPreview = true;

    [Tooltip("How often to refresh the moving preview (seconds).")]
    [SerializeField] private float telegraphRefreshInterval = 0.05f;

    [Header("Jock Melee Audio/VFX")]
    [Tooltip("Whoosh played during the swing (animation event).")]
    [SerializeField] private AudioClip jockSwingWhooshSfx;
    [Range(0f, 1f)] [SerializeField] private float jockWhooshVolume = 1f;

    [Tooltip("Impact SFX played only if the swing actually hits something.")]
    [SerializeField] private AudioClip jockImpactSfx;
    [Range(0f, 1f)] [SerializeField] private float jockImpactVolume = 1f;

    [Tooltip("Optional impact VFX spawned at the first hit point (only on hit).")]
    [SerializeField] private GameObject jockImpactVfx;
    [SerializeField] private float jockImpactVfxLife = 1.5f;
    [SerializeField] private float jockImpactVfxScale = 1f;

    [System.Serializable]
    public class EmissiveSlot
    {
        public Renderer renderer;
        [Tooltip("Material index on the renderer (0-based).")]
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
            // Optional: if your pipeline requires the keyword to light up, uncomment:
            // var mats = renderer.materials; // instantiates per-renderer
            // if (mats != null && materialIndex >= 0 && materialIndex < mats.Length)
            // {
            //     if (emissionColor.maxColorComponent > 0f) mats[materialIndex].EnableKeyword("_EMISSION");
            //     else mats[materialIndex].DisableKeyword("_EMISSION");
            // }
        }

        public void DisableEmission()
        {
            ApplyEmission(Color.black);
        }
    }

    [Header("Nerd Auto-Attack Emissive Pulse")]
    [SerializeField] private bool enableNerdEmissivePulse = true;
    [SerializeField] private EmissiveSlot[] nerdEmissiveSlots;   // assign the weapon renderer twice with index 0 and 1
    [SerializeField] private Color nerdEmissionColor = Color.cyan;
    [Tooltip("Max HDR intensity multiplier for the emission color.")]
    [SerializeField] [Range(0f, 8f)] private float nerdEmissionMax = 2.0f;
    [Tooltip("Seconds per full brighten->dim cycle.")]
    [SerializeField] [Range(0.1f, 5f)] private float nerdEmissionPeriod = 1.0f;
    [Tooltip("Optional phase offset so multiple materials don't peak at the exact same time.")]
    [SerializeField] [Range(0f, 1f)] private float nerdEmissionPhaseOffset = 0f;

    private PlayerAbilities ctx;
    private float attackTimer;

    public bool AutoAttackEnabled { get; private set; } = false;
    public bool IsSuppressedByOtherAbilities { get; set; }

    // ---------- pending (Nerd ranged) ----------
    private struct PendingRanged
    {
        public bool has;
        // dir/step kept for back-compat but no longer used for impact; we recompute at event time.
        public Vector3 dir8;
        public int stepX, stepZ;
        public int finalDamage;
        public bool wasCrit;
        public float timeoutAt;

        // preview helper
        public float nextTelegraphAt;
    }
    private PendingRanged pendingRanged;

    // ---------- pending (Jock melee) ----------
    private struct PendingMelee
    {
        public bool has;
        public int finalDamage;
        public float timeoutAt;

        // preview helper
        public float nextTelegraphAt;
    }
    private PendingMelee pendingMelee;

    void Awake()
    {
        ctx = GetComponent<PlayerAbilities>();

        if (!nerdAnimator || !jockAnimator)
        {
            var anims = GetComponentsInChildren<Animator>(true);
            foreach (var a in anims)
            {
                var n = a.gameObject.name.ToLowerInvariant();
                if (!nerdAnimator && (n.Contains("nerd") || n.Contains("mage") || n.Contains("ranged"))) nerdAnimator = a;
                else if (!jockAnimator && (n.Contains("jock") || n.Contains("melee") || n.Contains("warrior"))) jockAnimator = a;
            }
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleAutoAttackKey))
        {
            AutoAttackEnabled = !AutoAttackEnabled;
            if (!AutoAttackEnabled) NerdEmission_ForceOffImmediate();
        }

        if (!AutoAttackEnabled || IsSuppressedByOtherAbilities)
        {
            CheckFailSafes();
            return;
        }

        attackTimer -= Time.deltaTime;
        if (attackTimer <= 0f)
        {
            attackTimer = Mathf.Max(0.05f, weaponAttackInterval);
            DoAutoAttack();
        }

        // live preview telegraph for Jock while pending
        if (pendingMelee.has && telegraphJockPreImpactPreview && Time.time >= pendingMelee.nextTelegraphAt)
        {
            var tilesNow = BuildMeleeTilesFromCurrent();
            ctx.TelegraphOnce(tilesNow); // short lifetime/fade assumed
            pendingMelee.nextTelegraphAt = Time.time + Mathf.Max(0.01f, telegraphRefreshInterval);
        }

        // live preview telegraph for Nerd while pending
        if (pendingRanged.has && telegraphNerdPreImpactPreview && Time.time >= pendingRanged.nextTelegraphAt)
        {
            var (sx, sz) = ctx.StepFromDir8(ctx.SnapDirTo8(transform.forward));
            if (sx != 0 || sz != 0)
            {
                var pathNow = ctx.GetRangedPathTiles(sx, sz, weaponRangeTiles);
                ctx.TelegraphOnce(pathNow);
            }
            pendingRanged.nextTelegraphAt = Time.time + Mathf.Max(0.01f, telegraphRefreshInterval);
        }

        NerdEmission_Update();
        CheckFailSafes();
    }

    Animator ActiveAnimator =>
        ctx && ctx.playerClass == PlayerAbilities.PlayerClass.Jock ? jockAnimator : nerdAnimator;

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

        // Pulse 0..1 with a sine, then scale by max intensity
        float t = nerdEmissionPeriod <= 0.0001f ? 1f
                : (Time.time / Mathf.Max(0.0001f, nerdEmissionPeriod) + nerdEmissionPhaseOffset);
        float pulse01 = 0.5f * (1f + Mathf.Sin(t * Mathf.PI * 2f));
        float intensity = pulse01 * Mathf.Max(0f, nerdEmissionMax);

        // Use HDR emission color (URP/HDRP happy), gamma pipeline also fine
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

    void DoAutoAttack()
    {
        if (!ctx) return;

        // --- JOCK (melee with event) ---
        if (ctx.playerClass == PlayerAbilities.PlayerClass.Jock)
        {
            int final = ctx.stats
                ? ctx.stats.ComputeDamage(weaponDamage, PlayerStats.AbilitySchool.Jock, true, out _)
                : weaponDamage;

            pendingMelee.has         = true;
            pendingMelee.finalDamage = final;
            pendingMelee.timeoutAt   = Time.time + eventFailSafeSeconds;
            pendingMelee.nextTelegraphAt = 0f; // show preview ASAP if enabled

            var anim = ActiveAnimator;
            if (anim && !string.IsNullOrEmpty(jockAutoTrigger)) anim.SetTrigger(jockAutoTrigger);
            else AnimEvent_FireJockAutoAttack(); // fallback if animator missing
            return;
        }

        // --- NERD (projectile with event) ---
        if (ctx.playerClass == PlayerAbilities.PlayerClass.Nerd)
        {
            // compute damage now to lock in crit etc. for this throw
            bool didCrit = false;
            int final = ctx.stats
                ? ctx.stats.ComputeDamage(weaponDamage, PlayerStats.AbilitySchool.Nerd, true, out didCrit)
                : weaponDamage;

            pendingRanged.has         = true;
            pendingRanged.finalDamage = final;
            pendingRanged.wasCrit     = didCrit;
            pendingRanged.timeoutAt   = Time.time + eventFailSafeSeconds;
            pendingRanged.nextTelegraphAt = 0f; // show preview ASAP if enabled

            var anim = ActiveAnimator;
            if (anim && !string.IsNullOrEmpty(nerdThrowTrigger)) anim.SetTrigger(nerdThrowTrigger);
            else AnimEvent_FireAutoAttack(); // fallback
        }
    }

    // =======================
    //  Animation Event hooks
    // =======================

    // Nerd ranged (projectile) — recompute AT IMPACT so it matches where you ended up
    public void AnimEvent_FireAutoAttack()
    {
        if (!pendingRanged.has || ctx.playerClass != PlayerAbilities.PlayerClass.Nerd) return;

        // Current facing/step at the exact event moment
        Vector3 dir8 = ctx.SnapDirTo8(transform.forward);
        var (sx, sz) = ctx.StepFromDir8(dir8);
        if (sx == 0 && sz == 0)
        {
            // nothing to fire; clear pending to avoid spamming failsafe
            pendingRanged.has = false;
            return;
        }

        // Build the live path and (optionally) flash once if you disabled live preview
        var pathNow = ctx.GetRangedPathTiles(sx, sz, weaponRangeTiles);
        if (!telegraphNerdPreImpactPreview) ctx.TelegraphOnce(pathNow);

        if (ctx.projectilePrefab)
        {
            // Spawn from current snapped position, half a tile forward
            Vector3 spawn = ctx.Snap(transform.position)
                           + new Vector3(sx, 0, sz) * (ctx.tileSize * 0.5f);

            var go = Instantiate(ctx.projectilePrefab, spawn, Quaternion.LookRotation(dir8, Vector3.up));

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
                direction: dir8,
                speed: ctx.projectileSpeed,
                maxDistance: weaponRangeTiles * ctx.tileSize,
                hitLayers: impactLayers,
                damageLayers: ctx.targetLayer,
                damage: pendingRanged.finalDamage,
                tileSize: ctx.tileSize,
                wasCrit: pendingRanged.wasCrit
            );

            var vfx = go.GetComponent<ProjectileImpactVFX>() ?? go.AddComponent<ProjectileImpactVFX>();
            vfx.Configure(nerdImpactVfx, nerdImpactSfx, vfxScale, vfxLife, parentVfxToHit, impactLayers);
            vfx.ConfigureSweep(vfxUseSweepRaycast, impactLayers, vfxSweepRadius, false);
        }

        pendingRanged.has = false;
    }

    // Jock whoosh (play from an earlier animation event, before the impact)
    public void AnimEvent_JockSwingWhoosh()
    {
        if (ctx.playerClass != PlayerAbilities.PlayerClass.Jock) return;
        if (!jockSwingWhooshSfx) return;

        Vector3 at = transform.position + Vector3.up * 1.2f;
        AudioSource.PlayClipAtPoint(jockSwingWhooshSfx, at, jockWhooshVolume);
    }

    // Jock melee impact (recompute tiles AT IMPACT; play impact SFX/VFX only if we hit something)
    public void AnimEvent_FireJockAutoAttack()
    {
        if (!pendingMelee.has || ctx.playerClass != PlayerAbilities.PlayerClass.Jock) return;

        var tilesNow = BuildMeleeTilesFromCurrent();

        // Detect if we actually hit something on target layer
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
                var vfx = Instantiate(jockImpactVfx, firstHitPos, Quaternion.identity);
                vfx.transform.localScale *= jockImpactVfxScale;
                if (jockImpactVfxLife > 0f) Destroy(vfx, jockImpactVfxLife);
            }
        }

        pendingMelee.has = false;
    }

    // =======================
    //  Helpers / failsafes
    // =======================

    void CheckFailSafes()
    {
        if (pendingRanged.has && Time.time >= pendingRanged.timeoutAt) AnimEvent_FireAutoAttack();
        if (pendingMelee.has  && Time.time >= pendingMelee.timeoutAt)  AnimEvent_FireJockAutoAttack();
    }

    // Build the 3-tile fan based on CURRENT facing & position
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
