using UnityEngine;

[DisallowMultipleComponent]
public class AutoAttackAbility : MonoBehaviour
{
    [Header("Input")]
    public KeyCode toggleAutoAttackKey = KeyCode.R;

    [Header("Weapon")]
    public float weaponAttackInterval = 2.5f;
    public int weaponDamage = 10;
    public int weaponRangeTiles = 6;

    [Header("Animation")]
    [SerializeField] private Animator animator;                  // auto-found in Awake if left null
    [SerializeField] private string nerdThrowTrigger = "Throw";  // trigger on Nerd throw clip

    [Header("Anim Event Safety")]
    [Tooltip("If the animation event never arrives, fire automatically after this many seconds.")]
    [SerializeField] private float eventFailSafeSeconds = 1.0f;

    [Header("Impact VFX (Nerd projectile)")]
    [SerializeField] private GameObject nerdImpactVfx;   // ParticleSystem prefab (loop off)
    [SerializeField] private AudioClip nerdImpactSfx;    // optional
    [SerializeField] private float vfxScale = 1f;
    [SerializeField] private float vfxLife = 2f;
    [SerializeField] private bool parentVfxToHit = false;
    [SerializeField] private bool vfxUseSweepRaycast = true;     // <<< NEW: enable the per-frame sweep
    [SerializeField] private float vfxSweepRadius = 0.08f;       // spherecast radius for fast projectiles

    private PlayerAbilities ctx;
    private float attackTimer;

    // START DISABLED by default (as requested)
    public bool AutoAttackEnabled { get; private set; } = false;

    public bool IsSuppressedByOtherAbilities { get; set; }

    // ---------- pending shot (waits for the animation event) ----------
    private struct PendingShot
    {
        public bool has;
        public Vector3 dir8;
        public int stepX, stepZ;
        public int finalDamage;
        public bool wasCrit;
        public float timeoutAt;
    }
    private PendingShot pending;

    void Awake()
    {
        ctx = GetComponent<PlayerAbilities>();
        if (!animator) animator = GetComponentInChildren<Animator>(true);
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleAutoAttackKey))
            AutoAttackEnabled = !AutoAttackEnabled;

        // Allow a pending anim-event to complete even if auto-attack is toggled off mid-throw
        if (!AutoAttackEnabled || IsSuppressedByOtherAbilities)
        {
            CheckFailSafe();
            return;
        }

        attackTimer -= Time.deltaTime;
        if (attackTimer <= 0f)
        {
            attackTimer = Mathf.Max(0.05f, weaponAttackInterval);
            DoAutoAttack();
        }

        CheckFailSafe();
    }

    void DoAutoAttack()
    {
        // Only Nerd uses the throw anim/event. (Jock branch intentionally omitted.)
        if (ctx.playerClass != PlayerAbilities.PlayerClass.Nerd) return;

        Vector3 dir8 = ctx.SnapDirTo8(transform.forward);
        var (sx, sz) = ctx.StepFromDir8(dir8);
        if (sx == 0 && sz == 0) return;

        // Compute final damage now so crit/SFX stay in sync with this shot
        bool didCrit = false;
        int final = ctx.stats
            ? ctx.stats.ComputeDamage(weaponDamage, PlayerStats.AbilitySchool.Nerd, true, out didCrit)
            : weaponDamage;

        // Arm a pending shot and trigger the animation
        pending.has         = true;
        pending.dir8        = dir8;
        pending.stepX       = sx;
        pending.stepZ       = sz;
        pending.finalDamage = final;
        pending.wasCrit     = didCrit;
        pending.timeoutAt   = Time.time + eventFailSafeSeconds;

        if (animator && !string.IsNullOrEmpty(nerdThrowTrigger))
            animator.SetTrigger(nerdThrowTrigger);
    }

    // ------------------------ Animation Event ------------------------
    // Put this as an Animation Event on the Nerd "Throw" clip (or via a relay on the Animator object)
    public void AnimEvent_FireAutoAttack()
    {
        if (!pending.has || ctx.playerClass != PlayerAbilities.PlayerClass.Nerd) return;

        // Draw telegraph at release time
        var path = ctx.GetRangedPathTiles(pending.stepX, pending.stepZ, weaponRangeTiles);
        ctx.TelegraphOnce(path);

        // Spawn projectile
        if (ctx.projectilePrefab)
        {
            Vector3 spawn = ctx.Snap(transform.position)
                           + new Vector3(pending.stepX, 0, pending.stepZ) * (ctx.tileSize * 0.5f);

            var go = Instantiate(ctx.projectilePrefab, spawn, Quaternion.LookRotation(pending.dir8, Vector3.up));

            // Your existing straight projectile init
            var p = go.GetComponent<StraightProjectile>();
            if (!p) p = go.AddComponent<StraightProjectile>();
            p.Init(
                direction: pending.dir8,
                speed: ctx.projectileSpeed,
                maxDistance: weaponRangeTiles * ctx.tileSize,
                targetLayer: ctx.targetLayer,
                damage: pending.finalDamage,
                tileSize: ctx.tileSize,
                wasCrit: pending.wasCrit
            );

            // --- Add / configure impact VFX spawner (spawns particles+SFX on hit) ---
            var vfx = go.GetComponent<ProjectileImpactVFX>();
            if (!vfx) vfx = go.AddComponent<ProjectileImpactVFX>();

            // layers to spawn on: targets + walls + ground from PlayerAbilities
            LayerMask spawnLayers = ctx.targetLayer | ctx.wallLayer | ctx.groundLayer;

            vfx.Configure(
                vfxPrefab: nerdImpactVfx,
                sfx: nerdImpactSfx,
                scale: vfxScale,
                life: vfxLife,
                parent: parentVfxToHit,
                layers: spawnLayers
            );

            // enable sweep so we catch raycast-only projectiles too
            vfx.ConfigureSweep(
                enable: vfxUseSweepRaycast,
                layers: spawnLayers,
                radius: vfxSweepRadius,
                destroyProjectileOnHit: false  // keep false if StraightProjectile already destroys itself
            );
        }

        pending.has = false;
    }

    // If the event never fires (wrong clip name, mis-typed function, blend never reaches the frame), fail-safe.
    void CheckFailSafe()
    {
        if (pending.has && Time.time >= pending.timeoutAt)
        {
            Debug.LogWarning("AutoAttackAbility: Throw animation event missed—firing fail-safe.");
            AnimEvent_FireAutoAttack();
        }
    }
}
