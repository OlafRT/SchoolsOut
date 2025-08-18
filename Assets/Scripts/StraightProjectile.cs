using UnityEngine;

public class StraightProjectile : MonoBehaviour
{
    private Vector3 dir;
    private float speed;
    private float maxDist;
    private float traveled;
    private LayerMask targetLayer;
    private int damage;
    private float tileSize;

    public void Init(Vector3 direction, float speed, float maxDistance, LayerMask targetLayer, int damage, float tileSize)
    {
        this.dir = direction.normalized;
        this.speed = speed;
        this.maxDist = maxDistance;
        this.targetLayer = targetLayer;
        this.damage = damage;
        this.tileSize = tileSize;
    }

    void Update()
    {
        float step = speed * Time.deltaTime;
        transform.position += dir * step;
        traveled += step;

        // Per-tile hit check (small sphere)
        var hits = Physics.OverlapSphere(transform.position + Vector3.up * 0.5f, tileSize * 0.3f, targetLayer, QueryTriggerInteraction.Ignore);
        if (hits.Length > 0)
        {
            foreach (var h in hits)
            {
                var d = h.GetComponent<IDamageable>();
                if (d != null) d.ApplyDamage(damage);
            }
            Destroy(gameObject);
            return;
        }

        if (traveled >= maxDist)
            Destroy(gameObject);
    }
}
