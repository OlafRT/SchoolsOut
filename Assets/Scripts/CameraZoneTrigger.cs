// CameraZoneTrigger.cs
// ─────────────────────────────────────────────────────────────────────────────
// Drop on any GameObject with a trigger Collider.
// When the player walks in, the camera smoothly zooms to the target level.
// When they walk out, it smoothly returns to wherever the zoom was before entry.
//
// SETUP
// ─────
//  1. Add this component and a Collider (Box, Sphere, etc.) to a GameObject.
//  2. Set the Collider's "Is Trigger" checkbox — the script enforces it in Awake.
//  3. Assign the CameraFollow reference, or leave it blank and it auto-finds.
//  4. Adjust targetZoom (0 = fully zoomed out, 1 = fully zoomed in) and
//     transitionSpeed in the Inspector.
// ─────────────────────────────────────────────────────────────────────────────

using UnityEngine;
using System.Collections;

[DisallowMultipleComponent]
public class CameraZoneTrigger : MonoBehaviour
{
    [Header("Camera")]
    [Tooltip("Auto-found in scene if left empty.")]
    public CameraFollow cameraFollow;

    [Header("Zoom Override")]
    [Tooltip("Zoom level to apply while inside this zone. " +
             "0 = fully zoomed out (offsetFar), 1 = fully zoomed in (offsetNear).")]
    [Range(0f, 1f)]
    public float targetZoom = 0.7f;

    [Tooltip("How quickly the zoom transitions in and out. " +
             "Units per second — e.g. 1.5 crosses the full 0→1 range in ~0.67 s.")]
    public float transitionSpeed = 2f;

    // ── State ─────────────────────────────────────────────────────────────────
    bool       _playerInside;
    float      _savedZoom;       // zoom value at the moment of entry, restored on exit
    Coroutine  _transitionCo;

    // ── Unity ─────────────────────────────────────────────────────────────────

    void Awake()
    {
        // Guarantee trigger is enabled regardless of Inspector setting
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;

        if (!cameraFollow)
            cameraFollow = FindFirstObjectByType<CameraFollow>();

        if (!cameraFollow)
            Debug.LogWarning($"[CameraZoneTrigger] '{name}': No CameraFollow found in scene.");
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (_playerInside) return;   // ignore re-entry while already inside

        _playerInside = true;
        _savedZoom    = cameraFollow.zoom;

        // Lock scroll so the player can't manually fight the forced zoom
        cameraFollow.SetScrollLocked(true);

        StartTransition(targetZoom);
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (!_playerInside) return;

        _playerInside = false;

        StartTransition(_savedZoom, unlockScrollOnComplete: true);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    void StartTransition(float target, bool unlockScrollOnComplete = false)
    {
        if (_transitionCo != null) StopCoroutine(_transitionCo);
        _transitionCo = StartCoroutine(TransitionZoom(target, unlockScrollOnComplete));
    }

    IEnumerator TransitionZoom(float target, bool unlockScrollOnComplete)
    {
        if (!cameraFollow) yield break;

        while (!Mathf.Approximately(cameraFollow.zoom, target))
        {
            cameraFollow.zoom = Mathf.MoveTowards(
                cameraFollow.zoom,
                target,
                transitionSpeed * Time.deltaTime);
            yield return null;
        }

        cameraFollow.zoom = target;
        _transitionCo     = null;

        if (unlockScrollOnComplete)
            cameraFollow.SetScrollLocked(false);
    }

    // ── Editor Gizmo ─────────────────────────────────────────────────────────
    // Draw a semi-transparent box in the Scene view so the zone is easy to see.

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        var col = GetComponent<Collider>();
        if (!col) return;

        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.15f);
        Gizmos.matrix = transform.localToWorldMatrix;

        if (col is BoxCollider box)
            Gizmos.DrawCube(box.center, box.size);
        else if (col is SphereCollider sphere)
            Gizmos.DrawSphere(sphere.center, sphere.radius);

        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.6f);
        if (col is BoxCollider box2)
            Gizmos.DrawWireCube(box2.center, box2.size);
        else if (col is SphereCollider sphere2)
            Gizmos.DrawWireSphere(sphere2.center, sphere2.radius);
    }
#endif
}
