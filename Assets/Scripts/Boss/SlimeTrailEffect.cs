using UnityEngine;

/// <summary>
/// Attach to a slime minion NPC. Periodically drops SlimePuddle instances at the
/// NPC's current position, creating a damaging/slowing trail.
///
/// SETUP:
///   1. Attach to the slime minion prefab (or the same GO as NPCAI/NPCHealth).
///   2. Assign slimePuddlePrefab — a prefab with a flat trigger Collider + visual.
///   3. SlimePuddle will be added/Init'd automatically.
/// </summary>
[DisallowMultipleComponent]
public class SlimeTrailEffect : MonoBehaviour
{
    [Header("Prefab")]
    [Tooltip("Prefab for the slime puddle left on the floor. Needs a flat trigger Collider.")]
    public GameObject slimePuddlePrefab;

    [Header("Trail Cadence")]
    [Tooltip("Minimum seconds between puddle drops.")]
    public float dropInterval = 0.45f;
    [Tooltip("Don't drop a new puddle unless we've moved at least this far from the last one.")]
    public float minDistanceBetweenPuddles = 0.6f;
    [Tooltip("Drop a puddle immediately on spawn.")]
    public bool dropOnStart = true;

    [Header("Puddle Settings")]
    public float puddleDuration = 6f;
    public int   puddleTickDamage = 5;
    public float puddleTickInterval = 0.5f;
    [Range(0f, 0.95f)]
    public float puddleSlowPercent = 0.30f;

    [Header("Y Offset")]
    [Tooltip("Small Y nudge so puddle decal sits on the floor rather than inside it.")]
    public float groundYOffset = 0.02f;

    float nextDropTime;
    Vector3 lastDropPos;

    void Start()
    {
        nextDropTime = Time.time + dropInterval;
        lastDropPos  = transform.position;

        if (dropOnStart) DropPuddle();
    }

    void Update()
    {
        if (Time.time < nextDropTime) return;

        float dist = Vector3.Distance(
            new Vector3(transform.position.x, 0, transform.position.z),
            new Vector3(lastDropPos.x, 0, lastDropPos.z));

        if (dist < minDistanceBetweenPuddles) return;

        DropPuddle();
        nextDropTime = Time.time + dropInterval;
    }

    void OnDestroy()
    {
        // Don't spawn during scene teardown — Unity flags this as a leak.
        if (!Application.isPlaying) return;
        DropPuddle();
    }

    void DropPuddle()
    {
        if (!slimePuddlePrefab) return;

        Vector3 pos = transform.position;
        pos.y += groundYOffset;

        var go     = Instantiate(slimePuddlePrefab, pos, Quaternion.identity);
        var puddle = go.GetComponent<SlimePuddle>() ?? go.AddComponent<SlimePuddle>();

        puddle.Init(puddleDuration, puddleTickDamage, puddleTickInterval, puddleSlowPercent);
        lastDropPos = transform.position;
    }
}
