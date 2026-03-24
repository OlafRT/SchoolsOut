using UnityEngine;
using System.Collections;

/// <summary>
/// Attach to each individual door panel. Set slideDirection to the axis the
/// door should move along (e.g. Vector3.right for the right door, Vector3.left
/// for the left door) and slideDistance to how many units it travels when open.
/// </summary>
public class ElevatorDoor : MonoBehaviour
{
    [Header("Slide Settings")]
    [Tooltip("Local-space direction this panel slides when opening. " +
             "Right door: (1,0,0)  Left door: (-1,0,0)")]
    public Vector3 slideDirection = Vector3.right;

    [Tooltip("Units the door panel travels from fully-closed to fully-open.")]
    public float slideDistance = 1.2f;

    [Tooltip("Seconds to fully open or close.")]
    public float slideDuration = 0.8f;

    // Cached positions so repeated calls are safe regardless of current state
    Vector3 closedLocalPos;
    Vector3 openLocalPos;

    Coroutine slideRoutine;

    void Awake()
    {
        closedLocalPos = transform.localPosition;
        openLocalPos   = closedLocalPos + slideDirection.normalized * slideDistance;
    }

    // ── Public API ──────────────────────────────────────────────────────────

    public void Open()  => Slide(openLocalPos);
    public void Close() => Slide(closedLocalPos);

    // ── Internal ────────────────────────────────────────────────────────────

    void Slide(Vector3 target)
    {
        if (slideRoutine != null) StopCoroutine(slideRoutine);
        slideRoutine = StartCoroutine(SlideRoutine(target));
    }

    IEnumerator SlideRoutine(Vector3 target)
    {
        Vector3 start = transform.localPosition;
        float t = 0f;
        float dur = Mathf.Max(0.01f, slideDuration);

        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
            transform.localPosition = Vector3.Lerp(start, target, k);
            yield return null;
        }

        transform.localPosition = target;
        slideRoutine = null;
    }
}
