using UnityEngine;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class DestructibleWallV2 : MonoBehaviour
{
    [Header("Hierarchy")]
    public Transform dynamicBrick;      // required (parent of chunks with Rigidbodies)
    public Transform gluedBrick;        // optional (frame pieces; untouched)

    [Header("Hit Proxy Collider")]
    public BoxCollider hitProxy;        // auto-created & sized if null
    public int hitProxyLayer = 0;       // set to a layer included by your Strike damage mask

    public enum EjectDirection { WallForward, WallBackward, Custom }

    [Header("Primary Eject (first hit = break)")]
    public EjectDirection ejectDirection = EjectDirection.WallForward;
    public Vector3 customEject = new Vector3(0, 0, 1);
    [Tooltip("Main push; VelocityChange (mass-independent).")]
    public float ejectStrength = 9f;
    public float upKick = 3.5f;
    public float lateralJitter = 1.6f;
    public float randomTorque = 28f;

    [Header("Explosion (optional, added on break)")]
    public float explosionForce = 0f;     // Impulse
    public float explosionRadius = 1.8f;

    [Header("Follow-up Hits (after broken, before cleanup)")]
    [Tooltip("Extra velocity per FOLLOW-UP hit applied to all chunks.")]
    public float extraEjectPerHit = 4.0f;           // VelocityChange
    public float extraUpPerHit = 1.2f;              // VelocityChange
    public float extraTorquePerHit = 10f;           // Impulse

    [Header("Follow-up Hits Targeting")]
    [Tooltip("On break, set every dynamic chunk to the same layer as the hit proxy so Strike can find them.")]
    public bool setChunkLayerOnBreak = true;

    [Header("Cleanup (destroy flying bricks)")]
    [Tooltip("How many total hits until we start destroying chunks (1 = clean up right after first hit).")]
    public int hitsToStartCleanup = 3;
    [Tooltip("Seconds after cleanup starts to destroy each chunk (0 = immediate).")]
    public float destroyChunksAfter = 8f;
    [Tooltip("Destroy this root after the first break (0 = keep).")]
    public float destroyRootAfter = 0f;
    public bool unparentChunksOnBreak = true;

    [Header("Anti double-count")]
    [Tooltip("Ignore extra hits for this many seconds immediately after the first break, to prevent the same Strike from counting multiple times.")]
    public float armDelayAfterBreak = 0.05f;

    // runtime
    private readonly List<Rigidbody> rbs = new List<Rigidbody>();
    private bool broken;
    private int hitsTaken;
    private bool cleanupStarted;
    private float nextAcceptTime = 0f;

    void Reset()
    {
        if (!dynamicBrick) dynamicBrick = transform.Find("DynamicBrick");
        if (!gluedBrick)   gluedBrick   = transform.Find("GluedBrick");
    }

    void Awake()
    {
        if (!dynamicBrick)
        {
            Debug.LogError($"{name}: Assign DynamicBrick.", this);
            enabled = false;
            return;
        }

        // cache & “freeze” all dynamic chunk rigidbodies
        rbs.Clear();
        foreach (var rb in dynamicBrick.GetComponentsInChildren<Rigidbody>(true))
        {
            rbs.Add(rb);
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.sleepThreshold = 0f;
        }

        // ensure hit proxy
        if (!hitProxy)
        {
            hitProxy = GetComponent<BoxCollider>();
            if (!hitProxy) hitProxy = gameObject.AddComponent<BoxCollider>();
        }
        RefitHitProxyToDynamic();
        hitProxy.isTrigger = false;   // intact: block & receive damage
        if (hitProxyLayer != 0) gameObject.layer = hitProxyLayer;

        // add damage relay so Strike finds IDamageable on the proxy
        if (!TryGetComponent<DamageRelay>(out var relay))
            relay = gameObject.AddComponent<DamageRelay>();
        relay.wallV2 = this;

        hitsToStartCleanup = Mathf.Max(1, hitsToStartCleanup);
    }

    public void RefitHitProxyToDynamic()
    {
        var rends = dynamicBrick.GetComponentsInChildren<Renderer>(true);
        if (rends.Length == 0)
        {
            hitProxy.center = Vector3.zero;
            hitProxy.size   = Vector3.one;
            return;
        }

        var b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

        Vector3 lc = transform.InverseTransformPoint(b.center);
        Vector3 ws = b.size;
        Vector3 s  = transform.lossyScale;
        Vector3 ls = new Vector3(SafeDiv(ws.x, Mathf.Abs(s.x)), SafeDiv(ws.y, Mathf.Abs(s.y)), SafeDiv(ws.z, Mathf.Abs(s.z)));

        hitProxy.center = lc;
        hitProxy.size   = ls;
        hitProxy.enabled = true;
    }

    static float SafeDiv(float a, float b) => b < 1e-6f ? 0f : a / b;

    // Called by DamageRelay on every Strike that hits
    public void RegisterHit()
    {
        // If we're in the post-break arm delay window, ignore duplicates from the same strike
        if (broken && Time.time < nextAcceptTime) return;

        if (!broken)
        {
            hitsTaken++;          // count the first hit
            BreakNow();           // crumbles immediately
            nextAcceptTime = Time.time + Mathf.Max(0f, armDelayAfterBreak);
            MaybeStartCleanup();  // only starts if hitsToStartCleanup == 1
            return;
        }

        // Already broken and past the arm delay → accept a follow-up hit
        hitsTaken++;
        if (!cleanupStarted)
        {
            BoostChunks(extraEjectPerHit, extraUpPerHit, extraTorquePerHit);
            MaybeStartCleanup();
        }
    }

    private void MaybeStartCleanup()
    {
        if (!cleanupStarted && hitsTaken >= hitsToStartCleanup)
        {
            cleanupStarted = true;

            // begin destruction timers for all chunks
            if (destroyChunksAfter <= 0f)
            {
                foreach (var rb in rbs) if (rb) Destroy(rb.gameObject);
            }
            else
            {
                foreach (var rb in rbs) if (rb) Destroy(rb.gameObject, destroyChunksAfter);
            }

            // disable the proxy completely once cleanup begins
            if (hitProxy) hitProxy.enabled = false;
        }
    }

    private void BreakNow()
    {
        broken = true;

        // We no longer need the proxy; follow-up hits will target chunk colliders
        if (hitProxy) hitProxy.enabled = false;

        Vector3 n = GetEjectDir();
        Vector3 center = GetCenterFromDynamic();

        // Add DamageRelay to all chunk colliders and (optionally) align their layer with the proxy
        foreach (var col in dynamicBrick.GetComponentsInChildren<Collider>(true))
        {
            if (!col.TryGetComponent<DamageRelay>(out var relay))
                relay = col.gameObject.AddComponent<DamageRelay>();
            relay.wallV2 = this;

            if (setChunkLayerOnBreak && hitProxy)
                col.gameObject.layer = gameObject.layer; // same as proxy/root
        }

        foreach (var rb in rbs)
        {
            if (!rb) continue;

            rb.isKinematic = false;
            rb.useGravity = true;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.WakeUp();

            // slight nudge out of the wall surface
            rb.position += n * 0.015f;

            // big, mass-independent shove
            Vector3 velKick = n * Mathf.Max(0f, ejectStrength)
                            + Vector3.up * Mathf.Max(0f, upKick)
                            + new Vector3(Random.Range(-lateralJitter, lateralJitter), 0f, Random.Range(-lateralJitter, lateralJitter));
            if (velKick.sqrMagnitude > 0f)
                rb.AddForce(velKick, ForceMode.VelocityChange);

            if (explosionForce > 0.01f)
                rb.AddExplosionForce(explosionForce, center, Mathf.Max(0.01f, explosionRadius), 0.0f, ForceMode.Impulse);

            if (randomTorque > 0.01f)
                rb.AddTorque(Random.onUnitSphere * randomTorque, ForceMode.Impulse);

            if (unparentChunksOnBreak) rb.transform.SetParent(null, true);
        }

        if (destroyRootAfter > 0.01f) Destroy(gameObject, destroyRootAfter);
    }

    private void BoostChunks(float extraEject, float extraUp, float extraTorque)
    {
        Vector3 n = GetEjectDir();
        foreach (var rb in rbs)
        {
            if (!rb || rb.isKinematic) continue;
            Vector3 addVel = n * Mathf.Max(0f, extraEject) + Vector3.up * Mathf.Max(0f, extraUp);
            if (addVel.sqrMagnitude > 0f) rb.AddForce(addVel, ForceMode.VelocityChange);
            if (extraTorque > 0f) rb.AddTorque(Random.onUnitSphere * extraTorque, ForceMode.Impulse);
        }
    }

    Vector3 GetCenterFromDynamic()
    {
        var r = dynamicBrick.GetComponentInChildren<Renderer>(true);
        return r ? r.bounds.center : transform.position;
    }

    Vector3 GetEjectDir()
    {
        return ejectDirection == EjectDirection.WallForward  ? transform.forward :
               ejectDirection == EjectDirection.WallBackward ? -transform.forward :
               (customEject.sqrMagnitude > 0.0001f ? customEject.normalized : transform.forward);
    }
}
