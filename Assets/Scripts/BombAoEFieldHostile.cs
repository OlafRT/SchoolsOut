using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Lingering AoE that damages/slows anything on victimsLayer. 
/// Parameters are injected by NPCBombAbility.
/// Shows status icon via NPCStatusHost if present (NPCs only).
/// Also drives PlayerHUD "Slowed" banner with a static ref-count so
/// multiple overlapping fields behave correctly.
/// </summary>
public class BombAoEFieldHostile : MonoBehaviour
{
    // ----- player HUD slow banner ref-count -----
    static int s_activePlayerSlowCount = 0;
    bool weWereAffectingPlayer = false;

    // injected
    float tileSize;
    GameObject markerPrefab;
    float markerYOffset;
    LayerMask groundMask;
    LayerMask victimsLayer;
    int radiusTiles;
    float duration;
    float tickInterval;
    int tickDamage;
    float slowPercent;
    string slowTag;
    Sprite slowIcon;

    readonly List<GameObject> markers = new();

    [Header("Visuals")]
    public Color telegraphColor = new Color(0.3f, 0.9f, 0.3f, 0.85f); // green stink

    float baseGroundY;

    // Track occupants (for aura add/remove)
    readonly HashSet<NPCMovement> npcInsideNow = new();
    readonly HashSet<NPCMovement> npcInsidePrev = new();
    readonly HashSet<PlayerMovement> playerInsideNow = new();
    readonly HashSet<PlayerMovement> playerInsidePrev = new();

    public void Init(
        Vector3 center,
        float tileSize,
        GameObject markerPrefab,
        float markerYOffset,
        LayerMask groundMask,
        LayerMask victimsLayer,
        int radiusTiles,
        float duration,
        float tickInterval,
        int tickDamage,
        float slowPercent,
        string slowTag,
        Sprite slowIcon)
    {
        this.tileSize = Mathf.Max(0.01f, tileSize);
        this.markerPrefab = markerPrefab;
        this.markerYOffset = markerYOffset;
        this.groundMask = groundMask;
        this.victimsLayer = victimsLayer;

        this.radiusTiles = Mathf.Max(0, radiusTiles);
        this.duration = Mathf.Max(0.01f, duration);
        this.tickInterval = Mathf.Max(0.05f, tickInterval);
        this.tickDamage = Mathf.Max(0, tickDamage);
        this.slowPercent = Mathf.Clamp01(slowPercent);
        this.slowTag = slowTag;
        this.slowIcon = slowIcon;

        var holder = GameObject.Find("__Runtime_AOE") ?? new GameObject("__Runtime_AOE");
        transform.SetParent(holder.transform);

        center = Snap(center);
        baseGroundY = SampleGroundY(center);

        DrawTelegraph(center);
        StartCoroutine(TickLoop(center));
    }

    void DrawTelegraph(Vector3 center)
    {
        if (!markerPrefab) return;

        foreach (var c in GetDiamondTiles(center, radiusTiles))
        {
            Vector3 pos = c; pos.y = baseGroundY + markerYOffset;
            var m = Instantiate(markerPrefab, pos, Quaternion.identity, transform);
            TintMarker(m, telegraphColor);

            if (!m.TryGetComponent<TileMarker>(out var tm)) tm = m.AddComponent<TileMarker>();
            tm.Init(duration, tileSize);

            markers.Add(m);
        }
    }

