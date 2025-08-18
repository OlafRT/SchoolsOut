using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class BombAoEField : MonoBehaviour
{
    PlayerAbilities ctx;
    Vector3 center;
    int radiusTiles;
    float duration;
    float tickInterval;
    int tickDamage;

    [Range(0f, 0.95f)] float slowPercent;
    float slowDuration; // kept for inspector/back-compat (unused with aura)

    readonly List<GameObject> markers = new();

    [Header("Visuals")]
    public Color telegraphColor = new Color(0.3f, 0.9f, 0.3f, 0.85f);

    [Header("Ground Sample")]
    public LayerMask groundMask = 0; // set to Ground layer; falls back to ctx.groundLayer
    public float sampleRayHeight = 10f;

    [Header("Status UI")]
    public string slowStatusTag = "Slow";
    public Sprite slowStatusIcon;

    float baseGroundY;

    // Track who’s currently inside so we can add/remove aura properly
    readonly HashSet<NPCMovement> insideNow = new();
    readonly HashSet<NPCMovement> insidePrev = new();

    public void Init(PlayerAbilities ctx, Vector3 center, int radiusTiles, float duration, float tickInterval, int tickDamage, float slowPercent, float slowDuration)
    {
        this.ctx = ctx;
        this.center = ctx.Snap(center);
        this.radiusTiles = Mathf.Max(0, radiusTiles);
        this.duration = Mathf.Max(0.01f, duration);
        this.tickInterval = Mathf.Max(0.05f, tickInterval);
        this.tickDamage = Mathf.Max(0, tickDamage);
        this.slowPercent = Mathf.Clamp01(slowPercent);
        this.slowDuration = Mathf.Max(0.01f, slowDuration); // not used for aura, but kept

        var holder = GameObject.Find("__Runtime_AOE") ?? new GameObject("__Runtime_AOE");
        transform.SetParent(holder.transform);

        baseGroundY = SampleGroundYStrict(this.center, this.sampleRayHeight,
            (groundMask.value != 0 ? groundMask.value :
             (ctx && ctx.groundLayer.value != 0 ? ctx.groundLayer.value : 0)),
            fallbackY: this.center.y);

        DrawTelegraph();
        StartCoroutine(TickLoop());
    }

    float SampleGroundYStrict(Vector3 at, float rayHeight, int mask, float fallbackY)
    {
        Vector3 origin = at + Vector3.up * Mathf.Abs(rayHeight);
        if (mask != 0 &&
            Physics.Raycast(origin, Vector3.down, out var hit, rayHeight * 2f, mask, QueryTriggerInteraction.Ignore))
        {
            return hit.point.y;
        }
        return fallbackY;
    }

    void DrawTelegraph()
    {
        if (!ctx || !ctx.tileMarkerPrefab) return;

        foreach (var c in ctx.GetDiamondTiles(center, radiusTiles))
        {
            Vector3 pos = c;
            pos.y = baseGroundY + ctx.markerYOffset;

            var m = Instantiate(ctx.tileMarkerPrefab, pos, Quaternion.identity, transform);
            TintMarker(m, telegraphColor);

            if (!m.TryGetComponent<TileMarker>(out var tm)) tm = m.AddComponent<TileMarker>();
            tm.Init(duration, ctx.tileSize);

            markers.Add(m);
        }
    }

    static void TintMarker(GameObject m, Color col)
    {
        if (m.TryGetComponent<Renderer>(out var rend) && rend.material) { rend.material.color = col; return; }
        if (m.TryGetComponent<SpriteRenderer>(out var sr)) { sr.color = col; return; }
        if (m.TryGetComponent<Image>(out var img)) { img.color = col; return; }
        var childRend = m.GetComponentInChildren<Renderer>();
        if (childRend && childRend.material) childRend.material.color = col;
    }

    IEnumerator TickLoop()
    {
        float tEnd = Time.time + duration;
        var hits = new Collider[24];

        while (Time.time < tEnd)
        {
            // swap sets
            insidePrev.Clear();
            foreach (var m in insideNow) insidePrev.Add(m);
            insideNow.Clear();

            // Scan all tiles in the diamond
            foreach (var c in ctx.GetDiamondTiles(center, radiusTiles))
            {
                int count = Physics.OverlapSphereNonAlloc(
                    c + Vector3.up * 0.4f,
                    ctx.tileSize * 0.45f,
                    hits,
                    ctx.targetLayer,
                    QueryTriggerInteraction.Ignore);

                for (int i = 0; i < count; i++)
                {
                    var col = hits[i];
                    if (!col) continue;

                    if (tickDamage > 0 && col.TryGetComponent<IDamageable>(out var dmg))
                        dmg.ApplyDamage(tickDamage);

                    NPCMovement move = null;
                    if (!col.TryGetComponent<NPCMovement>(out move))
                        move = col.GetComponentInParent<NPCMovement>();

                    if (move)
                    {
                        insideNow.Add(move);
                        // apply/refresh aura slow (multiplier = 1 - slowPercent)
                        move.SetAuraMultiplier(this, Mathf.Clamp01(1f - slowPercent));

                        var host = move.GetComponent<NPCStatusHost>();
                        if (host) host.AddOrRefreshAura(slowStatusTag, this, slowStatusIcon);
                    }
                }
            }

            // Clear aura+status from anyone who left
            foreach (var was in insidePrev)
            {
                if (!insideNow.Contains(was))
                {
                    was.ClearAura(this);
                    var hostWas = was.GetComponent<NPCStatusHost>();
                    if (hostWas) hostWas.RemoveAura(slowStatusTag, this);
                }
            }

            yield return new WaitForSeconds(tickInterval);
        }

        // Field ended → clear aura+status from anyone still inside
        foreach (var m in insideNow)
        {
            m.ClearAura(this);
            var host = m.GetComponent<NPCStatusHost>();
            if (host) host.RemoveAura(slowStatusTag, this);
        }

        Destroy(gameObject);
    }
}