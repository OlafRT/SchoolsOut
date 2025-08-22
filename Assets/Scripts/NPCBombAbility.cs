using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class NPCBombAbility : MonoBehaviour
{
    [Header("Gate")]
    public bool requireNerdFaction = true;

    [Header("Casting")]
    public int maxRangeTiles = 6;
    public float windupSeconds = 1.25f;
    public float cooldownSeconds = 6f;

    [Header("Throw")]
    public GameObject bombPrefab;
    public float bombThrowTimePerTile = 0.08f;
    public float bombArcHeight = 1.5f;
    public GameObject explosionVfxPrefab;

    [Header("Lingering Field")]
    public int bombRadiusTiles = 1;
    public float lingerDuration = 3f;
    public float tickInterval = 0.5f;
    public int tickDamage = 5;
    [Range(0f, 0.95f)] public float slowPercent = 0.40f;

    [Header("Status UI")]
    public string slowStatusTag = "Slow";
    public Sprite slowStatusIcon;

    [Header("Targeting")]
    public LayerMask victimLayer;
    public LayerMask groundMask = ~0;

    [Header("Grid + Visuals")]
    public float tileSize = 1f;
    public GameObject tileMarkerPrefab;
    public float markerYOffset = 0.02f;
    public Color windupColor = new Color(1f, 0.2f, 0.1f, 0.85f);

    // --- New: smarter leading controls ---
    [Header("Leading (Prediction)")]
    [Tooltip("Max tiles to lead ahead of the player.")]
    public int leadMaxTiles = 2;
    [Tooltip("Blend 0..1 between current tile (0) and predicted tile (1).")]
    [Range(0f, 1f)] public float leadBlend = 0.5f;
    [Tooltip("Ignore leading if player avg speed < this (tiles/sec).")]
    public float minLeadSpeedTilesPerSec = 0.3f;
    [Tooltip("How many seconds of movement history to smooth over.")]
    public float historyWindowSeconds = 0.6f;

    // Refs
    NPCAI ai;
    NPCMovement mover;
    Transform player;

    float nextReadyTime = 0f;
    readonly List<GameObject> windupMarkers = new();

    // ---- Leading history ----
    struct Sample { public Vector3 snapPos; public float time; }
    readonly Queue<Sample> samples = new Queue<Sample>(8);

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
        // seed a sample so we start sane
        var s = new Sample { snapPos = Snap(transform.position), time = Time.time };
        samples.Enqueue(s);
    }

    void Update()
    {
        // Update player movement history (snapped)
        if (player)
        {
            var now = new Sample { snapPos = Snap(player.position), time = Time.time };
            if (samples.Count == 0 || (now.snapPos - samples.Peek().snapPos).sqrMagnitude > 0.0001f)
                samples.Enqueue(now);

            // Trim old samples beyond window
            while (samples.Count > 0 && (Time.time - samples.Peek().time) > Mathf.Max(0.1f, historyWindowSeconds))
                samples.Dequeue();
        }

        if (!CanConsiderCasting()) return;

        if (Time.time >= nextReadyTime && ai.CurrentHostility == NPCAI.Hostility.Hostile && player)
        {
            Vector3 self = transform.position;
            Vector3 pt = player.position;
            if (!WithinTiles(self, pt, maxRangeTiles)) return;

            Vector3 targetCenter = PredictTargetCenter();
            StartCoroutine(CastRoutine(targetCenter));
            nextReadyTime = Time.time + cooldownSeconds;
        }
    }

    bool CanConsiderCasting()
    {
        if (!ai || !mover || !bombPrefab || !tileMarkerPrefab) return false;
        if (requireNerdFaction && ai.faction != NPCFaction.Nerd) return false;
        return true;
    }

    // --------- NEW: robust, tile-aware prediction ----------
    Vector3 PredictTargetCenter()
    {
        if (!player) return Snap(transform.position);

        // Current snapped positions
        Vector3 npcSnap = Snap(transform.position);
        Vector3 pNow = Snap(player.position);

        // Compute smoothed velocity in tiles/sec from oldest to newest sample
        if (samples.Count < 2) return pNow;

        Sample oldest = default;
        Sample newest = default;
        foreach (var s in samples) { oldest = oldest.time == 0f ? s : oldest; newest = s; }

        float dt = Mathf.Max(0.0001f, newest.time - oldest.time);
        Vector3 deltaWorld = newest.snapPos - oldest.snapPos;
        // world units/sec
        Vector3 vWorld = deltaWorld / dt;
        // tiles/sec (in world)
        float tilesPerSec = vWorld.magnitude / Mathf.Max(0.0001f, tileSize);

        // If barely moving, don't lead at all
        if (tilesPerSec < minLeadSpeedTilesPerSec)
            return pNow;

        // Estimate total time until impact (windup + flight)
        float distNow = Vector3.Distance(npcSnap, pNow);
        float tilesNow = Mathf.Max(1f, distNow / Mathf.Max(0.0001f, tileSize));
        float flight = tilesNow * bombThrowTimePerTile;
        float totalTime = windupSeconds + flight;

        // Predict, then cap lead distance
        Vector3 predicted = pNow + vWorld * totalTime;

        // Limit lead along velocity direction to leadMaxTiles
        Vector3 dir = vWorld.sqrMagnitude > 0.0001f ? vWorld.normalized : Vector3.zero;
        if (dir == Vector3.zero) return pNow;

        // Project predicted relative to current, clamp magnitude
        Vector3 rel = predicted - pNow; rel.y = 0f;
        float maxLead = Mathf.Clamp(leadMaxTiles, 0, maxRangeTiles) * tileSize;
        if (rel.magnitude > maxLead) rel = dir * maxLead;

        Vector3 limited = pNow + rel;

        // Blend with current tile so we don't overcorrect
        Vector3 blended = Vector3.Lerp(pNow, limited, Mathf.Clamp01(leadBlend));

        // Clamp to max throw range from NPC
        Vector3 clamped = ClampWithinRange(npcSnap, blended, maxRangeTiles * tileSize);

        return Snap(clamped);
    }
    // -------------------------------------------------------

    Vector3 ClampWithinRange(Vector3 origin, Vector3 target, float maxDist)
    {
        Vector3 d = target - origin; d.y = 0f;
        float m = d.magnitude;
        if (m <= maxDist) return target;
        return origin + d.normalized * maxDist;
    }

    IEnumerator CastRoutine(Vector3 targetCenter)
    {
        // 1) WINDUP – telegraph
        float gy = SampleGroundY(targetCenter);
        ShowWindupTelegraph(targetCenter, gy, bombRadiusTiles);
        yield return new WaitForSeconds(Mathf.Max(0.05f, windupSeconds));
        ClearWindupMarkers();

        // 2) THROW via independent projectile (won't get stuck if NPC dies)
        {
            Vector3 start = Snap(transform.position) + Vector3.up * 0.5f;
            Vector3 end   = targetCenter + Vector3.up * 0.5f;

            float distTiles = Mathf.Max(1f, Vector3.Distance(start, end) / Mathf.Max(0.0001f, tileSize));
            float duration = bombThrowTimePerTile * distTiles;

            var bomb = Instantiate(bombPrefab, start, Quaternion.identity);
            var proj = bomb.GetComponent<BombProjectileArc>();
            if (!proj) proj = bomb.AddComponent<BombProjectileArc>();

            proj.Init(
                start: start,
                end: end,
                duration: duration,
                arcHeight: bombArcHeight,
                groundMask: groundMask,
                explosionPrefab: explosionVfxPrefab,
                onExplode: (landPos) =>
                {
                    // 3) After landing: spawn lingering field
                    var go = new GameObject("BombAoEFieldHostile");
                    var field = go.AddComponent<BombAoEFieldHostile>();
                    field.Init(
                        center: targetCenter,
                        tileSize: tileSize,
                        markerPrefab: tileMarkerPrefab,
                        markerYOffset: markerYOffset,
                        groundMask: groundMask,
                        victimsLayer: victimLayer,
                        radiusTiles: bombRadiusTiles,
                        duration: lingerDuration,
                        tickInterval: tickInterval,
                        tickDamage: tickDamage,
                        slowPercent: slowPercent,
                        slowTag: slowStatusTag,
                        slowIcon: slowStatusIcon
                    );
                }
            );
        }

        var go = new GameObject("BombAoEFieldHostile");
        var field = go.AddComponent<BombAoEFieldHostile>();
        field.Init(
            center: targetCenter,
            tileSize: tileSize,
            markerPrefab: tileMarkerPrefab,
            markerYOffset: markerYOffset,
            groundMask: groundMask,
            victimsLayer: victimLayer,
            radiusTiles: bombRadiusTiles,
            duration: lingerDuration,
            tickInterval: tickInterval,
            tickDamage: tickDamage,
            slowPercent: slowPercent,
            slowTag: slowStatusTag,
            slowIcon: slowStatusIcon
        );
    }


    void ShowWindupTelegraph(Vector3 center, float groundY, int radiusTiles)
    {
        foreach (var m in windupMarkers) if (m) Destroy(m);
        windupMarkers.Clear();

        foreach (var c in GetDiamondTiles(center, radiusTiles))
        {
            Vector3 pos = c; pos.y = groundY + markerYOffset;
            var m = Instantiate(tileMarkerPrefab, pos, Quaternion.identity);
            TintMarker(m, windupColor);

            if (!m.TryGetComponent<TileMarker>(out var tm)) tm = m.AddComponent<TileMarker>();
            tm.Init(windupSeconds + 5f, tileSize);

            windupMarkers.Add(m);
        }
    }

    void ClearWindupMarkers()
    {
        foreach (var m in windupMarkers) if (m) Destroy(m);
        windupMarkers.Clear();
    }

    // ------- Utils -------
    Vector3 Snap(Vector3 p) =>
        new Vector3(Mathf.Round(p.x / tileSize) * tileSize, p.y, Mathf.Round(p.z / tileSize) * tileSize);

    bool WithinTiles(Vector3 a, Vector3 b, int tiles)
    {
        float dx = Mathf.Abs(a.x - b.x);
        float dz = Mathf.Abs(a.z - b.z);
        float cheb = Mathf.Max(dx, dz) / Mathf.Max(0.01f, tileSize);
        return cheb <= tiles + 0.001f;
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

    float SampleGroundY(Vector3 at)
    {
        Vector3 origin = at + Vector3.up * 10f;
        if (Physics.Raycast(origin, Vector3.down, out var hit, 40f, groundMask, QueryTriggerInteraction.Ignore))
            return hit.point.y;
        return at.y;
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