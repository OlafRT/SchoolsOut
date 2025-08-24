using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// Nerd-only chain lightning that branches 2–3 tiles, with cast bar + move-to-cancel.
[DisallowMultipleComponent]
public class ChainShockAbility : MonoBehaviour, IAbilityUI, IClassRestrictedAbility
{
    [Header("Learned Gate")]
    public string abilityName = "Chain Shock";

    [Header("Input")]
    public KeyCode castKey = KeyCode.T;

    [Header("Core")]
    public float castTime = 1.5f;
    public float cooldownSeconds = 8f;
    public int baseDamage = 18;
    public int maxTargets = 8;
    public int initialSearchRangeTiles = 10;

    [Header("Chain Rules")]
    public int minHopTiles = 1;
    public int maxHopTiles = 3;

    [Header("VFX (multi-LR prefab)")]
    public Sprite icon;
    [Tooltip("Prefab root with LightningLine + two child LineRenderers (Core, Glow).")]
    public GameObject linePrefabGO;
    public float lineLifetime = 0.18f;   // pushed to LightningLine at runtime
    public float lineWidth = 0.06f;      // optional: if you want to override child widths
    [Tooltip("Sawtooth offset per step in tiles (0 = straight).")]
    public float sawtoothOffsetTiles = 0.15f;

    [Header("Animation (optional)")]
    public string castTrigger = "CastStart";
    public string releaseTrigger = "CastRelease";

    [Header("UI")]
    public CastBarUI castBar;

    // ---- Runtime ----
    private PlayerAbilities ctx;
    private AutoAttackAbility autoAttack;
    private Animator anim;
    private bool isCasting;
    private float cdUntil;

    public AbilityClassRestriction AllowedFor => AbilityClassRestriction.Nerd;

    void Awake()
    {
        ctx = GetComponent<PlayerAbilities>();
        autoAttack = GetComponent<AutoAttackAbility>();
        anim = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        if (!IsLearned) return;
        if (Time.time < cdUntil) return;
        if (isCasting) return;

        if (Input.GetKeyDown(castKey))
        {
            var first = FindNearestTarget();
            if (!first) return;
            StartCoroutine(CastRoutine(first));
        }
    }

    IEnumerator CastRoutine(Collider initialTarget)
    {
        isCasting = true;
        if (autoAttack) autoAttack.IsSuppressedByOtherAbilities = true;
        if (anim && !string.IsNullOrEmpty(castTrigger)) anim.SetTrigger(castTrigger);
        if (castBar) castBar.Show(abilityName, castTime);

        Vector3 startTile = ctx.Snap(transform.position);
        float endTime = Time.time + Mathf.Max(0.01f, castTime);
        while (Time.time < endTime)
        {
            float remain = endTime - Time.time;
            float p = 1f - Mathf.Clamp01(remain / castTime);
            if (castBar) castBar.SetProgress(p, remain);

            // cancel if moved to a different tile
            if (ctx.Snap(transform.position) != startTile)
            {
                CancelCast();
                yield break;
            }
            yield return null;
        }

        // revalidate target
        if (!initialTarget || !initialTarget.gameObject.activeInHierarchy)
        {
            initialTarget = FindNearestTarget();
            if (!initialTarget) { FinishCastNoFire(); yield break; }
        }

        if (anim && !string.IsNullOrEmpty(releaseTrigger)) anim.SetTrigger(releaseTrigger);
        FireChain(initialTarget);

        cdUntil = Time.time + Mathf.Max(0.01f, cooldownSeconds);
        FinishCastNoFire();
    }

    void CancelCast()
    {
        if (castBar) castBar.Hide();
        if (autoAttack) autoAttack.IsSuppressedByOtherAbilities = false;
        isCasting = false;
    }

    void FinishCastNoFire()
    {
        if (castBar) castBar.Hide();
        if (autoAttack) autoAttack.IsSuppressedByOtherAbilities = false;
        isCasting = false;
    }

    // ---------- Chain logic ----------

    void FireChain(Collider firstHit)
    {
        var visited = new HashSet<Collider>();
        var frontier = new List<Collider>();
        var next = new List<Collider>();
        var edges = new List<(Collider from, Collider to)>();
        var allHits = new List<Collider>();

        frontier.Add(firstHit);
        visited.Add(firstHit);
        int targetsLeft = Mathf.Max(1, maxTargets);

        allHits.Add(firstHit);
        targetsLeft--;

        while (targetsLeft > 0 && frontier.Count > 0)
        {
            next.Clear();
            foreach (var node in frontier)
            {
                var neighbors = FindHopNeighbors(node, minHopTiles, maxHopTiles, visited);
                foreach (var n in neighbors)
                {
                    edges.Add((node, n));
                    allHits.Add(n);
                    visited.Add(n);
                    next.Add(n);
                    targetsLeft--;
                    if (targetsLeft <= 0) break;
                }
                if (targetsLeft <= 0) break;
            }
            frontier.Clear();
            frontier.AddRange(next);
        }

        DrawAllEdges(edges);

        foreach (var c in allHits)
            DamageColliderOnce(c);
    }

