using UnityEngine;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class PlayerAbilities : MonoBehaviour
{
    public enum PlayerClass { Jock, Nerd }

    [Header("Player")]
    public PlayerClass playerClass = PlayerClass.Jock;
    public int playerLevel = 1;
    public List<string> learnedAbilities = new List<string>();

    [Header("Grid")]
    public float tileSize = 1f;

    [Header("Targets / Ground")]
    public LayerMask targetLayer;
    public LayerMask wallLayer;
    public LayerMask groundLayer;

    [Header("Telegraph")]
    public GameObject tileMarkerPrefab;
    public float telegraphDuration = 0.25f;
    public float groundRaycastHeight = 3f;
    public float markerYOffset = 0.001f;

    [Header("Projectiles (Nerd auto)")]
    public GameObject projectilePrefab;
    public float projectileSpeed = 12f;

    [HideInInspector] public PlayerMovement movement;
    [HideInInspector] public Camera aimCamera;

    // NEW: Stats hook
    [HideInInspector] public PlayerStats stats;

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        aimCamera = Camera.main;
        stats = GetComponent<PlayerStats>();
    }

    public Vector3 Snap(Vector3 p) =>
        new Vector3(Mathf.Round(p.x / tileSize) * tileSize, p.y, Mathf.Round(p.z / tileSize) * tileSize);

    public Vector3 SnapDirTo8(Vector3 v)
    {
        v.y = 0f;
        if (v.sqrMagnitude < 0.0001f) return Vector3.zero;
        float ang = Mathf.Atan2(v.z, v.x) * Mathf.Rad2Deg; if (ang < 0f) ang += 360f;
        int step = Mathf.RoundToInt(ang / 45f) % 8;
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

    public (int sx, int sz) StepFromDir8(Vector3 dir8)
    {
        int sx = Mathf.Abs(dir8.x) < 0.5f ? 0 : (dir8.x > 0 ? 1 : -1);
        int sz = Mathf.Abs(dir8.z) < 0.5f ? 0 : (dir8.z > 0 ? 1 : -1);
        return (sx, sz);
    }

    // Hardened: masked ray → unmasked ray → fallback
    public bool TryGetGroundHeight(Vector3 tileCenter, out float groundY, bool strict = false)
    {
        float h = Mathf.Max(0.1f, groundRaycastHeight);
        int mask = (groundLayer.value == 0) ? 0 : groundLayer.value;
        Vector3 origin = tileCenter + Vector3.up * h;

        if (mask != 0)
        {
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit ghit, h * 2f, mask, QueryTriggerInteraction.Ignore))
            {
                groundY = ghit.point.y; return true;
            }
            if (strict) { groundY = tileCenter.y; return false; }
        }

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, h * 2f, ~0, QueryTriggerInteraction.Ignore))
        {
            groundY = hit.point.y; return true;
        }
        groundY = tileCenter.y; return false;
    }

    public void TelegraphOnce(IEnumerable<Vector3> tileCenters)
    {
        if (!tileMarkerPrefab) return;
        foreach (var c in tileCenters)
        {
            Vector3 pos = c;
            if (TryGetGroundHeight(c, out float gy)) pos.y = gy + markerYOffset;
            else pos.y = c.y + markerYOffset;

            var m = Object.Instantiate(tileMarkerPrefab, pos, Quaternion.identity);
            if (!m.TryGetComponent<TileMarker>(out var tm)) tm = m.AddComponent<TileMarker>();
            tm.Init(telegraphDuration, tileSize);
        }
    }

    // --------- NEW: scaled damage helpers ---------

    // Spawns floating number + applies scaled (and possibly crit) damage to a single collider.
    public void ApplyDamageToCollider(Collider col, int baseDamage, PlayerStats.AbilitySchool school, bool allowCrit = true)
    {
        if (!col || baseDamage <= 0) return;

        int final = baseDamage;
        bool didCrit = false;

        if (stats)
            final = stats.ComputeDamage(baseDamage, school, allowCrit, out didCrit);

        // Floating number at target head
        if (CombatTextManager.Instance)
        {
            Vector3 pos = col.bounds.center;
            pos.y = col.bounds.max.y;
            CombatTextManager.Instance.ShowDamage(pos, final, didCrit, col.transform);
        }

        if (col.TryGetComponent<IDamageable>(out var dmg))
            dmg.ApplyDamage(final);
    }
    // Applies damage to everything in a tile-radius (calls the helper above per collider).
    public void DamageTileScaled(Vector3 tileCenter, float radius, int baseDamage, PlayerStats.AbilitySchool school, bool allowCrit = true)
    {
        var hits = Physics.OverlapSphere(tileCenter + Vector3.up * 0.5f, radius, targetLayer, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
            ApplyDamageToCollider(h, baseDamage, school, allowCrit);
    }

    // (legacy) unscaled version still here for reference
    public void DamageTile(Vector3 tileCenter, float radius, int damage)
    {
        var hits = Physics.OverlapSphere(tileCenter + Vector3.up * 0.5f, radius, targetLayer, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
            if (h.TryGetComponent<IDamageable>(out var dmg)) dmg.ApplyDamage(damage);
    }

    public IEnumerable<Vector3> GetRangedPathTiles(int sx, int sz, int rangeTiles)
    {
        Vector3 basePos = Snap(transform.position);
        for (int i = 1; i <= rangeTiles; i++)
            yield return basePos + new Vector3(sx * i * tileSize, 0, sz * i * tileSize);
    }

    public IEnumerable<Vector3> GetMeleeTiles(int sx, int sz)
    {
        Vector3 basePos = Snap(transform.position);
        bool diagonal = (sx != 0 && sz != 0);

        if (!diagonal)
        {
            int rx = -sz, rz = sx;
            Vector3 rowCenter = basePos + new Vector3(sx, 0, sz) * tileSize;
            yield return rowCenter + new Vector3(-rx, 0, -rz) * tileSize;
            yield return rowCenter;
            yield return rowCenter + new Vector3( rx, 0,  rz) * tileSize;
        }
        else
        {
            yield return basePos + new Vector3(sx, 0, 0) * tileSize;
            yield return basePos + new Vector3(sx, 0, sz) * tileSize;
            yield return basePos + new Vector3(0 , 0, sz) * tileSize;
        }
    }

    public IEnumerable<Vector3> GetDiamondTiles(Vector3 centerTile, int radius)
    {
        Vector3 c = Snap(centerTile);
        int r = Mathf.Max(0, radius);
        yield return c;
        for (int d = 1; d <= r; d++)
        {
            for (int dx = -d; dx <= d; dx++)
            {
                int dz = d - Mathf.Abs(dx);
                if (dz == 0) yield return c + new Vector3(dx * tileSize, 0, 0);
                else
                {
                    yield return c + new Vector3(dx * tileSize, 0,  dz * tileSize);
                    yield return c + new Vector3(dx * tileSize, 0, -dz * tileSize);
                }
            }
        }
    }

    public bool HasAbility(string abilityName) =>
        !string.IsNullOrEmpty(abilityName) && learnedAbilities != null && learnedAbilities.Contains(abilityName);
}

// Shared interfaces
public interface IDamageable { void ApplyDamage(int amount); }
public interface IStunnable { void ApplyStun(float seconds); }
