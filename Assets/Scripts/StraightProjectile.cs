using UnityEngine;

[DisallowMultipleComponent]
public class StraightProjectile : MonoBehaviour
{
    Vector3 dir;
    float speed;
    float maxDistance;
    LayerMask targetLayer;
    int damage;
    float tileSize;
    bool wasCrit;

    Vector3 startPos;
    float traveled;

    public void Init(
        Vector3 direction,
        float speed,
        float maxDistance,
        LayerMask targetLayer,
        int damage,
        float tileSize,
        bool wasCrit = false)
    {
        this.dir = direction.normalized;
        this.speed = Mathf.Max(0.01f, speed);
        this.maxDistance = Mathf.Max(0.01f, maxDistance);
        this.targetLayer = targetLayer;
        this.damage = Mathf.Max(0, damage);
        this.tileSize = Mathf.Max(0.01f, tileSize);
        this.wasCrit = wasCrit;

        startPos = transform.position;
        traveled = 0f;
    }

    void Update()
    {
        float step = speed * Time.deltaTime;
        if (step <= 0f) return;

        // Move
        Vector3 next = transform.position + dir * step;

        // Check hit along the step
        float radius = tileSize * 0.3f;
        if (Physics.SphereCast(transform.position, radius, dir, out var hit, step, targetLayer, QueryTriggerInteraction.Ignore))
        {
            OnHit(hit.collider);
            // place the projectile at the hit point before killing it (optional)
            transform.position = hit.point - dir * 0.05f;
            Destroy(gameObject);
            return;
        }

        transform.position = next;
        traveled = Vector3.Distance(startPos, transform.position);
        if (traveled >= maxDistance)
        {
            Destroy(gameObject);
        }
    }

    void OnHit(Collider col)
    {
        // Show number
        if (CombatTextManager.Instance)
        {
            Vector3 pos = col.bounds.center;
            pos.y = col.bounds.max.y; // head-ish
            CombatTextManager.Instance.ShowDamage(pos, damage, wasCrit, col.transform);
        }

        // Apply damage
        if (col.TryGetComponent<IDamageable>(out var dmg))
        {
            dmg.ApplyDamage(damage);
        }
    }
}