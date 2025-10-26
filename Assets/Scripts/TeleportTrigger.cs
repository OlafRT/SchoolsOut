using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class TeleportTrigger : MonoBehaviour
{
    [Header("Destination")]
    [Tooltip("If set, we go here. Otherwise we use Target Offset (relative to this trigger).")]
    public Transform target;
    public Vector3 targetOffset;

    [Header("Presentation")]
    public AudioClip teleportSfx;
    [Range(0f,1f)] public float sfxVolume = 1f;
    public float extraBlackPause = 0.0f; // optional tiny pause while black

    Collider myCol;

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
        if (guard != null && guard.IsIgnoring(myCol)) return; // still standing on me? ignore

        var pm = other.GetComponent<PlayerMovement>();
        if (!pm) return;

        StartCoroutine(TeleportRoutine(pm, guard));
    }

    IEnumerator TeleportRoutine(PlayerMovement pm, PlayerPortalGuard guard)
    {
        // Optional: stop player input instantly
        pm.canMove = false; // we re-enable via pm.RebaseTo(..., withCooldown:true) below. :contentReference[oaicite:1]{index=1}

        if (teleportSfx) AudioSource.PlayClipAtPoint(teleportSfx, transform.position, sfxVolume);

        Vector3 dest = target ? target.position : transform.TransformPoint(targetOffset);

        yield return ScreenFader.I.FadeOutIn(() =>
        {
            // Snap to grid & apply brief cooldown using your existing helper.
            pm.RebaseTo(dest, withCooldown: true); // this rounds to tile & uses TeleportCooldown. :contentReference[oaicite:2]{index=2}
        });

        if (extraBlackPause > 0f) yield return new WaitForSecondsRealtime(extraBlackPause);

        // After arrival: find any portal trigger under our feet and ignore it until we step off.
        if (guard)
        {
            var hits = Physics.OverlapSphere(pm.transform.position + Vector3.up * 0.1f, 0.25f, ~0, QueryTriggerInteraction.Collide);
            foreach (var h in hits)
            {
                if (h.TryGetComponent<TeleportTrigger>(out var _))
                {
                    guard.IgnoreThisPortal(h);
                    break; // just pick the first portal we stand on
                }
            }
        }
    }
}
