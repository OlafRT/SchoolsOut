using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A persistent trigger box that deals heavy tick damage to anything IDamageable inside it.
/// Place and size the BoxCollider in the scene to cover the area shown by the green line.
/// Activate() is called by BossTriggerZone when the player enters the kitchen.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class BossHazardZone : MonoBehaviour
{
    [Header("Damage")]
    [Tooltip("Damage applied every tick to anything inside the zone.")]
    public int tickDamage = 30;
    public float tickInterval = 0.5f;
    [Tooltip("Only colliders with this tag take damage. Leave as 'Player' so NPCs walking through are unaffected.")]
    public string victimTag = "Player";

    [Header("Telegraph Markers")]
    [Tooltip("Tile marker prefab to cover the zone floor — warns the player visually.")]
    public GameObject tileMarkerPrefab;
    public float markerYOffset = 0.02f;
    public float tileSize = 1f;
    public Color markerColor = new Color(0.15f, 0.9f, 0.15f, 0.85f);
    public LayerMask groundMask = ~0;

    [Header("Visual warning (optional)")]
    [Tooltip("Optional particle / VFX object that plays while the zone is live.")]
    public GameObject zoneVfx;

    bool active = false;
    readonly HashSet<Collider> inside = new();
    readonly List<GameObject> spawnedMarkers = new();

    void Awake()
    {
        var col = GetComponent<BoxCollider>();
        col.isTrigger = true;
        gameObject.SetActive(false);
    }

    public void Activate()
    {
        gameObject.SetActive(true);
        active = true;
        if (zoneVfx) zoneVfx.SetActive(true);
        SpawnMarkers();
        StartCoroutine(TickLoop());
    }

    public void Deactivate()
    {
        active = false;
        StopAllCoroutines();
        if (zoneVfx) zoneVfx.SetActive(false);
        foreach (var m in spawnedMarkers) if (m) Destroy(m);
        spawnedMarkers.Clear();
        gameObject.SetActive(false);
    }

    void SpawnMarkers()
    {
        if (!tileMarkerPrefab) return;

        var col = GetComponent<BoxCollider>();
        if (!col) return;

        Bounds bounds = new Bounds(
            transform.TransformPoint(col.center),
            Vector3.Scale(transform.lossyScale, col.size));

        // Cover the zone floor with tile markers on a grid
        float halfX = bounds.extents.x;
        float halfZ = bounds.extents.z;
        Vector3 center = bounds.center;

        for (float x = -halfX + tileSize * 0.5f; x < halfX; x += tileSize)
        {
            for (float z = -halfZ + tileSize * 0.5f; z < halfZ; z += tileSize)
            {
                Vector3 worldPos = center + new Vector3(x, 0, z);
                // Sample ground Y
                float groundY = worldPos.y;
                if (Physics.Raycast(worldPos + Vector3.up * 5f, Vector3.down, out var hit, 20f, groundMask, QueryTriggerInteraction.Ignore))
                    groundY = hit.point.y;

                worldPos.y = groundY + markerYOffset;

                var m = Instantiate(tileMarkerPrefab, worldPos, Quaternion.identity);
                TintMarker(m, markerColor);
                // Give them a very long lifetime — Deactivate() will clean them up
                if (!m.TryGetComponent<TileMarker>(out var tm)) tm = m.AddComponent<TileMarker>();
                tm.Init(99999f, tileSize);
                spawnedMarkers.Add(m);
            }
        }
    }

    static void TintMarker(GameObject m, Color col)
    {
        if (!m) return;
        if (m.TryGetComponent<Renderer>(out var rend) && rend.material) { rend.material.color = col; return; }
        if (m.TryGetComponent<SpriteRenderer>(out var sr)) { sr.color = col; return; }
        if (m.TryGetComponent<UnityEngine.UI.Image>(out var img)) { img.color = col; return; }
        var childR = m.GetComponentInChildren<Renderer>();
        if (childR && childR.material) childR.material.color = col;
    }

    void OnTriggerEnter(Collider other)
    {
        inside.Add(other);
    }

    void OnTriggerExit(Collider other)
    {
        inside.Remove(other);
    }

    IEnumerator TickLoop()
    {
        while (active)
        {
            yield return new WaitForSeconds(tickInterval);

            // Snapshot to avoid mutation during iteration
            Collider[] snapshot = new Collider[inside.Count];
            inside.CopyTo(snapshot);

            foreach (var col in snapshot)
            {
                if (!col) { inside.Remove(col); continue; }

                // Only damage objects with the victim tag (default: "Player")
                // This prevents slime minions walking through from taking friendly-fire damage.
                if (!string.IsNullOrEmpty(victimTag) && !col.CompareTag(victimTag))
                {
                    // Also check parent in case the tagged root owns a child collider
                    if (col.GetComponentInParent<Transform>() is Transform t && !t.CompareTag(victimTag))
                        continue;
                }

                IDamageable dmg = null;
                col.TryGetComponent(out dmg);
                if (dmg == null) dmg = col.GetComponentInParent<IDamageable>();
                dmg?.ApplyDamage(tickDamage);

                if (dmg != null && CombatTextManager.Instance)
                {
                    Vector3 pos = col.bounds.center;
                    pos.y = col.bounds.max.y;
                    CombatTextManager.Instance.ShowDamage(pos, tickDamage, false, col.transform, lifetimeOverride: 0.6f);
                }
            }
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.15f, 0.9f, 0.15f, 0.2f);
        var col = GetComponent<BoxCollider>();
        if (col)
            Gizmos.DrawCube(
                transform.TransformPoint(col.center),
                Vector3.Scale(transform.lossyScale, col.size));

        Gizmos.color = new Color(0.15f, 0.9f, 0.15f, 0.8f);
        if (GetComponent<BoxCollider>() is BoxCollider bc)
            Gizmos.DrawWireCube(
                transform.TransformPoint(bc.center),
                Vector3.Scale(transform.lossyScale, bc.size));
    }
}
