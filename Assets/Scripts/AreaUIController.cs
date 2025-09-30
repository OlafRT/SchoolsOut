using UnityEngine;
using TMPro;
using System.Collections;

public class AreaUIController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] TMP_Text minimapLabel;        // the small label on your minimap UI
    [SerializeField] TMP_Text bannerLabel;         // big centered banner text

    [Header("Banner Timing")]
    [SerializeField] float fadeIn = 0.25f;
    [SerializeField] float hold   = 1.25f;
    [SerializeField] float fadeOut= 0.6f;

    [Header("Styling")]
    [SerializeField] string emptyZoneFallback = "";
    [SerializeField] bool   uppercaseBanner = true;

    Coroutine bannerCo;

    void OnEnable()
    {
        if (AreaZoneService.Instance)
            AreaZoneService.Instance.OnZoneChanged += HandleZoneChanged;
    }

    void OnDisable()
    {
        if (AreaZoneService.Instance)
            AreaZoneService.Instance.OnZoneChanged -= HandleZoneChanged;
    }

    void Start()
    {
        // Initialize hidden banner
        if (bannerLabel)
        {
            var c = bannerLabel.color; c.a = 0f; bannerLabel.color = c;
            bannerLabel.gameObject.SetActive(false);
        }

        // Initialize labels immediately from current service state or fallback
        string startName = AreaZoneService.Instance ? AreaZoneService.Instance.CurrentZoneName : string.Empty;
        if (string.IsNullOrWhiteSpace(startName)) startName = emptyZoneFallback;

        if (minimapLabel) minimapLabel.text = startName;

        // Optionally show the banner on scene start too (comment out if not desired)
        if (!string.IsNullOrWhiteSpace(startName))
            HandleZoneChanged(startName);
    }

    void HandleZoneChanged(string zoneName)
    {
        string label = string.IsNullOrWhiteSpace(zoneName) ? emptyZoneFallback : zoneName;

        if (minimapLabel) minimapLabel.text = label;

        if (!bannerLabel) return;
        if (bannerCo != null) StopCoroutine(bannerCo);
        bannerCo = StartCoroutine(BannerRoutine(label));
    }

    IEnumerator BannerRoutine(string text)
    {
        bannerLabel.gameObject.SetActive(true);
        bannerLabel.text = uppercaseBanner ? text.ToUpperInvariant() : text;

        yield return FadeTo(1f, fadeIn);
        yield return new WaitForSeconds(hold);
        yield return FadeTo(0f, fadeOut);

        bannerLabel.gameObject.SetActive(false);
        bannerCo = null;
    }

    IEnumerator FadeTo(float target, float duration)
    {
        duration = Mathf.Max(0.01f, duration);
        Color c = bannerLabel.color;
        float start = c.a, t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            c.a = Mathf.Lerp(start, target, t / duration);
            bannerLabel.color = c;
            yield return null;
        }
        c.a = target;
        bannerLabel.color = c;
    }
}
