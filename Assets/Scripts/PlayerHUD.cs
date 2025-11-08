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
    public CanvasGroup slowPanel;   // a small banner/icon; alpha fades in/out
    public float slowFadeDuration = 0.2f;

    // cached
    Coroutine flashCo;
    Coroutine slowFadeCo;

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (deathPanel) deathPanel.SetActive(false);
        if (damageFlash) SetAlpha(damageFlash, 0f);
        if (slowPanel) slowPanel.alpha = 0f;

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

    /// <summary>External systems can tell HUD that the player is currently slowed (e.g., Bomb field).</summary>
    public static void SetSlowed(bool active)
    {
        if (!Instance || !Instance.slowPanel) return;
        if (Instance.slowFadeCo != null) Instance.StopCoroutine(Instance.slowFadeCo);
        Instance.slowFadeCo = Instance.InstanceFadeSlow(active);
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
        return StartCoroutine(FadeSlow(active));
    }

    IEnumerator FadeSlow(bool active)
    {
        float start = slowPanel.alpha;
        float end = active ? 1f : 0f;
        if (Mathf.Approximately(start, end)) yield break;

        float t = 0f;
        while (t < slowFadeDuration)
        {
            t += Time.deltaTime;
            slowPanel.alpha = Mathf.Lerp(start, end, t / slowFadeDuration);
            yield return null;
        }
        slowPanel.alpha = end;
        slowFadeCo = null;
    }

    // ---------- Utils ----------
    static void SetAlpha(Image img, float a)
    {
        var c = img.color; c.a = a; img.color = c;
    }
}
