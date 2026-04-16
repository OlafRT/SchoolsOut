using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BossHealthBar : MonoBehaviour
{
    [Header("UI Refs — assign in prefab")]
    [Tooltip("Image component with Image Type = Filled, Fill Method = Horizontal.")]
    public Image fillBar;
    public Image fillBarFlash;          // second image on top that flashes white on hit
    public TextMeshProUGUI bossNameText;
    public TextMeshProUGUI hpText;      // optional  "850 / 1000"
    public CanvasGroup canvasGroup;

    [Header("Settings")]
    public string bossDisplayName = "Cafeteria Lady";
    public float fadeSpeed = 4f;
    public float flashDuration = 0.12f;
    public Color barColor = new Color(0.18f, 0.72f, 0.18f);
    public Color barLowColor = new Color(0.85f, 0.15f, 0.15f);
    [Tooltip("Below this fraction of HP the bar turns red.")]
    public float lowHPThreshold = 0.35f;

    NPCHealth tracked;
    int prevHP;
    bool showing;

    void Awake()
    {
        if (!canvasGroup) canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup) canvasGroup.alpha = 0f;
        if (fillBarFlash) fillBarFlash.fillAmount = 0f;
        gameObject.SetActive(false);
    }

    public void Show(NPCHealth health)
    {
        tracked = health;
        prevHP  = health ? health.currentHP : 0;
        showing = true;
        gameObject.SetActive(true);

        if (bossNameText) bossNameText.text = bossDisplayName;

        StopAllCoroutines();
        StartCoroutine(FadeTo(1f));
        Refresh();
    }

    public void Hide()
    {
        showing = false;
        StopAllCoroutines();
        StartCoroutine(FadeAndDeactivate());
    }

    void Update()
    {
        if (!showing || !tracked) return;

        // Flash on damage
        if (tracked.currentHP < prevHP)
        {
            StopCoroutine("HitFlash");
            StartCoroutine(HitFlash());
        }
        prevHP = tracked.currentHP;

        Refresh();
    }

    void Refresh()
    {
        if (!tracked) return;
        float frac = (float)tracked.currentHP / Mathf.Max(1, tracked.maxHP);
        frac = Mathf.Clamp01(frac);

        if (fillBar)
        {
            fillBar.fillAmount = frac;
            fillBar.color = Color.Lerp(barLowColor, barColor,
                Mathf.InverseLerp(0f, lowHPThreshold, frac));
        }
        if (hpText) hpText.text = $"{tracked.currentHP} / {tracked.maxHP}";
    }

    IEnumerator HitFlash()
    {
        if (!fillBarFlash) yield break;
        fillBarFlash.fillAmount = fillBar ? fillBar.fillAmount : 1f;
        float t = 0f;
        while (t < flashDuration)
        {
            t += Time.unscaledDeltaTime;
            fillBarFlash.color = new Color(1f, 1f, 1f, 1f - (t / flashDuration));
            yield return null;
        }
        fillBarFlash.fillAmount = 0f;
    }

    IEnumerator FadeTo(float target)
    {
        if (!canvasGroup) yield break;
        while (!Mathf.Approximately(canvasGroup.alpha, target))
        {
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, target, fadeSpeed * Time.deltaTime);
            yield return null;
        }
        canvasGroup.alpha = target;
    }

    IEnumerator FadeAndDeactivate()
    {
        yield return StartCoroutine(FadeTo(0f));
        gameObject.SetActive(false);
        tracked = null;
    }
}
