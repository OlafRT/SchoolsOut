using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class StraightProjectile : MonoBehaviour
{
    Vector3 dir;
    float speed;
    float maxDistance;

    LayerMask hitLayers;      // walls | ground | targets
    LayerMask damageLayers;   // targets only

    int damage;
    float tileSize;
    bool wasCrit;

    Vector3 startPos;
    float traveled;

    Rigidbody rb;

    public void Init(
        Vector3 direction,
        float speed,
        float maxDistance,
        LayerMask hitLayers,      // combined layers for impact detection
        LayerMask damageLayers,   // layers that can receive damage/text
        int damage,
        float tileSize,
        bool wasCrit = false)
    {
        this.dir          = direction.normalized;
        this.speed        = Mathf.Max(0.01f, speed);
        this.maxDistance  = Mathf.Max(0.01f, maxDistance);
        this.hitLayers    = hitLayers;
        this.damageLayers = damageLayers;
        this.damage       = Mathf.Max(0, damage);
        this.tileSize     = Mathf.Max(0.01f, tileSize);
        this.wasCrit      = wasCrit;

        startPos = transform.position;
        traveled = 0f;

        if (!rb) rb = GetComponent<Rigidbody>();
        // Safety: make sure RB is set for kinematic MovePosition flow
        rb.isKinematic = true;
        rb.detectCollisions = true;
        // Keep interpolation ON — it smooths MovePosition between physics ticks
        if (rb.interpolation == RigidbodyInterpolation.None)
            rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    // Move & collide from FixedUpdate so interpolation can do its job.
    void FixedUpdate()
    {
        if (speed <= 0f) return;

        float step = speed * Time.fixedDeltaTime;
        if (step <= 0f) return;

        Vector3 from = rb.position;
        float radius = Mathf.Max(0.01f, tileSize * 0.30f);

        // Sweep for any hit along this physics step (include triggers for moving targets)
        if (Physics.SphereCast(from, radius, dir, out var hit, step, hitLayers, QueryTriggerInteraction.Collide))
        {
            ResolveHit(hit);
            return;
        }

        // No hit this tick—advance using RB so interpolation smooths visually
        Vector3 to = from + dir * step;
        rb.MovePosition(to);

        traveled += step;
        if (traveled >= maxDistance)
        {
            Destroy(gameObject);
        }
    }

    void ResolveHit(in RaycastHit hit)
    {
        // 1) Spawn impact VFX/SFX right at the authoritatively detected point
        var vfx = GetComponent<ProjectileImpactVFX>();
        if (vfx) vfx.ForceImpactAt(hit.point, hit.transform); // one-shot inside component

        // 2) Apply damage (+ numbers) only if target is damageable and on damageLayers
        Collider col = hit.collider;
        bool onDamageLayer = ((damageLayers.value & (1 << col.gameObject.layer)) != 0);

        if (onDamageLayer && col.TryGetComponent<IDamageable>(out var dmgable))
        {
            dmgable.ApplyDamage(damage);

            // Show damage number (at head-ish or exact hit point)
            if (CombatTextManager.Instance)
            {
                Vector3 pos = col.bounds.max; // top of the capsule/renderer bounds
                CombatTextManager.Instance.ShowDamage(pos, damage, wasCrit, col.transform);
            }
        }

        // 3) Snap to the hit point (slightly inset) and destroy
        Vector3 place = hit.point - dir * 0.05f;
        rb.MovePosition(place);
        Destroy(gameObject);
    }
}
