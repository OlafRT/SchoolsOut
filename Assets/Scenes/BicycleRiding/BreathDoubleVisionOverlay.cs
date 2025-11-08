using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class BreathDoubleVisionOverlay : MonoBehaviour
{
    [Tooltip("Assign the RawImage that shows the FX RenderTexture.")]
    public RawImage overlay;

    RectTransform rect;
    float phase;

    // Values driven by the manager:
    float offsetPixels;     // how far to shift the ghost image
    float ghostMix;         // alpha of the ghost image (0..1)
    float driftHz;          // circular drift speed

    void Awake()
    {
        if (!overlay) overlay = GetComponent<RawImage>();
        rect = overlay ? overlay.rectTransform : null;
    }

    void LateUpdate()
    {
        if (!overlay || !rect) return;

        // Alpha
        var c = overlay.color;
        c.a = Mathf.Clamp01(ghostMix);
        overlay.color = c;

        // Circular drift
        phase += Time.deltaTime * Mathf.PI * 2f * Mathf.Max(0.001f, driftHz);

        // Slightly elliptical path so it feels organic
        float px = Mathf.Sin(phase) * offsetPixels;
        float py = Mathf.Cos(phase * 0.8f) * offsetPixels * 0.6f;

        rect.anchoredPosition = new Vector2(px, py);
    }

    /// Called by BreathEffectsManager every Update.
    public void SetStrength(float offsetPixels, float mix, float driftHz)
    {
        this.offsetPixels = Mathf.Max(0f, offsetPixels);
        this.ghostMix     = Mathf.Clamp01(mix);
        this.driftHz      = Mathf.Max(0f, driftHz);
    }
}