    Collider FindNearestTarget()
    {
        float r = initialSearchRangeTiles * ctx.tileSize;
        var hits = Physics.OverlapSphere(transform.position, r, ctx.targetLayer, QueryTriggerInteraction.Ignore);

        Collider best = null;
        float bestTiles = float.MaxValue;
        Vector3 myTile = ctx.Snap(transform.position);

        foreach (var h in hits)
        {
            if (!h) continue;
            Vector3 ht = ctx.Snap(h.bounds.center);
            float dTiles = ChebyshevTiles(myTile, ht);
            if (dTiles < bestTiles)
            {
                bestTiles = dTiles;
                best = h;
            }
        }
        return best;
    }

    List<Collider> FindHopNeighbors(Collider from, int minRing, int maxRing, HashSet<Collider> exclude)
    {
        var results = new List<Collider>();
        Vector3 centerTile = ctx.Snap(from.bounds.center);
        float maxR = (maxRing + 0.5f) * ctx.tileSize;

        var hits = Physics.OverlapSphere(centerTile, maxR + 0.1f, ctx.targetLayer, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            if (!h || exclude.Contains(h)) continue;
            Vector3 ht = ctx.Snap(h.bounds.center);
            float d = ChebyshevTiles(centerTile, ht);
            if (d >= minRing && d <= maxRing)
                results.Add(h);
        }
        return results;
    }

    float ChebyshevTiles(Vector3 a, Vector3 b)
    {
        return Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.z - b.z)) / Mathf.Max(0.0001f, ctx.tileSize);
    }

    // ---------- Damage & VFX ----------

    void DamageColliderOnce(Collider c)
    {
        if (!c) return;
        bool didCrit;
        int final = ctx.stats ? ctx.stats.ComputeDamage(baseDamage, PlayerStats.AbilitySchool.Nerd, true, out didCrit)
                              : baseDamage;

        Vector3 tileCenter = ctx.Snap(c.bounds.center);
        ctx.DamageTileScaled(tileCenter, ctx.tileSize * 0.45f, final, PlayerStats.AbilitySchool.Nerd, false);
    }

    void DrawAllEdges(List<(Collider from, Collider to)> edges)
    {
        if (edges.Count == 0 || !linePrefabGO) return;

        foreach (var e in edges)
        {
            Vector3 a = ctx.Snap(e.from.bounds.center);
            Vector3 b = ctx.Snap(e.to.bounds.center);

            // Build a tile path and add "sawtooth" midpoints
            var points = BuildTilePathSawtooth(a, b);

            var fx = Instantiate(linePrefabGO);
            // optional: push lifetime into LightningLine
            var ll = fx.GetComponent<LightningLine>();
            if (ll) ll.lifetime = lineLifetime;

            // set positions on ALL child line renderers
            var lrs = fx.GetComponentsInChildren<LineRenderer>(true);
            foreach (var lr in lrs)
            {
                lr.positionCount = points.Count;
                lr.SetPositions(points.ToArray());
                // If you want to override widths globally:
                // var curve = lr.widthCurve;
                // for a quick override, just set start/end widths (if you used constant width):
                // lr.startWidth = lr.endWidth = lineWidth;
            }
        }
    }

    // Tile path with optional sawtooth midpoints
    List<Vector3> BuildTilePathSawtooth(Vector3 from, Vector3 to)
    {
        var path = new List<Vector3>();
        Vector3 cur = from;
        float y = from.y;
        path.Add(new Vector3(cur.x, y, cur.z));

        int guard = 256;
        int stepIndex = 0;
        float jitter = sawtoothOffsetTiles * ctx.tileSize;

        while (guard-- > 0)
        {
            if (ChebyshevTiles(cur, to) < 0.1f) break;

            int dx = Mathf.Clamp(Mathf.RoundToInt((to.x - cur.x) / ctx.tileSize), -1, 1);
            int dz = Mathf.Clamp(Mathf.RoundToInt((to.z - cur.z) / ctx.tileSize), -1, 1);
            Vector3 next = cur + new Vector3(dx * ctx.tileSize, 0f, dz * ctx.tileSize);

            if (jitter > 0f)
            {
                // add a midpoint offset perpendicular to the segment
                Vector3 mid = (cur + next) * 0.5f;
                Vector3 seg = (next - cur).normalized;
                Vector3 perp = new Vector3(-seg.z, 0f, seg.x); // 90° on XZ
                float sign = (stepIndex % 2 == 0) ? 1f : -1f;  // alternate
                Vector3 midOffset = mid + perp * (jitter * sign);

                path.Add(new Vector3(midOffset.x, y, midOffset.z));
            }

            path.Add(new Vector3(next.x, y, next.z));
            cur = next;
            stepIndex++;
        }

        return path;
    }

    // --------- IAbilityUI ---------
    public string AbilityName => abilityName;
    public Sprite Icon => icon;
    public KeyCode Key => castKey;
    public float CooldownRemaining => Mathf.Max(0f, cdUntil - Time.time);
    public float CooldownDuration => Mathf.Max(0.01f, cooldownSeconds);
    public bool IsLearned => ctx && ctx.HasAbility(abilityName);
}
