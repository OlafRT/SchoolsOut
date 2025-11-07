using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class TeleportTrigger : MonoBehaviour
{
    [Header("Destination")]
    [Tooltip("If set, we go here. Otherwise we use Target Offset (relative to this trigger).")]
    public Transform target;
    public Vector3 targetOffset;

    public enum ArrivalFacing
    {
        KeepCurrent,   // keep whatever the player had
        North,         // +Z
        South,         // -Z
        East,          // +X
        West,          // -X
        MatchTarget,   // use target.forward (if target assigned)
        CustomYaw      // use customYawDegrees around Y
    }

    [Header("Arrival Facing")]
    public ArrivalFacing arrivalFacing = ArrivalFacing.KeepCurrent;
    [Tooltip("Only used when ArrivalFacing = CustomYaw (degrees around Y).")]
    public float customYawDegrees = 0f;

    [Header("Presentation")]
    public AudioClip teleportSfx;
    [Range(0f,1f)] public float sfxVolume = 1f;
    public float extraBlackPause = 0.0f; // optional tiny pause while black

    [Header("Safety")]
    [Tooltip("Prevents rapid ping-pong teleports; time in seconds between allowed teleports for a player.")]
    public float teleportDebounce = 0.25f;
    [Tooltip("Wait one frame after arrival before ignoring the destination trigger.")]
    public bool waitOneFrameBeforeIgnore = true;

    Collider myCol;

    // Prevent concurrent teleports per player
    static readonly HashSet<PlayerMovement> activeTeleports = new HashSet<PlayerMovement>();
    // Per-player short cooldown between teleports (unscaled time so fades don’t affect it)
    static readonly Dictionary<PlayerMovement, float> nextAllowedTeleport = new Dictionary<PlayerMovement, float>();

    void Reset()
    {
        myCol = GetComponent<Collider>();
        myCol.isTrigger = true;
    }

    void Awake()
    {
        myCol = GetComponent<Collider>();
        myCol.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        var guard = other.GetComponent<PlayerPortalGuard>();
        if (guard != null && guard.IsIgnoring(myCol)) return;

        var pm = other.GetComponent<PlayerMovement>();
        if (!pm) return;

        // Debounce: too soon since last teleport for this player
        if (nextAllowedTeleport.TryGetValue(pm, out float nextT) && Time.unscaledTime < nextT)
            return;

        // Already teleporting? ignore
        if (activeTeleports.Contains(pm)) return;

        // Immediately ignore the SOURCE portal so tiny re-entries don't re-trigger
        if (guard != null) guard.IgnoreThisPortal(myCol);

        StartCoroutine(TeleportRoutine(pm, guard));
    }

    IEnumerator TeleportRoutine(PlayerMovement pm, PlayerPortalGuard guard)
    {
        activeTeleports.Add(pm);
        bool restoreMove = false;

        try
        {
            pm.canMove = false;  // no height/rigidbody changes; just freeze input
            restoreMove = true;

            if (teleportSfx) AudioSource.PlayClipAtPoint(teleportSfx, transform.position, sfxVolume);

            Vector3 dest = target ? target.position : transform.TransformPoint(targetOffset);

            yield return ScreenFader.I.FadeOutIn(() =>
            {
                // Snap & apply your internal teleport cooldown
                pm.RebaseTo(dest, withCooldown: true);

                // Apply arrival facing WHILE the screen is black
                var fwd = GetArrivalForward(target);
                if (fwd.sqrMagnitude > 0.5f) // not KeepCurrent
                {
                    var yOnly = new Vector3(fwd.x, 0f, fwd.z);
                    if (yOnly.sqrMagnitude > 0.0001f)
                        pm.transform.rotation = Quaternion.LookRotation(yOnly, Vector3.up);

                    // If your PlayerMovement exposes a method to sync facing/anim, prefer:
                    // pm.FaceDirection(yOnly);
                }
            });

            if (extraBlackPause > 0f)
                yield return new WaitForSecondsRealtime(extraBlackPause);

            if (waitOneFrameBeforeIgnore) yield return null;

            // Ignore the DESTINATION portal we arrived on until stepping off it
            if (guard)
            {
                var hits = Physics.OverlapSphere(pm.transform.position, 0.25f, ~0, QueryTriggerInteraction.Collide);
                foreach (var h in hits)
                {
                    if (h.TryGetComponent<TeleportTrigger>(out var _))
                    {
                        guard.IgnoreThisPortal(h);
                        break;
                    }
                }
            }

            // Set short per-player debounce
            nextAllowedTeleport[pm] = Time.unscaledTime + Mathf.Max(0.05f, teleportDebounce);
        }
        finally
        {
            if (restoreMove) pm.canMove = true;
            activeTeleports.Remove(pm);
        }
    }

    Vector3 GetArrivalForward(Transform targetT)
    {
        switch (arrivalFacing)
        {
            case ArrivalFacing.North: return Vector3.forward;   // +Z
            case ArrivalFacing.South: return Vector3.back;      // -Z
            case ArrivalFacing.East:  return Vector3.right;     // +X
            case ArrivalFacing.West:  return Vector3.left;      // -X
            case ArrivalFacing.MatchTarget:
                if (targetT) return new Vector3(targetT.forward.x, 0f, targetT.forward.z).normalized;
                return Vector3.forward;
            case ArrivalFacing.CustomYaw:
                {
                    // yaw around Y (flat world)
                    var rot = Quaternion.Euler(0f, customYawDegrees, 0f);
                    return rot * Vector3.forward;
                }
            case ArrivalFacing.KeepCurrent:
            default:
                return Vector3.zero; // zero = don't change
        }
    }
}
