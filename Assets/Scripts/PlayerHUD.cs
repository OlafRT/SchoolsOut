using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

[DisallowMultipleComponent]
public class PlayerHUD : MonoBehaviour
{
    public static PlayerHUD Instance { get; private set; }

    [Header("Health UI")]
    public Image healthFill;      // set to a Fill (Horizontal) image
    public TMP_Text healthLabel;  // "52 / 100"

    [Header("Damage Flash")]
    public Image damageFlash;       // full-screen or bar overlay image (red, alpha 0)
    public float flashFadeDuration = 0.25f;

    [Header("Death Panel")]
    public GameObject deathPanel;   // panel to enable on death

    [Header("Slow Panel")]
    public CanvasGroup slowPanel;
    public float slowFadeDuration = 0.2f;

    [Header("Mind Control Panel")]
    public CanvasGroup mindControlPanel;
    public float mindControlFadeDuration = 0.2f;

    // cached
    Coroutine flashCo;
    Coroutine slowFadeCo;
    Coroutine mindControlFadeCo;

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (deathPanel) deathPanel.SetActive(false);
        if (damageFlash) SetAlpha(damageFlash, 0f);
        if (slowPanel) slowPanel.alpha = 0f;
        if (mindControlPanel) mindControlPanel.alpha = 0f;

        // Auto-subscribe if a PlayerHealth exists already
        var ph = FindObjectOfType<PlayerHealth>();
        if (ph)
        {
            ph.OnDamaged += (_, __) => TryFlashDamage();
            //ph.OnDied    += TryShowDeathPanel;
        }
    }

    // ---------- Static helpers (so gameplay code can be ignorant of locating HUD) ----------

    public static void TryUpdateHealth(int hp, int max)
    {
        if (!Instance) return;
        float t = (max <= 0) ? 0f : Mathf.Clamp01((float)hp / max);
        if (Instance.healthFill) Instance.healthFill.fillAmount = t;
        if (Instance.healthLabel) Instance.healthLabel.text = $"{hp} / {max}";
    }

    public static void TryFlashDamage()
    {
        if (!Instance || !Instance.damageFlash) return;
        if (Instance.flashCo != null) Instance.StopCoroutine(Instance.flashCo);
        Instance.flashCo = Instance.StartCoroutine(Instance.DoFlash());
    }

    public static void TryShowDeathPanel()
    {
        if (!Instance) return;
        if (Instance.deathPanel) Instance.deathPanel.SetActive(true);
    }

    /// <summary>External systems can tell HUD that the player is currently slowed.</summary>
    public static void SetSlowed(bool active)
    {
        if (!Instance || !Instance.slowPanel) return;
        if (Instance.slowFadeCo != null) Instance.StopCoroutine(Instance.slowFadeCo);
        Instance.slowFadeCo = Instance.InstanceFadeSlow(active);
    }

    /// <summary>Called by MindControlEffect when the player is mind-controlled by the principal.</summary>
    public static void SetMindControlled(bool active)
    {
        if (!Instance || !Instance.mindControlPanel) return;
        if (Instance.mindControlFadeCo != null) Instance.StopCoroutine(Instance.mindControlFadeCo);
        Instance.mindControlFadeCo = Instance.StartCoroutine(Instance.FadePanel(Instance.mindControlPanel, active, Instance.mindControlFadeDuration));
    }

    // ---------- Coroutines ----------

    IEnumerator DoFlash()
    {
        // quick up, then fade out
        SetAlpha(damageFlash, 0.6f);
        float t = 0f;
        while (t < flashFadeDuration)
        {
            t += Time.deltaTime;
            float a = Mathf.Lerp(0.6f, 0f, t / flashFadeDuration);
            SetAlpha(damageFlash, a);
            yield return null;
        }
        SetAlpha(damageFlash, 0f);
        flashCo = null;
    }

    Coroutine InstanceFadeSlow(bool active)
    {
        return StartCoroutine(FadePanel(slowPanel, active, slowFadeDuration));
    }

    IEnumerator FadePanel(CanvasGroup panel, bool active, float duration)
    {
        float start = panel.alpha;
        float end   = active ? 1f : 0f;
        if (Mathf.Approximately(start, end)) yield break;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            panel.alpha = Mathf.Lerp(start, end, t / duration);
            yield return null;
        }
        panel.alpha = end;
    }

    // kept for compatibility — FadeSlow was previously its own coroutine
    IEnumerator FadeSlow(bool active)
    {
        yield return FadePanel(slowPanel, active, slowFadeDuration);
        slowFadeCo = null;
    }

    // ---------- Utils ----------
    static void SetAlpha(Image img, float a)
    {
        var c = img.color; c.a = a; img.color = c;
    }
}