    IEnumerator TickLoop(Vector3 center)
    {
        float tEnd = Time.time + duration;
        var hits = new Collider[24];

        while (Time.time < tEnd)
        {
            // swap sets
            npcInsidePrev.Clear();
            foreach (var m in npcInsideNow) npcInsidePrev.Add(m);
            npcInsideNow.Clear();

            playerInsidePrev.Clear();
            foreach (var p in playerInsideNow) playerInsidePrev.Add(p);
            playerInsideNow.Clear();

            foreach (var c in GetDiamondTiles(center, radiusTiles))
            {
                int count = Physics.OverlapSphereNonAlloc(
                    c + Vector3.up * 0.4f,
                    tileSize * 0.45f,
                    hits,
                    victimsLayer,
                    QueryTriggerInteraction.Ignore);

                for (int i = 0; i < count; i++)
                {
                    var col = hits[i];
                    if (!col) continue;

                    // DoT damage (flat)
                    if (tickDamage > 0 && col.TryGetComponent<IDamageable>(out var dmg))
                    {
                        dmg.ApplyDamage(tickDamage);

                        if (CombatTextManager.Instance)
                        {
                            Vector3 pos = col.bounds.center; pos.y = col.bounds.max.y;
                            CombatTextManager.Instance.ShowDamage(pos, tickDamage, false, col.transform, lifetimeOverride: 0.6f);
                        }
                    }

                    // Apply / refresh aura to NPCs
                    if (col.TryGetComponent<NPCMovement>(out var npcMv) || (npcMv = col.GetComponentInParent<NPCMovement>()))
                    {
                        npcInsideNow.Add(npcMv);
                        npcMv.SetAuraMultiplier(this, Mathf.Clamp01(1f - slowPercent));
                        var host = npcMv.GetComponent<NPCStatusHost>();
                        if (host) host.AddOrRefreshAura(slowTag, this, slowIcon);
                    }

                    // Apply / refresh aura to Player
                    if (col.TryGetComponent<PlayerMovement>(out var pMv) || (pMv = col.GetComponentInParent<PlayerMovement>()))
                    {
                        playerInsideNow.Add(pMv);
                        pMv.SetAuraMultiplier(this, Mathf.Clamp01(1f - slowPercent));
                        // HUD banner control handled below via ref-count
                    }
                }
            }

            // Remove aura from NPCs who left
            foreach (var was in npcInsidePrev)
            {
                if (!npcInsideNow.Contains(was))
                {
                    was.ClearAura(this);
                    var hostWas = was.GetComponent<NPCStatusHost>();
                    if (hostWas) hostWas.RemoveAura(slowTag, this);
                }
            }

            // Remove aura from Player who left
            foreach (var was in playerInsidePrev)
            {
                if (!playerInsideNow.Contains(was))
                {
                    was.ClearAura(this);
                }
            }

            // ---- HUD slow banner ref-count transitions ----
            bool nowAffecting = playerInsideNow.Count > 0;
            if (nowAffecting && !weWereAffectingPlayer)
            {
                weWereAffectingPlayer = true;
                s_activePlayerSlowCount++;
                PlayerHUD.SetSlowed(true);
            }
            else if (!nowAffecting && weWereAffectingPlayer)
            {
                weWereAffectingPlayer = false;
                s_activePlayerSlowCount = Mathf.Max(0, s_activePlayerSlowCount - 1);
                PlayerHUD.SetSlowed(s_activePlayerSlowCount > 0);
            }

            yield return new WaitForSeconds(tickInterval);
        }

        // Clear all on end
        foreach (var m in npcInsideNow)
        {
            m.ClearAura(this);
            var host = m.GetComponent<NPCStatusHost>();
            if (host) host.RemoveAura(slowTag, this);
        }
        foreach (var p in playerInsideNow)
            p.ClearAura(this);

        // Final HUD ref-count cleanup (in case field ends while affecting the player)
        if (weWereAffectingPlayer)
        {
            weWereAffectingPlayer = false;
            s_activePlayerSlowCount = Mathf.Max(0, s_activePlayerSlowCount - 1);
            PlayerHUD.SetSlowed(s_activePlayerSlowCount > 0);
        }

        Destroy(gameObject);
    }

    // ---------- helpers ----------
    Vector3 Snap(Vector3 p) =>
        new Vector3(Mathf.Round(p.x / tileSize) * tileSize, p.y, Mathf.Round(p.z / tileSize) * tileSize);

    float SampleGroundY(Vector3 at)
    {
        Vector3 origin = at + Vector3.up * 10f;
        if (Physics.Raycast(origin, Vector3.down, out var hit, 40f, groundMask, QueryTriggerInteraction.Ignore))
            return hit.point.y;
        return at.y;
    }

    IEnumerable<Vector3> GetDiamondTiles(Vector3 center, int radius)
    {
        center = Snap(center);
        for (int dx = -radius; dx <= radius; dx++)
        {
            int maxDz = radius - Mathf.Abs(dx);
            for (int dz = -maxDz; dz <= maxDz; dz++)
                yield return new Vector3(center.x + dx * tileSize, center.y, center.z + dz * tileSize);
        }
    }

    static void TintMarker(GameObject m, Color col)
    {
        if (!m) return;
        if (m.TryGetComponent<Renderer>(out var rend) && rend.material) { rend.material.color = col; return; }
        if (m.TryGetComponent<SpriteRenderer>(out var sr)) { sr.color = col; return; }
        if (m.TryGetComponent<Image>(out var img)) { img.color = col; return; }
        var childR = m.GetComponentInChildren<Renderer>(); if (childR && childR.material) childR.material.color = col;
    }
}
