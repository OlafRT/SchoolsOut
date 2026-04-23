using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
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
    Coroutine rebindCo;

    void Awake()
    {
        if (!fillImage)
        {
            var child = transform.Find("Fill");
            if (child) fillImage = child.GetComponent<Image>();
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // Every time this UI is enabled (including after scene transitions), kick off a
    // delayed rebind. We can't call Rebind() synchronously here because OnEnable fires
    // in the same frame as scene load — before the Player's Awake/Start has run.
    void OnEnable()
    {
        if (rebindCo != null) StopCoroutine(rebindCo);
        rebindCo = StartCoroutine(RebindNextFrame());
    }

    // Start is intentionally removed — OnEnable + the coroutine above fully replaces
    // the old Start/LateStart pair, and this version also runs on every re-enable,
    // not just the first time.

    void OnDisable()
    {
        if (rebindCo != null) { StopCoroutine(rebindCo); rebindCo = null; }
        HookEvents(false);
        stats = null; // clear so the next Rebind() always re-finds fresh
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // The player is freshly instantiated — old stats ref is destroyed.
        // Trigger a fresh rebind one frame after load so the Player's Awake has run.
        if (!isActiveAndEnabled) return;
        if (rebindCo != null) StopCoroutine(rebindCo);
        rebindCo = StartCoroutine(RebindNextFrame());
    }

    IEnumerator RebindNextFrame()
    {
        // Wait one frame so the Player's Awake/Start has run and
        // GameSaveManager has applied the stat snapshot.
        yield return null;
        Rebind();
        RefreshImmediate();
        rebindCo = null;
    }

    /// <summary>
    /// Finds the Player, then unsubscribes from any stale ref and re-subscribes to the fresh one.
    /// Safe to call multiple times.
    /// </summary>
    void Rebind()
    {
        HookEvents(false); // unsubscribe from whatever we currently hold (may be null/stale)

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player) stats = player.GetComponent<PlayerStats>();

        HookEvents(true);
    }

    void HookEvents(bool on)
    {
        if (!stats) return;
        if (on)
        {
            stats.OnStatsChanged += HandleStatsChanged;
            stats.OnLeveledUp    += HandleLeveledUp;
        }
        else
        {
            stats.OnStatsChanged -= HandleStatsChanged;
            stats.OnLeveledUp    -= HandleLeveledUp;
        }
    }

    void HandleStatsChanged()
    {
        // Guard: stats ref could theoretically go stale between events
        if (!stats) { Rebind(); return; }
        AnimateTo(stats.currentXP, stats.xpToNext);
        UpdateTexts();
    }

    void HandleLeveledUp(int newLevel)
    {
        if (!stats) { Rebind(); return; }
        // Snap bar to 0 (new level) then pulse
        if (animCo != null) StopCoroutine(animCo);
        SetFill(0f);
        UpdateTexts();
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
        if (xpText)    xpText.text    = $"{stats.currentXP} / {stats.xpToNext}";
    }

    void RefreshImmediate()
    {
        if (!stats) return;
        float frac = SafeFrac(stats.currentXP, stats.xpToNext);
        SetFill(frac);
        UpdateTexts();
    }

    void AnimateTo(int currentXP, int xpToNext)
    {
        float target = SafeFrac(currentXP, xpToNext);
        float from   = fillImage ? fillImage.fillAmount : 0f;
        if (animCo != null) StopCoroutine(animCo);
        animCo = StartCoroutine(AnimateFill(from, target, animateSeconds));
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
}
