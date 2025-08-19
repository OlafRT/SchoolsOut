using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

[DisallowMultipleComponent]
public class XPBarUI : MonoBehaviour
{
    [Header("Wiring")]
    public PlayerStats stats;            // drag Player (with PlayerStats) here, or leave empty to auto-find by tag "Player"
    public Image fillImage;              // assign the child "Fill" Image (type: Filled, Horizontal)
    public TextMeshProUGUI levelText;    // optional
    public TextMeshProUGUI xpText;       // optional

    [Header("Animation")]
    [Tooltip("Seconds to animate the fill when XP changes.")]
    public float animateSeconds = 0.35f;
    [Tooltip("Brief scale pulse on level up.")]
    public float levelUpPulseScale = 1.15f;
    public float levelUpPulseTime = 0.20f;

    // internal
    Coroutine animCo;
    int lastLevel = -1;
    int lastXP = -1;
    int lastXpToNext = -1;

    void Awake()
    {
        if (!stats)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player) stats = player.GetComponent<PlayerStats>();
        }

        if (!fillImage)
        {
            var child = transform.Find("Fill");
            if (child) fillImage = child.GetComponent<Image>();
        }
    }

    void OnEnable()
    {
        HookEvents(true);
        RefreshImmediate();
    }

    void OnDisable()
    {
        HookEvents(false);
    }

    void HookEvents(bool on)
    {
        if (!stats) return;
        if (on)
        {
            stats.OnStatsChanged += HandleStatsChanged;
            stats.OnLeveledUp += HandleLeveledUp;
        }
        else
        {
            stats.OnStatsChanged -= HandleStatsChanged;
            stats.OnLeveledUp -= HandleLeveledUp;
        }
    }

    void HandleStatsChanged()
    {
        // Smoothly animate to current fraction
        AnimateTo(stats.currentXP, stats.xpToNext);
        UpdateTexts();
    }

    void HandleLeveledUp(int newLevel)
    {
        // Snap bar to 0 (new level) + pulse
        if (animCo != null) StopCoroutine(animCo);
        SetFill(0f);
        UpdateTexts();

        // Tiny pulse
        StartCoroutine(LevelPulse());
    }

    IEnumerator LevelPulse()
    {
        Vector3 baseScale = transform.localScale;
        Vector3 target = baseScale * Mathf.Max(1.0f, levelUpPulseScale);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.01f, levelUpPulseTime);
            transform.localScale = Vector3.Lerp(baseScale, target, t);
            yield return null;
        }
        t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.01f, levelUpPulseTime);
            transform.localScale = Vector3.Lerp(target, baseScale, t);
            yield return null;
        }
        transform.localScale = baseScale;
    }

    void UpdateTexts()
    {
        if (!stats) return;

        if (levelText) levelText.text = $"Lv {stats.level}";
        if (xpText) xpText.text = $"{stats.currentXP} / {stats.xpToNext}";
    }

    void RefreshImmediate()
    {
        if (!stats) return;
        float frac = SafeFrac(stats.currentXP, stats.xpToNext);
        SetFill(frac);
        UpdateTexts();
        Cache();
    }

    void AnimateTo(int currentXP, int xpToNext)
    {
        float target = SafeFrac(currentXP, xpToNext);

        // Start from whatever is currently displayed
        float from = fillImage ? fillImage.fillAmount : 0f;

        if (animCo != null) StopCoroutine(animCo);
        animCo = StartCoroutine(AnimateFill(from, target, animateSeconds));
        Cache();
    }

    IEnumerator AnimateFill(float from, float to, float dur)
    {
        if (!fillImage) yield break;
        dur = Mathf.Max(0.01f, dur);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / dur;
            fillImage.fillAmount = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        fillImage.fillAmount = to;
    }

    void SetFill(float value)
    {
        if (fillImage) fillImage.fillAmount = Mathf.Clamp01(value);
    }

    float SafeFrac(int cur, int max) => (max <= 0) ? 0f : Mathf.Clamp01((float)cur / (float)max);

    void Cache()
    {
        if (!stats) return;
        lastLevel = stats.level;
        lastXP = stats.currentXP;
        lastXpToNext = stats.xpToNext;
    }
}