using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// Nerd-only chain lightning that branches to targets 2–3 tiles away.
[DisallowMultipleComponent]
public class ChainShockAbility : MonoBehaviour, IAbilityUI, IClassRestrictedAbility
{
    [Header("Learned Gate")]
    public string abilityName = "Chain Shock";

    [Header("Input")]
    public KeyCode castKey = KeyCode.T;

    [Header("Core")]
    [Tooltip("Seconds to cast before it fires.")]
    public float castTime = 1.5f;
    [Tooltip("Cooldown after a successful cast.")]
    public float cooldownSeconds = 8f;
    [Tooltip("Base damage per target before stats/crit.")]
    public int baseDamage = 18;
    [Tooltip("Max number of targets the chain can hit in total.")]
    public int maxTargets = 8;
    [Tooltip("Initial search range from the player in tiles.")]
    public int initialSearchRangeTiles = 10;

    [Header("Chain Rules")]
    [Tooltip("Minimum tile distance for a hop (rings outside the hit target).")]
    public int minHopTiles = 2;
    [Tooltip("Maximum tile distance for a hop.")]
    public int maxHopTiles = 3;

    [Header("VFX (optional)")]
    public Sprite icon;
    public LineRenderer linePrefab;
    public float segmentLife = 0.18f;
    public float lineWidth = 0.06f;

    [Header("Animation (optional)")]
    public string castTrigger = "CastStart";
    public string releaseTrigger = "CastRelease";

    [Header("UI")]
    [Tooltip("Reference to your cast bar UI.")]
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
            if (first == null) return;

            StartCoroutine(CastRoutine(first));
        }
    }

    IEnumerator CastRoutine(Collider initialTarget)
    {
        isCasting = true;
        if (autoAttack) autoAttack.IsSuppressedByOtherAbilities = true;
        if (anim && !string.IsNullOrEmpty(castTrigger)) anim.SetTrigger(castTrigger);

        // Cast bar on
        if (castBar) castBar.Show(abilityName, castTime);

        // Movement-cancel: remember the start tile
        Vector3 startTile = ctx.Snap(transform.position);

        float endTime = Time.time + Mathf.Max(0.01f, castTime);
        while (Time.time < endTime)
        {
            // update bar
            float remaining = endTime - Time.time;
            float progressed = 1f - Mathf.Clamp01(remaining / castTime);
            if (castBar) castBar.SetProgress(progressed, remaining);

            // cancel if moved to a different tile
            if (ctx.Snap(transform.position) != startTile)
            {
                CancelCast();
                yield break;
            }

            yield return null;
        }

        // Re-validate target
        if (!initialTarget || !initialTarget.gameObject.activeInHierarchy)
        {
            initialTarget = FindNearestTarget();
            if (!initialTarget) { FinishCastNoFire(); yield break; }
        }

        if (anim && !string.IsNullOrEmpty(releaseTrigger)) anim.SetTrigger(releaseTrigger);
        FireChain(initialTarget);

        // success → cooldown
        cdUntil = Time.time + Mathf.Max(0.01f, cooldownSeconds);
        FinishCastNoFire(); // re-enables AA, hides bar
    }

    void CancelCast()
    {
        // hide bar + re-enable AA
        if (castBar) castBar.Hide();
        if (autoAttack) autoAttack.IsSuppressedByOtherAbilities = false;
        isCasting = false;
        // (optional: play cancel anim/sfx)
    }

    void FinishCastNoFire()
    {
        if (castBar) castBar.Hide();
        if (autoAttack) autoAttack.IsSuppressedByOtherAbilities = false;
        isCasting = false;
    }

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

    // --------- Targeting ---------

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

    // --------- Damage & VFX ---------

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
        if (edges.Count == 0 || (!linePrefab && segmentLife <= 0f)) return;

        foreach (var e in edges)
        {
            Vector3 a = ctx.Snap(e.from.bounds.center);
            Vector3 b = ctx.Snap(e.to.bounds.center);

            var pts = BuildTilePath(a, b);
            if (linePrefab)
            {
                var lr = Instantiate(linePrefab);
                lr.positionCount = pts.Count;
                lr.startWidth = lr.endWidth = lineWidth;
                lr.SetPositions(pts.ToArray());
                Destroy(lr.gameObject, segmentLife);
            }
        }
    }

    List<Vector3> BuildTilePath(Vector3 from, Vector3 to)
    {
        var path = new List<Vector3>();
        Vector3 cur = from;
        path.Add(new Vector3(cur.x, from.y, cur.z));

        int guard = 256;
        while (guard-- > 0)
        {
            if (ChebyshevTiles(cur, to) < 0.1f) break;
            int dx = Mathf.Clamp(Mathf.RoundToInt((to.x - cur.x) / ctx.tileSize), -1, 1);
            int dz = Mathf.Clamp(Mathf.RoundToInt((to.z - cur.z) / ctx.tileSize), -1, 1);
            cur += new Vector3(dx * ctx.tileSize, 0f, dz * ctx.tileSize);
            path.Add(new Vector3(cur.x, from.y, cur.z));
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
