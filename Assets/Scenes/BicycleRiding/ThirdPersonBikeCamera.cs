using UnityEngine;

[DefaultExecutionOrder(1000)]
public class ThirdPersonBikeCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public Rigidbody targetRigidbody;           // <- NEW (assign your bike/player RB)
    public Vector3 targetOffset = new Vector3(0f, 1.2f, 0f);

    [Header("Orbit")]
    public float distance = 5f, minDistance = 2f, maxDistance = 8f;
    public float zoomSensitivity = 5f;
    public float mouseXSens = 120f, mouseYSens = 90f;
    public float minPitch = -20f, maxPitch = 60f;

    [Header("Smoothing (set to 0 to SNAP)")]
    public float focusSmoothTime    = 0.06f;    // 0 = no lag
    public float positionSmoothTime = 0.04f;    // 0 = no lag
    public float rotationLerp       = 16f;      // 0 = snap aim

    [Header("Collision")]
    public LayerMask collisionMask = ~0;
    public float collisionRadius = 0.2f, collisionBuffer = 0.15f;

    [Header("Auto Align (optional)")]
    public bool autoAlign = true;
    public float alignSpeed = 4f, alignStartSpeed = 2f, alignBlendMaxSpeed = 10f;

    [Header("Optional FOV kick")]
    public Camera cam;
    public float baseFOV = 60f, fovAtTopSpeed = 72f, topSpeedForFOV = 10f;
    public BicycleController bike;

    [Header("Startup")]
    public float startPitch = 25f;          // desired initial X angle
    public bool lockAndHideCursor = true;   // lock & hide mouse on Start

    float yaw, pitch, curDistance;
    Vector3 focusPos, focusVel, camVel;

    void Start()
    {
        if (!cam) cam = GetComponentInChildren<Camera>();
        if (!target) { enabled = false; return; }

        if (targetRigidbody) targetRigidbody.interpolation = RigidbodyInterpolation.Interpolate;

        // Initialize focus at target (no spring)
        focusPos = (targetRigidbody ? targetRigidbody.position : target.position) + targetOffset;

        // Yaw from current facing of the target; pitch from our desired value
        yaw   = Norm((targetRigidbody ? targetRigidbody.rotation : target.rotation).eulerAngles.y);
        pitch = Mathf.Clamp(startPitch, minPitch, maxPitch);

        distance     = Mathf.Clamp(distance, minDistance, maxDistance);
        curDistance  = distance;
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

        // INPUT
        float mx = Input.GetAxisRaw("Mouse X");
        float my = Input.GetAxisRaw("Mouse Y");
        float mw = Input.GetAxis("Mouse ScrollWheel");

        yaw   += mx * mouseXSens * Time.deltaTime;
        pitch -= my * mouseYSens * Time.deltaTime;
        pitch  = Mathf.Clamp(pitch, minPitch, maxPitch);
        distance = Mathf.Clamp(distance - mw * zoomSensitivity, minDistance, maxDistance);

        // AUTO ALIGN (doesn't fight mouse)
        float speed = bike ? bike.CurrentSpeed : 0f;
        if (autoAlign && speed > alignStartSpeed && Mathf.Abs(mx) < 0.01f)
        {
            float t = Mathf.InverseLerp(alignStartSpeed, alignBlendMaxSpeed, speed);
            float targetYaw = Norm((targetRigidbody ? targetRigidbody.rotation : target.rotation).eulerAngles.y);
            yaw = Mathf.LerpAngle(yaw, targetYaw, alignSpeed * t * Time.deltaTime);
        }

        // FOCUS from target or RB (interpolated) — no feedback loop
        Vector3 rawFocus = (targetRigidbody ? targetRigidbody.position : target.position) + targetOffset;
        focusPos = focusSmoothTime <= 0f
            ? rawFocus
            : Vector3.SmoothDamp(focusPos, rawFocus, ref focusVel, focusSmoothTime);

        // DESIRED CAMERA
        Quaternion rigRot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 desired = focusPos + rigRot * Vector3.back * distance;

        // COLLISION
        Vector3 dir = desired - focusPos;
        float dist = dir.magnitude;
        Vector3 final = desired;
        if (dist > 0.001f && Physics.SphereCast(focusPos, collisionRadius, dir.normalized, out RaycastHit hit, dist, collisionMask, QueryTriggerInteraction.Ignore))
            final = hit.point - dir.normalized * collisionBuffer;

        curDistance = (final - focusPos).magnitude;

        // APPLY (snap if smooth time/lerp are zero)
        transform.position = positionSmoothTime <= 0f
            ? final
            : Vector3.SmoothDamp(transform.position, final, ref camVel, positionSmoothTime);

        Quaternion aim = Quaternion.LookRotation(focusPos - transform.position, Vector3.up);
        transform.rotation = rotationLerp <= 0f
            ? aim
            : Quaternion.Slerp(transform.rotation, aim, rotationLerp * Time.deltaTime);

        // FOV
        if (cam)
        {
            float tf = Mathf.Clamp01((bike ? bike.CurrentSpeed : 0f) / Mathf.Max(0.01f, topSpeedForFOV));
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, Mathf.Lerp(baseFOV, fovAtTopSpeed, tf), 6f * Time.deltaTime);
        }
    }

    static float Norm(float a){ a%=360f; if(a>180f)a-=360f; if(a<-180f)a+=360f; return a; }
}
