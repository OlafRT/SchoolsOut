using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class NPCAutoAttack : MonoBehaviour
{
    [Header("Enable / Gating")]
    public bool abilityEnabled = true;
    [Tooltip("Only cast when hostile to the player.")]
    public bool requireHostileToPlayer = true;

    [Header("Weapon")]
    [Tooltip("Seconds between swings/shots.")]
    public float weaponAttackInterval = 2.5f;
    [Tooltip("Flat damage per swing/shot.")]
    public int weaponDamage = 10;
    [Tooltip("Tiles forward for ranged attack (Nerd).")]
    public int weaponRangeTiles = 6;
    [Tooltip("Projectile speed (units/sec) for Nerd ranged.")]
    public float projectileSpeed = 12f;

    [Header("Wind-up Telegraph")]
    [Tooltip("Seconds to show the telegraph BEFORE executing the attack.")]
    public float windupSeconds = 0.6f;
    public GameObject tileMarkerPrefab;
    public Color telegraphColor = new Color(1f, 0.9f, 0.2f, 0.85f); // warm yellow
    [Tooltip("Small vertical offset to avoid Z-fighting.")]
    public float markerYOffset = 0.02f;
    [Tooltip("Ground layers for grounding telegraphs.")]
    public LayerMask groundMask = ~0;

    [Header("Targeting / Layers")]
    [Tooltip("Layer that the player Body/Collider is on.")]
    public LayerMask playerLayer = 0; // set this to your Player layer in the inspector

    [Header("Grid")]
    public float tileSize = 1f;

    [Header("Ranged Projectile")]
    public GameObject projectilePrefab;     // reuse your StraightProjectile-compatible prefab

    // Refs
    NPCAI ai;
    NPCMovement mover;
    Transform player;

    // timer
    float attackTimer = 0f;

    // temp windup visuals
    readonly List<GameObject> windupMarkers = new();

    void Awake()
    {
        ai = GetComponent<NPCAI>();
        mover = GetComponent<NPCMovement>();
        if (!player)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
        }
        if (mover) tileSize = mover.tileSize;
    }

    void Update()
    {
        if (!abilityEnabled || !ai || !mover || !player) return;
        if (requireHostileToPlayer && ai.CurrentHostility != NPCAI.Hostility.Hostile) return;

        attackTimer -= Time.deltaTime;
        if (attackTimer > 0f) return;

        // ready to attempt an auto-attack
        if (ai.faction == NPCFaction.Jock)
        {
            TryAutoMelee();
        }
        else if (ai.faction == NPCFaction.Nerd)
        {
            TryAutoRanged();
        }

        attackTimer = Mathf.Max(0.05f, weaponAttackInterval);
    }

    // ======================
    //      MELEE (Jock)
    // ======================
    void TryAutoMelee()
    {
        Vector3 forward8 = SnapDirTo8(transform.forward);
        (int sx, int sz) = StepFromDir8(forward8);
        if (sx == 0 && sz == 0) return;

        // Are we adjacent-ish to the player so this makes sense?
        if (!WithinTiles(transform.position, player.position, 2)) return;

        // Build 3 tiles in front (with proper diagonal handling)
        var hits = GetMeleeTiles(sx, sz);

        // Telegraph them during wind-up, then execute
        StartCoroutine(DoWindupAndMelee(hits));
    }

    IEnumerator DoWindupAndMelee(List<Vector3> tiles)
    {
        ShowTelegraph(tiles, windupSeconds);
        yield return new WaitForSeconds(Mathf.Max(0.05f, windupSeconds));
        ClearTelegraph();

        // Damage player if inside any of those tiles
        foreach (var c in tiles)
            DamageTile(c, tileSize * 0.45f, weaponDamage, playerLayer);
    }

    // ======================
    //     RANGED (Nerd)
    // ======================
    void TryAutoRanged()
    {
        Vector3 forward8 = SnapDirTo8(transform.forward);
        (int sx, int sz) = StepFromDir8(forward8);
        if (sx == 0 && sz == 0) return;

        // Build straight path tiles
        var path = GetRangedPathTiles(sx, sz, weaponRangeTiles);

        // Telegraph during wind-up, then fire
        StartCoroutine(DoWindupAndShoot(path, forward8, sx, sz));
    }

    IEnumerator DoWindupAndShoot(List<Vector3> pathTiles, Vector3 dir8, int sx, int sz)
    {
        ShowTelegraph(pathTiles, windupSeconds);
        yield return new WaitForSeconds(Mathf.Max(0.05f, windupSeconds));
        ClearTelegraph();

        if (!projectilePrefab) yield break;   // <-- FIXED

        Vector3 spawn = Snap(transform.position) + new Vector3(sx, 0, sz) * (tileSize * 0.5f);
        var go = Instantiate(projectilePrefab, spawn, Quaternion.LookRotation(dir8, Vector3.up));
        var p = go.GetComponent<StraightProjectile>();
        if (!p) p = go.AddComponent<StraightProjectile>();

        p.Init(
            direction: dir8,
            speed: projectileSpeed,
            maxDistance: weaponRangeTiles * tileSize,
            targetLayer: playerLayer,          // hit the PLAYER
            damage: weaponDamage,
            tileSize: tileSize
        );
    }

    // ======================
    //      Telegraphing
    // ======================
    void ShowTelegraph(IEnumerable<Vector3> tileCenters, float lifetime)
    {
        ClearTelegraph();

        foreach (var c in tileCenters)
        {
            Vector3 pos = c;
            float gy = SampleGroundY(c);
            pos.y = gy + markerYOffset;

            var m = Instantiate(tileMarkerPrefab, pos, Quaternion.identity);
            TintMarker(m, telegraphColor);

            if (!m.TryGetComponent<TileMarker>(out var tm)) tm = m.AddComponent<TileMarker>();
            tm.Init(lifetime, tileSize);

            windupMarkers.Add(m);
        }
    }

    void ClearTelegraph()
    {
        foreach (var m in windupMarkers) if (m) Destroy(m);
        windupMarkers.Clear();
    }

    float SampleGroundY(Vector3 at)
    {
        Vector3 origin = at + Vector3.up * 10f;
        if (Physics.Raycast(origin, Vector3.down, out var hit, 40f, groundMask, QueryTriggerInteraction.Ignore))
            return hit.point.y;
        return at.y;
    }

    // ======================
    //       Damage
    // ======================
    void DamageTile(Vector3 tileCenter, float radius, int damage, LayerMask victimLayer)
    {
        var hits = Physics.OverlapSphere(tileCenter + Vector3.up * 0.4f, radius, victimLayer, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            if (h.TryGetComponent<IDamageable>(out var dmg))
            {
                dmg.ApplyDamage(damage);

                if (CombatTextManager.Instance)
                {
                    Vector3 pos = h.bounds.center; pos.y = h.bounds.max.y;
                    CombatTextManager.Instance.ShowDamage(pos, damage, false, h.transform, lifetimeOverride: 0.6f);
                }
            }
        }
    }

    // ======================
    //       Helpers
    // ======================
    Vector3 Snap(Vector3 p) =>
        new Vector3(Mathf.Round(p.x / tileSize) * tileSize, p.y,
                    Mathf.Round(p.z / tileSize) * tileSize);

    Vector3 SnapDirTo8(Vector3 v)
    {
        v.y = 0f;
        if (v.sqrMagnitude < 0.0001f) return Vector3.zero;
        float a = Mathf.Atan2(v.z, v.x) * Mathf.Rad2Deg; if (a < 0f) a += 360f;
        int step = Mathf.RoundToInt(a / 45f) % 8;
        return step switch
        {
            0 => new Vector3( 1,0, 0),
            1 => new Vector3( 1,0, 1).normalized,
            2 => new Vector3( 0,0, 1),
            3 => new Vector3(-1,0, 1).normalized,
            4 => new Vector3(-1,0, 0),
            5 => new Vector3(-1,0,-1).normalized,
            6 => new Vector3( 0,0,-1),
            _ => new Vector3( 1,0,-1).normalized,
        };
    }

    (int sx, int sz) StepFromDir8(Vector3 dir8)
    {
        Vector3 d = dir8.normalized;
        int sx = Mathf.RoundToInt(d.x);
        int sz = Mathf.RoundToInt(d.z);
        if (sx > 1) sx = 1; if (sx < -1) sx = -1;
        if (sz > 1) sz = 1; if (sz < -1) sz = -1;
        return (sx, sz);
    }

    List<Vector3> GetMeleeTiles(int sx, int sz)
    {
        Vector3 self = Snap(transform.position);
        var tiles = new List<Vector3>(3);

        bool diagonal = (sx != 0 && sz != 0);

        if (!diagonal)
        {
            Vector3 f = new Vector3(sx, 0, sz);
            Vector3 r = new Vector3(f.z, 0, -f.x); // right = 90°
            Vector3 rowCenter = self + f * tileSize;
            tiles.Add(rowCenter - r * tileSize);
            tiles.Add(rowCenter);
            tiles.Add(rowCenter + r * tileSize);
        }
        else
        {
            Vector3 baseTile = self + new Vector3(sx, 0, sz) * tileSize;
            tiles.Add(baseTile);
            tiles.Add(baseTile + new Vector3(sx, 0, 0) * tileSize);
            tiles.Add(baseTile + new Vector3(0, 0, sz) * tileSize);
        }

        return tiles;
    }

    List<Vector3> GetRangedPathTiles(int sx, int sz, int rangeTiles)
    {
        Vector3 start = Snap(transform.position) + new Vector3(sx, 0, sz) * tileSize; // first tile ahead
        var tiles = new List<Vector3>(rangeTiles);
        for (int i = 0; i < rangeTiles; i++)
            tiles.Add(start + new Vector3(sx, 0, sz) * (i * tileSize));
        return tiles;
    }

    bool WithinTiles(Vector3 a, Vector3 b, int tiles)
    {
        float dx = Mathf.Abs(a.x - b.x);
        float dz = Mathf.Abs(a.z - b.z);
        float cheb = Mathf.Max(dx, dz) / Mathf.Max(0.01f, tileSize);
        return cheb <= tiles + 0.001f;
    }

    static void TintMarker(GameObject m, Color col)
    {
        if (!m) return;
        if (m.TryGetComponent<Renderer>(out var rend) && rend.material) { rend.material.color = col; return; }
        if (m.TryGetComponent<SpriteRenderer>(out var sr)) { sr.color = col; return; }
        if (m.TryGetComponent<UnityEngine.UI.Image>(out var img)) { img.color = col; return; }
        var childR = m.GetComponentInChildren<Renderer>(); if (childR && childR.material) childR.material.color = col;
    }
}
