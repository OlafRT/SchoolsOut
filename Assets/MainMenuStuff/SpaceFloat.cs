using UnityEngine;

[DisallowMultipleComponent]
public class SpaceFloat : MonoBehaviour
{
    [Header("Time/Base Space")]
    [Tooltip("Use unscaled time (good for menus/paused games).")]
    public bool useUnscaledTime = true;
    [Tooltip("Animate in local space (default) or world space.")]
    public bool worldSpace = false;

    [Header("Position Bob (meters)")]
    [Tooltip("Amplitude per axis in meters.")]
    public Vector3 posAmplitude = new Vector3(0.02f, 0.04f, 0.02f);
    [Tooltip("Cycles per second for bobbing.")]
    public float posFrequency = 0.2f;

    [Header("Rotation Drift (degrees)")]
    [Tooltip("Max angular deviation (Euler degrees) around each axis.")]
    public Vector3 rotAmplitude = new Vector3(1.5f, 2f, 1.0f);
    [Tooltip("Cycles per second for slow rotation wobble.")]
    public float rotFrequency = 0.1f;

    [Header("Subtle Randomness")]
    [Tooltip("Blend in Perlin noise (0 = pure sine, 1 = all noise).")]
    [Range(0f, 1f)] public float noiseBlend = 0.35f;

    [Header("Optional gentle drift")]
    [Tooltip("Very small constant drift in meters/second.")]
    public float driftSpeed = 0.00f;

    Vector3 _basePos;
    Quaternion _baseRot;
    float _seedX, _seedY, _seedZ;
    Vector3 _driftDir;

    void OnEnable()
    {
        if (worldSpace)
        {
            _basePos = transform.position;
            _baseRot = transform.rotation;
        }
        else
        {
            _basePos = transform.localPosition;
            _baseRot = transform.localRotation;
        }

        // Different phase/noise per axis
        _seedX = Random.value * 1000f;
        _seedY = _seedX + 123.456f;
        _seedZ = _seedX + 987.654f;

        _driftDir = Random.onUnitSphere; // tiny constant drift direction
    }

    void Update()
    {
        float t = useUnscaledTime ? Time.unscaledTime : Time.time;
        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        // --- Position offset (sine + Perlin) ---
        Vector3 pSine = new Vector3(
            Mathf.Sin((t + _seedX) * Mathf.PI * 2f * posFrequency),
            Mathf.Sin((t + _seedY) * Mathf.PI * 2f * posFrequency),
            Mathf.Sin((t + _seedZ) * Mathf.PI * 2f * posFrequency)
        );

        Vector3 pNoise = new Vector3(
            Mathf.PerlinNoise(_seedX, t * posFrequency) * 2f - 1f,
            Mathf.PerlinNoise(_seedY, t * posFrequency) * 2f - 1f,
            Mathf.PerlinNoise(_seedZ, t * posFrequency) * 2f - 1f
        );

        Vector3 pBlend = Vector3.Lerp(pSine, pNoise, noiseBlend);
        Vector3 posOffset = new Vector3(
            pBlend.x * posAmplitude.x,
            pBlend.y * posAmplitude.y,
            pBlend.z * posAmplitude.z
        );

        // Optional super-tiny drift
        if (driftSpeed > 0f)
            _basePos += _driftDir * driftSpeed * dt;

        // --- Rotation offset (sine + Perlin) ---
        Vector3 rSine = new Vector3(
            Mathf.Sin((t + _seedX) * Mathf.PI * 2f * rotFrequency),
            Mathf.Sin((t + _seedY) * Mathf.PI * 2f * rotFrequency),
            Mathf.Sin((t + _seedZ) * Mathf.PI * 2f * rotFrequency)
        );

        Vector3 rNoise = new Vector3(
            Mathf.PerlinNoise(_seedX + 10f, t * rotFrequency) * 2f - 1f,
            Mathf.PerlinNoise(_seedY + 10f, t * rotFrequency) * 2f - 1f,
            Mathf.PerlinNoise(_seedZ + 10f, t * rotFrequency) * 2f - 1f
        );

        Vector3 rBlend = Vector3.Lerp(rSine, rNoise, noiseBlend);
        Quaternion rotOffset = Quaternion.Euler(
            rBlend.x * rotAmplitude.x,
            rBlend.y * rotAmplitude.y,
            rBlend.z * rotAmplitude.z
        );

        // Apply
        if (worldSpace)
        {
            transform.position = _basePos + posOffset;
            transform.rotation = _baseRot * rotOffset;
        }
        else
        {
            transform.localPosition = _basePos + posOffset;
            transform.localRotation = _baseRot * rotOffset;
        }
    }

    // Call this if you move the object at runtime and want the float to re-center.
    public void RebaseToCurrent()
    {
        if (worldSpace)
        {
            _basePos = transform.position;
            _baseRot = transform.rotation;
        }
        else
        {
            _basePos = transform.localPosition;
            _baseRot = transform.localRotation;
        }
    }
}
