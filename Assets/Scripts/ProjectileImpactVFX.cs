using UnityEngine;

[DisallowMultipleComponent]
public class ProjectileImpactVFX : MonoBehaviour
{
    [Header("VFX")]
    [SerializeField] private GameObject impactPrefab;   // ParticleSystem prefab (loop off)
    [SerializeField] private float prefabScale = 1f;
    [SerializeField] private float destroyAfterSeconds = 2f;
    [SerializeField] private bool parentToHit = false;

    [Header("SFX")]
    [SerializeField] private AudioClip impactSfx;
    [SerializeField] private float sfxVolume = 1f;

    [Header("Physics Contacts")]
    [Tooltip("Layers that will spawn VFX when a physics contact occurs (trigger/collision).")]
    [SerializeField] private LayerMask spawnOnLayers = ~0; // default: everything

    [Header("Sweep Raycast (for fast or raycast-only projectiles)")]
    [SerializeField] private bool useSweepRaycast = true;
    [SerializeField] private LayerMask sweepLayers = ~0;
    [SerializeField] private float sweepRadius = 0.06f; // spherecast radius
    [SerializeField] private bool destroyProjectileOnHit = false;

    private bool fired;
    private Vector3 prevPos;
    private Collider[] ownColliders;

    void Awake()
    {
        prevPos = transform.position;
        ownColliders = GetComponentsInChildren<Collider>(true);
    }

    void LateUpdate()
    {
        if (!useSweepRaycast || fired) { prevPos = transform.position; return; }

        Vector3 cur = transform.position;
        Vector3 delta = cur - prevPos;
        float dist = delta.magnitude;
        if (dist <= Mathf.Epsilon) { prevPos = cur; return; }

        Vector3 dir = delta / dist;
        // SphereCast to be robust against thin obstacles / high speed
        if (Physics.SphereCast(prevPos, sweepRadius, dir, out RaycastHit hit, dist, sweepLayers, QueryTriggerInteraction.Collide))
        {
            if (IsOwnCollider(hit.collider)) { prevPos = cur; return; } // ignore self
            SpawnImpact(hit.point, hit.normal, hit.transform);
        }

        prevPos = cur;
    }

    // ------------- Public setup from code -------------
    public void Configure(GameObject vfxPrefab, AudioClip sfx, float scale, float life, bool parent, LayerMask layers)
    {
        impactPrefab = vfxPrefab;
        impactSfx = sfx;
        prefabScale = scale;
        destroyAfterSeconds = life;
        parentToHit = parent;
        spawnOnLayers = layers;
    }

    public void ConfigureSweep(bool enable, LayerMask layers, float radius, bool destroyProjectileOnHit)
    {
        useSweepRaycast = enable;
        sweepLayers = layers;
        sweepRadius = Mathf.Max(0.0f, radius);
        this.destroyProjectileOnHit = destroyProjectileOnHit;
    }

    // ------------- NEW: called by StraightProjectile to guarantee FX at the authoritative hit -------------
    public void ForceImpactAt(Vector3 pos, Transform hit)
    {
        if (fired) return;
        Vector3 normal = (-transform.forward).normalized; // best-effort if no surface normal provided
        SpawnImpact(pos, normal, hit);
    }

    // ------------- Contacts -------------
    void OnTriggerEnter(Collider other)
    {
        if (fired) return;
        if (((1 << other.gameObject.layer) & spawnOnLayers) == 0) return;
        if (IsOwnCollider(other)) return;

        Vector3 p = other.ClosestPoint(transform.position);
        Vector3 n = (p - prevPos).sqrMagnitude > 0.0001f ? (p - prevPos).normalized : -transform.forward;
        SpawnImpact(p, n, other.transform);
    }

    void OnCollisionEnter(Collision col)
    {
        if (fired) return;
        if (((1 << col.gameObject.layer) & spawnOnLayers) == 0) return;
        if (IsOwnCollider(col.collider)) return;

        ContactPoint cp = col.contacts.Length > 0 ? col.contacts[0] : default;
        Vector3 p = cp.point != Vector3.zero ? cp.point : transform.position;
        Vector3 n = cp.normal != Vector3.zero ? cp.normal : -transform.forward;
        SpawnImpact(p, n, col.transform);
    }

    // ------------- Core -------------
    void SpawnImpact(Vector3 pos, Vector3 normal, Transform hit)
    {
        if (fired) return;
        fired = true;

        if (impactPrefab)
        {
            var rot = Quaternion.LookRotation(normal, Vector3.up);
            var parent = parentToHit ? hit : null;
            var fx = Instantiate(impactPrefab, pos, rot, parent);
            fx.transform.localScale *= prefabScale;
            Destroy(fx, destroyAfterSeconds);
        }

        if (impactSfx)
        {
            var a = new GameObject("ImpactSFX").AddComponent<AudioSource>();
            a.transform.position = pos;
            a.spatialBlend = 1f;
            a.volume = sfxVolume;
            a.clip = impactSfx;
            a.Play();
            Destroy(a.gameObject, impactSfx.length + 0.1f);
        }

        if (destroyProjectileOnHit)
            Destroy(gameObject);
        else
            enabled = false; // prevent any late double-fires
    }

    bool IsOwnCollider(Collider c)
    {
        if (c == null || ownColliders == null) return false;
        for (int i = 0; i < ownColliders.Length; i++)
            if (ownColliders[i] == c) return true;
        return false;
    }
}
