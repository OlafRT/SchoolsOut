using System.Collections;
using UnityEngine;

/// <summary>
/// Drives the full vent-emerge sequence for a slime minion.
/// CafeteriaLadyBoss injects ventWaypoints + landingTarget then calls Begin()
/// before Start() fires, so the sequence always has valid data.
/// </summary>

// Runs early so gameObject.name is cleaned before any Nameplate Awake reads it.
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public class SlimeVentEmerge : MonoBehaviour
{
    public enum Phase { Idle, Crawling, Launching, Splatting, Waking, Done }
    public Phase CurrentPhase { get; private set; } = Phase.Idle;

    [Header("Vent Path")]
    public Transform[] ventWaypoints;
    [Tooltip("Speed in m/s while crawling through the vent.")]
    public float crawlSpeed = 1.2f;
    [Tooltip("How close to a waypoint counts as arrived.")]
    public float waypointTolerance = 0.08f;

    [Header("Launch Arc")]
    [Tooltip("Floor target. If null a downward raycast is used.")]
    public Transform landingTarget;
    public float arcHeight = 1.8f;
    public float arcDuration = 0.65f;
    public LayerMask groundMask = ~0;
    public float groundRayLength = 12f;

    [Header("Splat on Landing")]
    public float splatImpulse = 3.5f;
    [Tooltip("Seconds to wait after the splat before NPC scripts wake up.")]
    public float wakeDelay = 0.35f;
    public GameObject splatVfxPrefab;
    public AudioClip splatSound;
    public AudioClip settleSound;

    [Header("NPC Components to Enable on Wake")]
    [Tooltip("Auto-found if left empty.")]
    public NPCAI           npcAI;
    public NPCMovement     npcMovement;
    public NPCHealth       npcHealth;
    public NPCAutoAttack   npcAutoAttack;
    public NPCBombAbility  npcBombAbility;
    public SlimeTrailEffect slimeTrail;

    SlimeJiggle _jiggle;
    Rigidbody   _rb;
    bool        _begun;

    void Awake()
    {
        // Strip Unity's "(Clone)" suffix immediately — [DefaultExecutionOrder(-100)]
        // ensures this runs before any Nameplate script reads gameObject.name.
        gameObject.name = gameObject.name.Replace("(Clone)", "").TrimEnd();

        _jiggle = GetComponent<SlimeJiggle>() ?? GetComponentInChildren<SlimeJiggle>();
        _rb     = GetComponent<Rigidbody>();

        if (!npcAI)          npcAI          = GetComponent<NPCAI>();
        if (!npcMovement)    npcMovement    = GetComponent<NPCMovement>();
        if (!npcHealth)      npcHealth      = GetComponent<NPCHealth>();
        if (!npcAutoAttack)  npcAutoAttack  = GetComponent<NPCAutoAttack>();
        if (!npcBombAbility) npcBombAbility = GetComponent<NPCBombAbility>();
        if (!slimeTrail)     slimeTrail     = GetComponent<SlimeTrailEffect>();

        if (_rb) { _rb.isKinematic = true; _rb.detectCollisions = false; }
    }

    /// <summary>
    /// Called by CafeteriaLadyBoss after injecting waypoints, before Start() fires.
    /// </summary>
    public void Begin()
    {
        if (_begun) return;
        _begun = true;

        if (ventWaypoints == null || ventWaypoints.Length == 0)
        {
            Debug.LogWarning($"[SlimeVentEmerge] {gameObject.name}: No waypoints assigned — waking NPC immediately.");
            WakeNPC();
            return;
        }

        transform.position = ventWaypoints[0].position;
        StartCoroutine(EmergeSequence());
    }

    void Start()
    {
        // Fallback for prefabs placed directly in a scene (no boss injecting waypoints).
        Begin();
    }

    IEnumerator EmergeSequence()
    {
        // 1. CRAWL
        CurrentPhase = Phase.Crawling;
        for (int i = 1; i < ventWaypoints.Length; i++)
        {
            if (!ventWaypoints[i]) continue;
            yield return StartCoroutine(CrawlTo(ventWaypoints[i].position));
        }

        // 2. LAUNCH
        CurrentPhase = Phase.Launching;
        Vector3 launchFrom = transform.position;
        Vector3 landPos    = ResolveLandingPosition(launchFrom);
        yield return StartCoroutine(LaunchArc(launchFrom, landPos));

        // 3. SPLAT
        CurrentPhase = Phase.Splatting;
        OnLand(landPos);
        yield return new WaitForSeconds(wakeDelay);

        // 4. WAKE
        CurrentPhase = Phase.Waking;
        WakeNPC();
        CurrentPhase = Phase.Done;
    }

    IEnumerator CrawlTo(Vector3 target)
    {
        while (Vector3.Distance(transform.position, target) > waypointTolerance)
        {
            Vector3 dir     = target - transform.position;
            Vector3 flatDir = new Vector3(dir.x, 0f, dir.z);
            if (flatDir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(flatDir.normalized, Vector3.up),
                    1f - Mathf.Exp(-12f * Time.deltaTime));

            transform.position = Vector3.MoveTowards(transform.position, target, crawlSpeed * Time.deltaTime);
            yield return null;
        }
        transform.position = target;
    }

    IEnumerator LaunchArc(Vector3 from, Vector3 to)
    {
        float t = 0f;
        Vector3 launchDir = to - from; launchDir.y = 0f;
        if (launchDir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(launchDir.normalized, Vector3.up);

        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, arcDuration);
            float tc  = Mathf.Clamp01(t);
            Vector3 pos = Vector3.Lerp(from, to, tc);
            pos.y += arcHeight * Mathf.Sin(tc * Mathf.PI);

            Vector3 vel = pos - transform.position;
            if (tc > 0.01f && vel.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(vel.normalized, Vector3.up),
                    1f - Mathf.Exp(-10f * Time.deltaTime));

            transform.position = pos;
            yield return null;
        }
        transform.position = to;
    }

    void OnLand(Vector3 landPos)
    {
        transform.position    = landPos;
        Vector3 e             = transform.eulerAngles;
        transform.eulerAngles = new Vector3(0f, e.y, 0f);

        if (_jiggle)
            for (int i = 0; i <= Mathf.RoundToInt(splatImpulse); i++)
                _jiggle.PlayHitJiggle();

        if (splatVfxPrefab) Instantiate(splatVfxPrefab, landPos, Quaternion.identity);

        var src = GetComponent<AudioSource>();
        if (src)
        {
            if (splatSound)  { src.pitch = Random.Range(0.85f, 1.1f); src.PlayOneShot(splatSound); }
            if (settleSound) StartCoroutine(PlayDelayed(src, settleSound, 0.18f));
        }
    }

    IEnumerator PlayDelayed(AudioSource src, AudioClip clip, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (src && clip) src.PlayOneShot(clip, 0.7f);
    }

    void WakeNPC()
    {
        if (_rb) { _rb.isKinematic = false; _rb.detectCollisions = true; }
        if (npcMovement) { npcMovement.enabled = true; npcMovement.HardStop(); }
        if (npcAI)          npcAI.enabled          = true;
        if (npcHealth)      npcHealth.enabled       = true;
        if (npcAutoAttack)  npcAutoAttack.enabled   = true;
        if (npcBombAbility) npcBombAbility.enabled  = true;
        if (slimeTrail)     slimeTrail.enabled       = true;
        enabled = false;
    }

    Vector3 ResolveLandingPosition(Vector3 fromWorld)
    {
        if (landingTarget) return landingTarget.position;
        Vector3 origin = fromWorld + Vector3.up * 0.5f;
        if (Physics.Raycast(origin, Vector3.down, out var hit, groundRayLength, groundMask, QueryTriggerInteraction.Ignore))
            return hit.point;
        return fromWorld + Vector3.down * 3f;
    }

    void OnDrawGizmos()
    {
        if (ventWaypoints == null || ventWaypoints.Length == 0) return;
        Gizmos.color = new Color(0.2f, 1f, 0.3f, 0.9f);
        for (int i = 0; i < ventWaypoints.Length; i++)
        {
            if (!ventWaypoints[i]) continue;
            Gizmos.DrawWireSphere(ventWaypoints[i].position, 0.12f);
            if (i > 0 && ventWaypoints[i - 1])
                Gizmos.DrawLine(ventWaypoints[i - 1].position, ventWaypoints[i].position);
        }
        if (ventWaypoints[^1] && landingTarget)
        {
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.7f);
            Vector3 a = ventWaypoints[^1].position, b = landingTarget.position, prev = a;
            for (int s = 1; s <= 20; s++)
            {
                float tc = s / 20f;
                Vector3 p = Vector3.Lerp(a, b, tc);
                p.y += arcHeight * Mathf.Sin(tc * Mathf.PI);
                Gizmos.DrawLine(prev, p); prev = p;
            }
        }
    }
}
