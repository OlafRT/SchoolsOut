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

    // Exposed so abilities can check movement
    [HideInInspector] public PlayerMovement movement;

    // Optional shared camera for mouse-aim abilities (Bomb)
    [HideInInspector] public Camera aimCamera;

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        aimCamera = Camera.main;
    }

    // ------------- Shared helpers (abilities call these) -------------

    public Vector3 Snap(Vector3 p)
    {
        return new Vector3(
            Mathf.Round(p.x / tileSize) * tileSize,
            p.y,
            Mathf.Round(p.z / tileSize) * tileSize
        );
    }

    public Vector3 SnapDirTo8(Vector3 v)
    {
        v.y = 0f;
        if (v.sqrMagnitude < 0.0001f) return Vector3.zero;
        float ang = Mathf.Atan2(v.z, v.x) * Mathf.Rad2Deg;
        if (ang < 0f) ang += 360f;
        int step = Mathf.RoundToInt(ang / 45f) % 8;
        switch (step)
        {
            case 0:  return new Vector3( 1,0, 0);                 // E
            case 1:  return new Vector3( 1,0, 1).normalized;      // NE
            case 2:  return new Vector3( 0,0, 1);                 // N
            case 3:  return new Vector3(-1,0, 1).normalized;      // NW
            case 4:  return new Vector3(-1,0, 0);                 // W
            case 5:  return new Vector3(-1,0,-1).normalized;      // SW
            case 6:  return new Vector3( 0,0,-1);                 // S
            default: return new Vector3( 1,0,-1).normalized;      // SE
        }
    }

    public (int sx, int sz) StepFromDir8(Vector3 dir8)
    {
        int sx = Mathf.Abs(dir8.x) < 0.5f ? 0 : (dir8.x > 0 ? 1 : -1);
        int sz = Mathf.Abs(dir8.z) < 0.5f ? 0 : (dir8.z > 0 ? 1 : -1);
        return (sx, sz);
    }

    public bool TryGetGroundHeight(Vector3 tileCenter, out float groundY)
    {
        float h = Mathf.Max(0.1f, groundRaycastHeight);
        int mask = (groundLayer.value != 0) ? groundLayer.value : Physics.DefaultRaycastLayers;
        if (Physics.Raycast(tileCenter + Vector3.up * h, Vector3.down, out RaycastHit hit, h * 2f, mask, QueryTriggerInteraction.Ignore))
        {
            groundY = hit.point.y;
            return true;
        }
        groundY = tileCenter.y;
        return false;
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
            // row ahead (3-wide)
            int rx = -sz, rz = sx;
            Vector3 rowCenter = basePos + new Vector3(sx, 0, sz) * tileSize;
            yield return rowCenter + new Vector3(-rx, 0, -rz) * tileSize;
            yield return rowCenter;
            yield return rowCenter + new Vector3( rx, 0,  rz) * tileSize;
        }
        else
        {
            // diagonal wedge {A, center, B}
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
