using UnityEngine;

/// Controls lifetime + optional UV scroll and fade for all child LineRenderers.
/// Attach to the prefab ROOT.
public class LightningLine : MonoBehaviour
{
    [Header("Lifetime")]
    public float lifetime = 0.18f;
    public AnimationCurve alphaOverLife = AnimationCurve.EaseInOut(0, 1, 1, 0);

    [Header("Optional UV Scroll (needs textured additive material set to Tile)")]
    public float uvScrollSpeedCore = 8f;
    public float uvScrollSpeedGlow = 6f;

    LineRenderer[] childLines;
    float t;

    void Awake()
    {
        childLines = GetComponentsInChildren<LineRenderer>(true);
    }

    void Update()
    {
        t += Time.deltaTime;
        float u = Mathf.Clamp01(t / Mathf.Max(0.0001f, lifetime));
        float a = alphaOverLife.Evaluate(u);

        // fade all lines
        foreach (var lr in childLines)
        {
            if (!lr) continue;
            var g = lr.colorGradient;
            var alphas = g.alphaKeys;
            for (int i = 0; i < alphas.Length; i++) alphas[i].alpha = a;
            g.SetKeys(g.colorKeys, alphas);
            lr.colorGradient = g;

            // scroll UV if the material supports it
            var mat = lr.material; // instance, not shared
            if (mat && mat.HasProperty("_MainTex"))
            {
                var off = mat.GetTextureOffset("_MainTex");
                float spd = (lr.gameObject.name.ToLower().Contains("glow")) ? uvScrollSpeedGlow : uvScrollSpeedCore;
                off.x += spd * Time.deltaTime;
                mat.SetTextureOffset("_MainTex", off);
            }
        }

        if (u >= 1f) Destroy(gameObject);
    }
}
