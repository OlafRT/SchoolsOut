using UnityEngine;
using UnityEngine.UI;

/// Put this on a small GameObject in your scene and assign refs in the Inspector.
public class BreathEffectsManager : MonoBehaviour
{
    // -------- Singleton --------
    public static BreathEffectsManager Instance { get; private set; }
    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // Expose intensity/pulse for other scripts (read-only).
    public float Intensity { get; private set; }  // 0..1 (how "bad" it is)
    public float Pulse     { get; private set; }  // -1..1 (global wobble)

    [Header("Source (Bike)")]
    public BicycleController bike;           // must expose 'breath' and 'maxBreath'

    [Header("Targets")]
    public BreathDoubleVisionOverlay doubleVision; // RawImage-based overlay
    public BreathCameraWobble wobble;               // camera wobble
    public CanvasGroup vignetteGroup;               // UI vignette CanvasGroup

    [Header("Start/Full thresholds (fraction of Max Breath)")]
    [Range(0f,1f)] public float startAt = 0.30f;    // effects begin below 30%
    [Range(0f,1f)] public float fullAt  = 0.10f;    // max strength at/below 10%

    [Header("Double vision max")]
    public float dvMaxOffsetPixels = 18f;
    [Range(0f,1f)] public float dvMaxMix = 0.55f;
    public float dvBaseDriftHz = 0.35f;

    [Header("Wobble max")]
    public float wobbleMaxAmount = 1.0f;    // conceptual amplitude
    public float wobbleHz        = 1.6f;

    [Header("Vignette")]
    [Range(0f,1f)] public float vignetteMaxAlpha   = 0.85f;
    [Range(0f,1f)] public float vignettePulseAmount= 0.35f;

    [Header("Global pulse")]
    public float pulseHz = 1.2f;

    void Update()
    {
        // 1) Compute Intensity and Pulse
        if (!bike || bike.maxBreath <= 0f)
        {
            Intensity = 0f;
            Pulse = 0f;
            return;
        }

        float frac = Mathf.Clamp01(bike.breath / bike.maxBreath);  // 1 = safe, 0 = out
        // Map frac ∈ [startAt..fullAt]  -> Intensity ∈ [0..1]
        Intensity = Mathf.Clamp01(Mathf.InverseLerp(startAt, fullAt, frac));
        Pulse = Mathf.Sin(Time.time * Mathf.PI * 2f * Mathf.Max(0.01f, pulseHz));

        // 2) Drive Double Vision
        if (doubleVision)
        {
            doubleVision.SetStrength(
                offsetPixels: dvMaxOffsetPixels * Intensity,
                mix:          dvMaxMix          * Intensity,
                driftHz:      dvBaseDriftHz * Mathf.Lerp(0.7f, 1.4f, (Pulse + 1f) * 0.5f)
            );
        }

        // 3) Drive Camera Wobble
        if (wobble)
        {
            wobble.SetStrength(Intensity * wobbleMaxAmount, wobbleHz);
        }

        // 4) Drive Vignette
        if (vignetteGroup)
        {
            float baseA = vignetteMaxAlpha * Intensity;
            float pulsed = baseA + vignettePulseAmount * Intensity * (Pulse * 0.5f);
            vignetteGroup.alpha = Mathf.Clamp01(pulsed);
        }
    }
}
