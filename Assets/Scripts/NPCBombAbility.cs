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

    // Refs
    NPCAI ai;
    NPCMovement mover;
    Transform player;

    float nextReadyTime = 0f;
    readonly List<GameObject> windupMarkers = new();

    // ---- Leading cache ----
    Vector3 lastSnap;
    Vector3 prevSnap;
    float lastSnapTime;

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
        prevSnap = lastSnap = Snap(transform.position); // init
        lastSnapTime = Time.time;
    }

    void Update()
    {
        // Update player snap history for velocity
        if (player)
        {
            Vector3 snapNow = Snap(player.position);
            if ((snapNow - lastSnap).sqrMagnitude > 0.0001f)
            {
                prevSnap = lastSnap;
                lastSnap = snapNow;
                lastSnapTime = Time.time;
            }
        }

        if (!CanConsiderCasting()) return;

        if (Time.time >= nextReadyTime && ai.CurrentHostility == NPCAI.Hostility.Hostile && player)
        {
            Vector3 self = transform.position;
            Vector3 pt = player.position;
            if (!WithinTiles(self, pt, maxRangeTiles)) return;

            // Predict landing tile
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

    Vector3 PredictTargetCenter()
    {
        if (!player) return Snap(transform.position);

        // Estimate player velocity from last two snapped positions
        Vector3 pNow = Snap(player.position);
        Vector3 pPrev = prevSnap;
        float dt = Mathf.Max(0.0001f, Time.time - lastSnapTime);
        Vector3 vel = (pNow - pPrev) / dt; // world units/sec

        // Estimate bomb travel time = windup + flight time
        float dist = Vector3.Distance(Snap(transform.position), pNow);
        float tiles = Mathf.Max(1f, dist / Mathf.Max(0.0001f, tileSize));
        float flight = tiles * bombThrowTimePerTile;
        float leadTime = Mathf.Max(0f, windupSeconds + flight * 0.8f); // slight bias

        Vector3 predicted = pNow + vel * leadTime;
        Vector3 clamped = ClampWithinRange(Snap(transform.position), predicted, maxRangeTiles * tileSize);
        return Snap(clamped);
    }

    Vector3 ClampWithinRange(Vector3 origin, Vector3 target, float maxDist)
    {
        Vector3 d = target - origin; d.y = 0f;
        float m = d.magnitude;
        if (m <= maxDist) return target;
        return origin + d.normalized * maxDist;
    }

    IEnumerator CastRoutine(Vector3 targetCenter)
    {
        // 1) WINDUP – show telegraph in red
        float gy = SampleGroundY(targetCenter);
        ShowWindupTelegraph(targetCenter, gy, bombRadiusTiles);
        yield return new WaitForSeconds(Mathf.Max(0.05f, windupSeconds));
        ClearWindupMarkers();

        // 2) THROW
        yield return StartCoroutine(ThrowBombArc(targetCenter));

        // 3) VFX + Field
        if (explosionVfxPrefab) Instantiate(explosionVfxPrefab, targetCenter + Vector3.up * 0.02f, Quaternion.identity);

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

    IEnumerator ThrowBombArc(Vector3 targetCenter)
    {
        Vector3 start = Snap(transform.position) + Vector3.up * 0.5f;
        Vector3 end = targetCenter + Vector3.up * 0.5f;

        float distTiles = Mathf.Max(1f, Vector3.Distance(start, end) / Mathf.Max(0.0001f, tileSize));
        float duration = bombThrowTimePerTile * distTiles;

        var bomb = Instantiate(bombPrefab, start, Quaternion.identity);

        float t = 0f;
        Vector3 flatStart = new Vector3(start.x, 0f, start.z);
        Vector3 flatEnd = new Vector3(end.x, 0f, end.z);
        float flatDist = Vector3.Distance(flatStart, flatEnd);
        float arc = bombArcHeight + 0.15f * flatDist;

        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.0001f, duration);
            float u = Mathf.Clamp01(t);
            Vector3 pos = Vector3.Lerp(start, end, u);
            pos.y = Mathf.Lerp(start.y, end.y, u) + (-4f * arc * (u * u - u));
            bomb.transform.position = pos;
            yield return null;
        }

        Destroy(bomb);
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
