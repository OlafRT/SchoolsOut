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

    private PlayerAbilities ctx;
    private float attackTimer;
    public bool AutoAttackEnabled { get; private set; } = true;

    void Awake() => ctx = GetComponent<PlayerAbilities>();

    public bool IsSuppressedByOtherAbilities { get; set; } // set by Charge/Bomb each frame if needed

    void Update()
    {
        if (Input.GetKeyDown(toggleAutoAttackKey))
            AutoAttackEnabled = !AutoAttackEnabled;

        if (!AutoAttackEnabled || IsSuppressedByOtherAbilities) return;

        attackTimer -= Time.deltaTime;
        if (attackTimer <= 0f)
        {
            attackTimer = Mathf.Max(0.05f, weaponAttackInterval);
            DoAutoAttack();
        }
    }

    void DoAutoAttack()
    {
        Vector3 dir8 = ctx.SnapDirTo8(transform.forward);
        var (sx, sz) = ctx.StepFromDir8(dir8);
        if (sx == 0 && sz == 0) return;

        if (ctx.playerClass == PlayerAbilities.PlayerClass.Jock)
        {
            // Melee: three tiles in front
            var tiles = ctx.GetMeleeTiles(sx, sz);
            ctx.TelegraphOnce(tiles);
            foreach (var c in ctx.GetMeleeTiles(sx, sz))
                ctx.DamageTile(c, ctx.tileSize * 0.45f, weaponDamage);
        }
        else // Nerd ranged
        {
            var path = ctx.GetRangedPathTiles(sx, sz, weaponRangeTiles);
            ctx.TelegraphOnce(path);

            if (ctx.projectilePrefab)
            {
                Vector3 spawn = ctx.Snap(transform.position) + new Vector3(sx, 0, sz) * (ctx.tileSize * 0.5f);
                var go = Instantiate(ctx.projectilePrefab, spawn, Quaternion.LookRotation(dir8, Vector3.up));
                var p = go.GetComponent<StraightProjectile>();
                if (!p) p = go.AddComponent<StraightProjectile>();
                p.Init(dir8, ctx.projectileSpeed, weaponRangeTiles * ctx.tileSize, ctx.targetLayer, weaponDamage, ctx.tileSize);
            }
        }
    }
}
