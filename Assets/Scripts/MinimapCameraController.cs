using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class MinimapCameraController : MonoBehaviour
{
    [Header("Follow")]
    public Transform target;                         // Player root
    public Vector3 worldOffset = new Vector3(0, 50, 0);
    public float followSmooth = 15f;

    [Header("Rotation Lock")]
    public float pitchDown = 90f;                    // Straight down
    public bool lockYawAndRoll = true;               // true = north-up map

    [Header("Zoom (Orthographic)")]
    public float defaultSize = 15f;
    public float step = 5f;
    public float minSize = 5f;
    public float maxSize = 30f;

    [Header("UI Arrow (HUD)")]
    public RectTransform headingArrow;               // UI element anchored to map center
    public float arrowSmooth = 20f;                  // 0 = snap
    public bool invertArrow = true;                  // flip if your art points “up”

    Camera cam;

    void Awake()
    {
        cam = GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = Mathf.Clamp(defaultSize, minSize, maxSize);
    }

    void LateUpdate()
    {
        if (!target) return;

        // Follow
        Vector3 wantedPos = target.position + worldOffset;
        transform.position = (followSmooth <= 0f)
            ? wantedPos
            : Vector3.Lerp(transform.position, wantedPos, 1f - Mathf.Exp(-followSmooth * Time.deltaTime));

        // Keep map north-up (no yaw/roll)
        if (lockYawAndRoll)
            transform.rotation = Quaternion.Euler(pitchDown, 0f, 0f);
        else
            transform.rotation = Quaternion.Euler(pitchDown, target.eulerAngles.y, 0f);

        // Rotate UI arrow to show player heading
        if (headingArrow)
        {
            // Target angle in UI space (Z-rotation). 
            // For “north-up” maps this should be opposite the player yaw in screen space.
            float sign = invertArrow ? -1f : 1f;
            float targetZ = sign * target.eulerAngles.y;

            Quaternion targetRot = Quaternion.Euler(0f, 0f, targetZ);
            if (arrowSmooth <= 0f)
                headingArrow.rotation = targetRot;
            else
                headingArrow.rotation = Quaternion.Slerp(
                    headingArrow.rotation, targetRot, 1f - Mathf.Exp(-arrowSmooth * Time.deltaTime));
        }
    }

    // Hook these to your + / – buttons:
    public void ZoomIn()  => SetSize(cam.orthographicSize - step); // zoom in
    public void ZoomOut() => SetSize(cam.orthographicSize + step); // zoom out

    void SetSize(float s) => cam.orthographicSize = Mathf.Clamp(s, minSize, maxSize);
}
