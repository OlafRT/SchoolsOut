using UnityEngine;

[RequireComponent(typeof(Camera))]
public class BreathCameraWobble : MonoBehaviour
{
    [Header("Max wobble at intensity = 1")]
    public float maxRollDeg  = 3.5f;
    public float maxYawDeg   = 2.0f;
    public float maxPitchDeg = 1.5f;

    [Header("Frequency (Hz)")]
    public float wobbleHz = 0.9f;

    [Header("FOV kick")]
    public bool alsoKickFOV = true;
    public float fovKickAtFull = 6f; // add up to this much FOV at full intensity

    Camera cam;
    Quaternion baseRot;
    float baseFOV;

    // Driven externally by the manager each frame.
    float externalIntensity = 0f;

    void Awake()
    {
        cam = GetComponent<Camera>();
        baseRot = transform.localRotation;
        baseFOV = cam.fieldOfView;
    }

    void LateUpdate()
    {
        float I = externalIntensity; // manager feeds us directly via SetStrength

        float t = Time.time * Mathf.PI * 2f * Mathf.Max(0.01f, wobbleHz);

        float roll  = Mathf.Sin(t * 0.9f) * maxRollDeg  * I;
        float yaw   = Mathf.Sin(t * 1.3f) * maxYawDeg   * I;
        float pitch = Mathf.Cos(t * 1.1f) * maxPitchDeg * I;

        transform.localRotation = baseRot * Quaternion.Euler(pitch, yaw, roll);

        if (alsoKickFOV && cam)
            cam.fieldOfView = baseFOV + fovKickAtFull * I;
    }

    /// Called by BreathEffectsManager every Update.
    public void SetStrength(float amount01, float hz)
    {
        externalIntensity = Mathf.Clamp01(amount01);
        wobbleHz = hz;
    }

    void OnDisable()
    {
        // reset
        if (cam) cam.fieldOfView = baseFOV;
        transform.localRotation = baseRot;
    }
}
