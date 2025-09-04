using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class MinimapCameraController : MonoBehaviour
{
    [Header("Follow")]
    public Transform target;                 // Player
    public Vector3 worldOffset = new Vector3(0f, 50f, 0f); // Height above player
    public float followSmooth = 15f;         // 0 = snap

    [Header("Rotation Lock")]
    public float pitchDown = 90f;            // Look straight down
    public bool lockYawAndRoll = true;       // Keep map “north-up”

    [Header("Zoom (Orthographic)")]
    public float defaultSize = 15f;
    public float step = 5f;
    public float minSize = 5f;
    public float maxSize = 30f;

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

        // Follow (smooth in world space)
        Vector3 wantedPos = target.position + worldOffset;
        transform.position = (followSmooth <= 0f)
            ? wantedPos
            : Vector3.Lerp(transform.position, wantedPos, 1f - Mathf.Exp(-followSmooth * Time.deltaTime));

        // Keep the minimap from rotating with the player
        if (lockYawAndRoll)
            transform.rotation = Quaternion.Euler(pitchDown, 0f, 0f); // north-up
        else
            transform.rotation = Quaternion.Euler(pitchDown, target.eulerAngles.y, 0f); // rotate with player if you ever want it
    }

    // Hook these to your + / – UI buttons (OnClick)
    public void ZoomIn()  => SetSize(cam.orthographicSize - step); // smaller size = zoom in
    public void ZoomOut() => SetSize(cam.orthographicSize + step); // larger size  = zoom out

    void SetSize(float s)
    {
        cam.orthographicSize = Mathf.Clamp(s, minSize, maxSize);
    }
}