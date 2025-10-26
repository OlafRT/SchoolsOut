using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    public Transform player;

    [Header("Zoom Presets (edit in Inspector)")]
    [Tooltip("Offset when fully zoomed OUT (zoom = 0).")]
    public Vector3 offsetFar = new Vector3(0f, 5f, -6.5f);

    [Tooltip("Offset when fully zoomed IN (zoom = 1).")]
    public Vector3 offsetNear = new Vector3(0f, 3.0f, -3.5f);

    [Tooltip("Pitch (X rotation) at zoom = 0 (degrees).")]
    public float pitchFar = 40f;

    [Tooltip("Pitch (X rotation) at zoom = 1 (degrees).")]
    public float pitchNear = 22f;

    [Header("Scroll Settings")]
    [Tooltip("How much zoom changes per scroll tick.")]
    public float scrollSensitivity = 0.12f;

    [Tooltip("Invert scroll direction (true = wheel up zooms OUT).")]
    public bool invertScroll = false;

    [Tooltip("Initial zoom level (0 = far, 1 = near).")]
    [Range(0f, 1f)] public float zoom = 0f;

    [Header("Smoothing")]
    [Tooltip("Higher = snappier (position).")]
    public float positionLerpSpeed = 18f;
    [Tooltip("Higher = snappier (rotation).")]
    public float rotationLerpSpeed = 18f;

    // --- NEW: FOV sprint kick ---
    [Header("FOV Kick (Sprint)")]
    [Tooltip("Camera FOV when not sprinting.")]
    public float fovDefault = 60f;
    [Tooltip("Camera FOV while sprinting.")]
    public float fovSprint = 75f;
    [Tooltip("Smoothing for FOV change.")]
    public float fovLerpSpeed = 12f;

    // Shaker (and other systems) can add to this safely.
    [HideInInspector] public Vector3 extraOffset = Vector3.zero;

    float baseYaw;
    Camera cam;
    bool sprintOverride = false; // set via SetSprinting

    void Awake()
    {
        baseYaw = transform.eulerAngles.y;
        cam = GetComponent<Camera>();
        if (!cam) cam = GetComponentInChildren<Camera>(true);
        if (cam) cam.fieldOfView = fovDefault;
    }

    void Update()
    {
        // Zoom input (per-frame so it feels responsive)
        float scroll = Input.mouseScrollDelta.y;
        if (invertScroll) scroll = -scroll;
        if (Mathf.Abs(scroll) > 0.0001f)
            zoom = Mathf.Clamp01(zoom + scroll * scrollSensitivity);
    }

    void LateUpdate()
    {
        if (!player) return;

        // Interpolate offset & pitch based on zoom
        Vector3 targetOffset = Vector3.Lerp(offsetFar, offsetNear, zoom);
        float   targetPitch  = Mathf.Lerp(pitchFar,  pitchNear,  zoom);

        // Target position with shaker offset
        Vector3 desiredPos = player.position + targetOffset + extraOffset;
        float posT = 1f - Mathf.Exp(-positionLerpSpeed * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, desiredPos, posT);

        // Smooth pitch, keep yaw
        Quaternion desiredRot = Quaternion.Euler(targetPitch, baseYaw, 0f);
        float rotT = 1f - Mathf.Exp(-rotationLerpSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, rotT);

        // --- FOV sprint kick ---
        if (cam)
        {
            float targetFov = sprintOverride ? fovSprint : fovDefault;
            float fovT = 1f - Mathf.Exp(-fovLerpSpeed * Time.deltaTime);
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, fovT);
        }
    }

    /// <summary>Drive sprint state from your movement script (e.g., when using custom input).</summary>
    public void SetSprinting(bool isSprinting)
    {
        sprintOverride = isSprinting;
    }

    /// <summary>Optional: set zoom from other scripts (0..1).</summary>
    public void SetZoomNormalized(float t)
    {
        zoom = Mathf.Clamp01(t);
    }
}
