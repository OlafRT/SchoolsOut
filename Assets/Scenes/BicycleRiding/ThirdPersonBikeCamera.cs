using UnityEngine;

[DefaultExecutionOrder(1000)]
public class ThirdPersonBikeCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public Rigidbody targetRigidbody;
    public Vector3 targetOffset = new Vector3(0f, 1.2f, 0f);

    [Header("Orbit")]
    public float distance = 5f, minDistance = 2f, maxDistance = 8f;
    public float zoomSensitivity = 5f;
    public float mouseXSens = 120f, mouseYSens = 90f;
    public float minPitch = -20f, maxPitch = 60f;

    [Header("Smoothing (set to 0 to SNAP)")]
    [Tooltip("How fast the internal smoothed-bike-position catches up to the real one. " +
             "This is the PRIMARY jank absorber — increase it to hide pedal impulses.")]
    public float bikePosSmoothTime  = 0.14f;   // NEW — absorbs pedal impulse spikes

    [Tooltip("Additional lag on the focus point on top of bikePosSmoothTime.")]
    public float focusSmoothTime    = 0.08f;

    [Tooltip("Lag on the camera's own world position.")]
    public float positionSmoothTime = 0.10f;

    [Tooltip("How fast the camera rotates to face the focus point. Lower = more lag.")]
    public float rotationLerp       = 12f;

    [Header("Look-Ahead")]
    [Tooltip("Camera focus point is nudged forward by velocity * this value. " +
             "Gives a sense of looking where you're going.")]
    public float lookAheadDistance  = 0.6f;    // NEW

    [Tooltip("How fast the look-ahead offset follows velocity changes.")]
    public float lookAheadSmoothTime = 0.18f;  // NEW

    [Header("Collision")]
    public LayerMask collisionMask = ~0;
    public float collisionRadius = 0.2f, collisionBuffer = 0.15f;

    [Header("Auto Align (optional)")]
    public bool autoAlign = true;
    public float alignSpeed = 4f, alignStartSpeed = 2f, alignBlendMaxSpeed = 10f;

    [Tooltip("How fast the auto-align yaw target itself is smoothed. " +
             "Prevents the camera from snapping when the bike steers sharply.")]
    public float alignYawSmoothTime = 0.20f;   // NEW

    [Header("Optional FOV kick")]
    public Camera cam;
    public float baseFOV = 60f, fovAtTopSpeed = 72f, topSpeedForFOV = 10f;
    public BicycleController bike;

    [Header("Startup")]
    public float startPitch = 25f;
    public bool lockAndHideCursor = true;

    // --- runtime state ---
    float yaw, pitch, curDistance;

    // Layer 1: smoothed bike world position (absorbs physics impulse spikes)
    Vector3 smoothedBikePos, smoothedBikePosVel;

    // Layer 2: smoothed focus point (on top of layer 1)
    Vector3 focusPos, focusVel;

    // Layer 3: smoothed camera world position
    Vector3 camVel;

    // Look-ahead
    Vector3 lookAheadOffset, lookAheadVel;

    // Auto-align yaw smoothing
    float smoothedAlignYaw, alignYawVel;

    void Start()
    {
        if (!cam) cam = GetComponentInChildren<Camera>();
        if (!target) { enabled = false; return; }

        if (targetRigidbody) targetRigidbody.interpolation = RigidbodyInterpolation.Interpolate;

        Vector3 startPos = RigidbodyPos();

        // Seed all three layers at the same position so there's no initial snap/lerp
        smoothedBikePos = startPos;
        focusPos        = startPos + targetOffset;
        lookAheadOffset = Vector3.zero;
        transform.position = focusPos + Vector3.back * distance;

        yaw   = Norm(RigidbodyRot().eulerAngles.y);
        pitch = Mathf.Clamp(startPitch, minPitch, maxPitch);

        smoothedAlignYaw = yaw;

        distance    = Mathf.Clamp(distance, minDistance, maxDistance);
        curDistance = distance;
        if (cam) cam.fieldOfView = baseFOV;

        if (lockAndHideCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void LateUpdate()
    {
        if (!target) return;

        // ── INPUT ─────────────────────────────────────────────────────────────
        float mx = Input.GetAxisRaw("Mouse X");
        float my = Input.GetAxisRaw("Mouse Y");
        float mw = Input.GetAxis("Mouse ScrollWheel");

        yaw   += mx * mouseXSens * Time.deltaTime;
        pitch -= my * mouseYSens * Time.deltaTime;
        pitch  = Mathf.Clamp(pitch, minPitch, maxPitch);
        distance = Mathf.Clamp(distance - mw * zoomSensitivity, minDistance, maxDistance);

        // ── LAYER 1: smooth the raw bike position ──────────────────────────────
        // This is the main jank absorber. The camera never sees the raw position;
        // it only ever sees this smoothed version. Pedal impulses become gentle
        // accelerations instead of sudden pops.
        Vector3 rawBikePos = RigidbodyPos();
        smoothedBikePos = bikePosSmoothTime <= 0f
            ? rawBikePos
            : Vector3.SmoothDamp(smoothedBikePos, rawBikePos, ref smoothedBikePosVel, bikePosSmoothTime);

        // ── LOOK-AHEAD: nudge focus in the direction of travel ─────────────────
        Vector3 bikeVel = targetRigidbody ? targetRigidbody.velocity : Vector3.zero;
        Vector3 horizVel = Vector3.ProjectOnPlane(bikeVel, Vector3.up);
        Vector3 targetLookAhead = horizVel.normalized * (Mathf.Min(horizVel.magnitude, 10f) / 10f * lookAheadDistance);
        lookAheadOffset = lookAheadSmoothTime <= 0f
            ? targetLookAhead
            : Vector3.SmoothDamp(lookAheadOffset, targetLookAhead, ref lookAheadVel, lookAheadSmoothTime);

        // ── LAYER 2: smooth the focus point ───────────────────────────────────
        Vector3 rawFocus = smoothedBikePos + targetOffset + lookAheadOffset;
        focusPos = focusSmoothTime <= 0f
            ? rawFocus
            : Vector3.SmoothDamp(focusPos, rawFocus, ref focusVel, focusSmoothTime);

        // ── AUTO ALIGN ────────────────────────────────────────────────────────
        float speed = bike ? bike.CurrentSpeed : 0f;
        if (autoAlign && speed > alignStartSpeed && Mathf.Abs(mx) < 0.01f)
        {
            // First smooth the target yaw itself so abrupt steers don't whip
            // the camera around — they become gradual turns instead.
            float rawTargetYaw = Norm(RigidbodyRot().eulerAngles.y);
            smoothedAlignYaw = Mathf.SmoothDampAngle(
                smoothedAlignYaw, rawTargetYaw, ref alignYawVel, alignYawSmoothTime);

            float t = Mathf.InverseLerp(alignStartSpeed, alignBlendMaxSpeed, speed);
            yaw = Mathf.LerpAngle(yaw, smoothedAlignYaw, alignSpeed * t * Time.deltaTime);
        }

        // ── DESIRED CAMERA POSITION ───────────────────────────────────────────
        Quaternion rigRot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 desired   = focusPos + rigRot * Vector3.back * distance;

        // ── COLLISION ─────────────────────────────────────────────────────────
        Vector3 dir  = desired - focusPos;
        float   dist = dir.magnitude;
        Vector3 final = desired;
        if (dist > 0.001f && Physics.SphereCast(
                focusPos, collisionRadius, dir.normalized,
                out RaycastHit hit, dist, collisionMask, QueryTriggerInteraction.Ignore))
            final = hit.point - dir.normalized * collisionBuffer;

        curDistance = (final - focusPos).magnitude;

        // ── LAYER 3: smooth the camera world position ─────────────────────────
        transform.position = positionSmoothTime <= 0f
            ? final
            : Vector3.SmoothDamp(transform.position, final, ref camVel, positionSmoothTime);

        // ── ROTATION ──────────────────────────────────────────────────────────
        Quaternion aim = Quaternion.LookRotation(focusPos - transform.position, Vector3.up);
        transform.rotation = rotationLerp <= 0f
            ? aim
            : Quaternion.Slerp(transform.rotation, aim, rotationLerp * Time.deltaTime);

        // ── FOV KICK ──────────────────────────────────────────────────────────
        if (cam)
        {
            float tf = Mathf.Clamp01(speed / Mathf.Max(0.01f, topSpeedForFOV));
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, Mathf.Lerp(baseFOV, fovAtTopSpeed, tf), 6f * Time.deltaTime);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    Vector3    RigidbodyPos() => targetRigidbody ? targetRigidbody.position : target.position;
    Quaternion RigidbodyRot() => targetRigidbody ? targetRigidbody.rotation : target.rotation;

    static float Norm(float a) { a %= 360f; if (a > 180f) a -= 360f; if (a < -180f) a += 360f; return a; }
}