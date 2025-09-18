using UnityEngine;

[DisallowMultipleComponent]
public class EyeballsLookAtMouse : MonoBehaviour
{
    [System.Serializable]
    public class Eye
    {
        public Transform transform;          // EyeballLeft / EyeballRight
        [Tooltip("Flip horizontal (yaw) just for this eye.")]
        public bool flipYaw = false;
        [Tooltip("Flip vertical (pitch) just for this eye.")]
        public bool flipPitch = false;

        [HideInInspector] public Quaternion baseLocal;
        [HideInInspector] public float yaw, pitch;  // smoothed
    }

    [Header("Refs")]
    [SerializeField] private Camera cam;           // leave empty -> Camera.main
    [SerializeField] private Transform head;       // parent of eyeballs (Head bone)
    [SerializeField] private Eye leftEye;
    [SerializeField] private Eye rightEye;

    [Header("Targeting")]
    [Tooltip("Mouse is projected to a plane this far in front of the head (m).")]
    [SerializeField] private float aimPlaneOffset = 0f;

    [Header("Limits (deg)")]
    [SerializeField, Range(0,60)] private float yawLimit = 30f;    // L/R
    [SerializeField, Range(0,60)] private float pitchLimit = 20f;  // Up/Down
    [SerializeField] private bool invertYaw = false;   // global
    [SerializeField] private bool invertPitch = false; // global

    [Header("Motion")]
    [SerializeField] private float followSpeed = 12f;
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Local Up (rarely needed)")]
    [SerializeField] private Vector3 localUp = Vector3.up; // eye parent space

    void Awake()
    {
        if (!cam) cam = Camera.main;
        if (!head) head = (leftEye.transform ? leftEye.transform.parent : transform);

        if (leftEye.transform)  leftEye.baseLocal  = leftEye.transform.localRotation;
        if (rightEye.transform) rightEye.baseLocal = rightEye.transform.localRotation;
    }

    void LateUpdate()
    {
        if (!cam || (!leftEye.transform && !rightEye.transform) || !head) return;

        // Project cursor to a plane near the head, facing the camera
        Vector3 planePoint = head.position + cam.transform.forward * aimPlaneOffset;
        Plane plane = new Plane(-cam.transform.forward, planePoint);
        Vector3 target = planePoint + cam.transform.forward;
        var ray = cam.ScreenPointToRay(Input.mousePosition);
        if (plane.Raycast(ray, out float d)) target = ray.GetPoint(d);

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float k = 1f - Mathf.Exp(-followSpeed * Mathf.Max(0.0001f, dt));

        AimEye(leftEye,  target, k);
        AimEye(rightEye, target, k);
    }

    void AimEye(Eye eye, Vector3 worldTarget, float lerp)
    {
        if (eye == null || !eye.transform) return;

        Transform parent = eye.transform.parent ? eye.transform.parent : transform;

        // Direction to target in the eye's parent local space
        Vector3 localTarget = parent.InverseTransformPoint(worldTarget);
        Vector3 dirLocal = (localTarget - eye.transform.localPosition).normalized;

        // Desired local look rotation
        Quaternion lookLocal = Quaternion.LookRotation(dirLocal, localUp);

        // Delta from neutral -> yaw/pitch
        Quaternion delta = Quaternion.Inverse(eye.baseLocal) * lookLocal;
        Vector3 ang = delta.eulerAngles;
        float pitch = Normalize180(ang.x);  // +up
        float yaw   = Normalize180(ang.y);  // +right

        // Apply global inverts, then per-eye flips
        if (invertYaw)   yaw   = -yaw;
        if (invertPitch) pitch = -pitch;
        if (eye.flipYaw)   yaw   = -yaw;
        if (eye.flipPitch) pitch = -pitch;

        // Clamp, smooth, apply
        yaw   = Mathf.Clamp(yaw,   -yawLimit,   yawLimit);
        pitch = Mathf.Clamp(pitch, -pitchLimit, pitchLimit);

        eye.yaw   = Mathf.Lerp(eye.yaw,   yaw,   lerp);
        eye.pitch = Mathf.Lerp(eye.pitch, pitch, lerp);

        eye.transform.localRotation = eye.baseLocal * Quaternion.Euler(eye.pitch, eye.yaw, 0f);
    }

    static float Normalize180(float a)
    {
        a %= 360f; if (a > 180f) a -= 360f; if (a < -180f) a += 360f; return a;
    }

    [ContextMenu("Rebase eyes to current pose")]
    void Rebase()
    {
        if (leftEye.transform)  { leftEye.baseLocal  = leftEye.transform.localRotation; leftEye.yaw = leftEye.pitch = 0f; }
        if (rightEye.transform) { rightEye.baseLocal = rightEye.transform.localRotation; rightEye.yaw = rightEye.pitch = 0f; }
    }
}
