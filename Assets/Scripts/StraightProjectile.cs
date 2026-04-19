using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class StraightProjectile : MonoBehaviour
{
    Vector3 dir;
    float   speed;
    float   maxDistance;

    LayerMask hitLayers;    // walls | ground | targets  (for swept cast)
    LayerMask damageLayers; // targets only

    int   damage;
    float tileSize;
    bool  wasCrit;

    PlayerAbilities shooter;

    float traveled;

    // Previous rb.position — lets the swept cast cover the full gap since the
    // last tick, including any NPC that moved INTO the path between ticks.
    Vector3 _prevPhysPos;
    bool    _prevPhysPosSet;

    // Shared guard: set by whichever detection path fires first so the other
    // one can't double-apply damage or double-play VFX.
    bool _resolved;

    Rigidbody rb;

    // ── Setup ─────────────────────────────────────────────────────────────────
    public void Init(
        Vector3         direction,
        float           speed,
        float           maxDistance,
        LayerMask       hitLayers,
        LayerMask       damageLayers,
        int             damage,
        float           tileSize,
        bool            wasCrit  = false,
        PlayerAbilities shooter  = null)
    {
        this.dir          = direction.normalized;
        this.speed        = Mathf.Max(0.01f, speed);
        this.maxDistance  = Mathf.Max(0.01f, maxDistance);
        this.hitLayers    = hitLayers;
        this.damageLayers = damageLayers;
        this.damage       = Mathf.Max(0, damage);
        this.tileSize     = Mathf.Max(0.01f, tileSize);
        this.wasCrit      = wasCrit;
        this.shooter      = shooter;

        traveled        = 0f;
        _resolved       = false;
        _prevPhysPos    = transform.position;
        _prevPhysPosSet = true;

        if (!rb) rb = GetComponent<Rigidbody>();
        rb.isKinematic      = true;
        rb.detectCollisions = true;
        if (rb.interpolation == RigidbodyInterpolation.None)
            rb.interpolation = RigidbodyInterpolation.Interpolate;

        // Secondary path: if ProjectileImpactVFX detects a hit before FixedUpdate
        // does (rare but possible), let it trigger damage via callback.
        var vfx = GetComponent<ProjectileImpactVFX>();
        if (vfx) vfx.OnHitDetected += OnVFXHitDetected;
    }

    void Awake() { rb = GetComponent<Rigidbody>(); }

    void OnDestroy()
    {
        var vfx = GetComponent<ProjectileImpactVFX>();
        if (vfx) vfx.OnHitDetected -= OnVFXHitDetected;
    }

    // ── Primary detection + movement (FixedUpdate) ────────────────────────────
    //
    // Two checks every tick make tunneling essentially impossible:
    //
    //   1. SphereCastAll — sweeps the FULL path from the PREVIOUS physics
    //      position to the NEXT physics position with a generous radius.
    //      Covers the entire travel volume this tick, including the gap from
    //      the previous tick, so a fast-moving NPC can't slip through.
    //
    //   2. OverlapSphere at the destination — catches enemies that moved
    //      LATERALLY into the arrival point between ticks (not in the cast
    //      direction, so the sweep alone would miss them).
    //
    // Radius is half a tile (~0.45). Large enough to be reliable, small enough
    // that it won't hit enemies clearly out of the flight path.
    void FixedUpdate()
    {
        if (_resolved || speed <= 0f) return;

        Vector3 curPos  = rb.position;
        Vector3 nextPos = curPos + dir * (speed * Time.fixedDeltaTime);
        float   radius  = Mathf.Max(0.35f, tileSize * 0.45f);

        // ── Check 1: SphereCastAll along the full swept path ──────────────────
        Vector3 castOrigin = _prevPhysPosSet ? _prevPhysPos : curPos;
        Vector3 castDelta  = nextPos - castOrigin;
        float   castDist   = castDelta.magnitude;

        if (castDist > 0.001f)
        {
            var hits = Physics.SphereCastAll(
                castOrigin, radius, castDelta.normalized, castDist,
                hitLayers, QueryTriggerInteraction.Collide);

            // Sort nearest-first so we always hit the closest target
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var h in hits)
            {
                if (h.collider.gameObject == gameObject) continue;
                if (ApplyHit(h.collider, h.point)) return;
            }
        }

        // ── Check 2: OverlapSphere at destination (lateral movers) ────────────
        var overlaps = Physics.OverlapSphere(
            nextPos, radius, hitLayers, QueryTriggerInteraction.Collide);

        foreach (var col in overlaps)
        {
            if (col.gameObject == gameObject) continue;
            if (ApplyHit(col, col.ClosestPoint(nextPos))) return;
        }

        // ── No hit — advance ──────────────────────────────────────────────────
        _prevPhysPos    = curPos;
        _prevPhysPosSet = true;

        rb.MovePosition(nextPos);
        traveled += speed * Time.fixedDeltaTime;
        if (traveled >= maxDistance)
            Destroy(gameObject);
    }

    // ── Secondary path: ProjectileImpactVFX detected a hit first ─────────────
    void OnVFXHitDetected(Collider col, Vector3 hitPoint)
    {
        ApplyHit(col, hitPoint);
    }

    // ── Shared hit handler ────────────────────────────────────────────────────
    // Returns true if the hit was consumed (on damageLayers), false if it should
    // be skipped (wall / ground — VFX still plays but no damage, no destroy).
    bool ApplyHit(Collider col, Vector3 hitPoint)
    {
        if (_resolved) return false;

        bool onDamageLayer = ((damageLayers.value & (1 << col.gameObject.layer)) != 0);
        if (!onDamageLayer) return false; // wall/floor — let VFX handle visuals, keep flying

        if (!col.TryGetComponent<IDamageable>(out var dmgable)) return false;

        _resolved = true;

        Vector3 textPos = col.bounds.center;
        textPos.y = col.bounds.max.y;

        bool isMiss = shooter != null
                   && shooter.missChance > 0f
                   && Random.value < shooter.missChance;

        var vfx = GetComponent<ProjectileImpactVFX>();

        if (isMiss)
        {
            if (vfx) vfx.suppressImpact = true;

            if (CombatTextManager.Instance && shooter != null)
                CombatTextManager.Instance.ShowText(
                    textPos, "MISS",
                    shooter.missTextColor, col.transform,
                    -1f, shooter.missFontSize);
        }
        else
        {
            dmgable.ApplyDamage(damage);

            if (CombatTextManager.Instance)
                CombatTextManager.Instance.ShowDamage(textPos, damage, wasCrit, col.transform);
        }

        // Trigger VFX at the hit point (suppressed above on a miss).
        // ForceImpactAt sets fired=true on the VFX component so LateUpdate
        // won't fire a second time on the same frame.
        if (!isMiss && vfx)
            vfx.ForceImpactAt(hitPoint, col.transform);

        Destroy(gameObject);
        return true;
    }
}
