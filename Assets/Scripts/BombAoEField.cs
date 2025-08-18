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
    float slowPercent;
    float slowDuration;

    readonly List<GameObject> markers = new();

    [Header("Visuals")]
    public Color telegraphColor = new Color(0.3f, 0.9f, 0.3f, 0.85f);

    [Header("Ground Sample")]
    [Tooltip("Mask used to find the floor for the telegraph Y. Exclude FX/markers.")]
    public LayerMask groundMask = ~0;   // set in Inspector if you want to restrict to 'Ground' layer only
    [Tooltip("How high above the center to start the downward ray.")]
    public float sampleRayHeight = 5f;

    float baseGroundY;                  // <- single sampled height for all tiles

    public void Init(PlayerAbilities ctx, Vector3 center, int radiusTiles, float duration, float tickInterval, int tickDamage, float slowPercent, float slowDuration)
    {
        this.ctx = ctx;
        this.center = ctx.Snap(center);
        this.radiusTiles = Mathf.Max(0, radiusTiles);
        this.duration = Mathf.Max(0.01f, duration);
        this.tickInterval = Mathf.Max(0.05f, tickInterval);
        this.tickDamage = Mathf.Max(0, tickDamage);
        this.slowPercent = Mathf.Clamp01(slowPercent);
        this.slowDuration = Mathf.Max(0.01f, slowDuration);

        var holder = GameObject.Find("__Runtime_AOE") ?? new GameObject("__Runtime_AOE");
        transform.SetParent(holder.transform);

        // Sample the floor ONCE before we spawn any markers/VFX so we never hit them.
        baseGroundY = SampleGroundY(this.center, this.groundMask, this.sampleRayHeight, fallback: this.center.y);

        DrawTelegraph();
        StartCoroutine(TickLoop());
    }

    float SampleGroundY(Vector3 at, LayerMask mask, float rayHeight, float fallback)
    {
        Vector3 origin = at + Vector3.up * Mathf.Abs(rayHeight);
        if (Physics.Raycast(origin, Vector3.down, out var hit, rayHeight * 2f, mask, QueryTriggerInteraction.Ignore))
            return hit.point.y;
        return fallback;
    }

    void DrawTelegraph()
    {
        if (!ctx || !ctx.tileMarkerPrefab) return;

        foreach (var c in ctx.GetDiamondTiles(center, radiusTiles))
        {
            Vector3 pos = c;
            pos.y = baseGroundY + ctx.markerYOffset; // <- single consistent Y

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
        var hits = new Collider[16];

        while (Time.time < tEnd)
        {
            foreach (var c in ctx.GetDiamondTiles(center, radiusTiles))
            {
                int count = Physics.OverlapSphereNonAlloc(c + Vector3.up * 0.4f, ctx.tileSize * 0.45f, hits, ctx.targetLayer, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    var col = hits[i];
                    if (!col) continue;

                    if (tickDamage > 0 && col.TryGetComponent<IDamageable>(out var dmg))
                        dmg.ApplyDamage(tickDamage);

                    if (col.TryGetComponent<NPCMovement>(out var move))
                        move.ApplySlow(slowPercent, slowDuration);
                    else
                    {
                        var m = col.GetComponentInParent<NPCMovement>();
                        if (m) m.ApplySlow(slowPercent, slowDuration);
                    }
                }
            }

            yield return new WaitForSeconds(tickInterval);
        }

        Destroy(gameObject);
    }
}
