using UnityEngine;
using System;
using System.Collections;

[DisallowMultipleComponent]
public class BombProjectileArc : MonoBehaviour
{
    [Header("Motion")]
    public float duration = 0.5f;
    public float arcHeight = 1.5f;

    [Header("Grounding")]
    public LayerMask groundMask = ~0;   // Ground layers
    public float groundRayUp = 10f;     // how high above to start ground sample
    public float groundRayDown = 40f;   // how far down to raycast

    [Header("Safety")]
    public float maxLifetime = 5f;      // hard fail-safe

    // Explosion handling
    private GameObject explosionPrefab;
    private Action<Vector3> onExplode;  // optional callback

    // Internal
    private Vector3 start;
    private Vector3 end;

    public void Init(Vector3 start, Vector3 end, float duration, float arcHeight, LayerMask groundMask, GameObject explosionPrefab, Action<Vector3> onExplode = null)
    {
        this.start = start;
        this.end = end;
        this.duration = Mathf.Max(0.05f, duration);
        this.arcHeight = arcHeight;
        this.groundMask = groundMask;
        this.explosionPrefab = explosionPrefab;
        this.onExplode = onExplode;

        // Begin independent motion (not tied to NPC lifecycle)
        StartCoroutine(ArcRoutine());
    }

    IEnumerator ArcRoutine()
    {
        float dieAt = Time.time + maxLifetime;
        float t = 0f;

        // Precompute arc scale (slightly taller for longer throws)
        Vector3 flatStart = new Vector3(start.x, 0f, start.z);
        Vector3 flatEnd   = new Vector3(end.x,   0f, end.z);
        float flatDist = Vector3.Distance(flatStart, flatEnd);
        float arc = arcHeight + 0.15f * flatDist;

        while (t < 1f && Time.time < dieAt)
        {
            t += Time.deltaTime / duration;
            float u = Mathf.Clamp01(t);

            Vector3 pos = Vector3.Lerp(start, end, u);
            pos.y = Mathf.Lerp(start.y, end.y, u) + (-4f * arc * (u * u - u));
            transform.position = pos;

            yield return null;
        }

        // Snap to ground (robustly) before exploding
        Vector3 land = end;
        Vector3 rayOrigin = land + Vector3.up * groundRayUp;
        if (Physics.Raycast(rayOrigin, Vector3.down, out var hit, groundRayDown + groundRayUp, groundMask, QueryTriggerInteraction.Ignore))
            land.y = hit.point.y;
        else
            land.y = end.y; // fallback

        transform.position = land;

        // spawn explosion
        if (explosionPrefab) Instantiate(explosionPrefab, land, Quaternion.identity);
        onExplode?.Invoke(land);

        Destroy(gameObject);
    }

    // Extra safety: if something disables/destroys us prematurely, still cleanup soon
    void OnDisable()
    {
        // If the GameObject is being destroyed, no action needed.
        // If it gets disabled by pooling or something odd, ensure it doesn’t linger.
        if (gameObject && !Application.isPlaying) return;
    }
}
