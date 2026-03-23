using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
public class PlayerDeathSequence : MonoBehaviour
{
    [Header("Animator")]
    public string deathBoolName = "IsDead";
    public int animatorLayer = 0;
    public string deathStateTag = "Death";
    public float fallbackDeathTime = 1.2f;

    [Header("Panel Fade")]
    [Tooltip("Unscaled seconds after death trigger before the panel starts fading in. " +
             "Set this to overlap with the animation — e.g. 0.4 starts the fade early in the death clip.")]
    public float panelFadeDelay = 0.4f;
    public float fadeDuration = 1.0f;
    [Tooltip("A child GameObject inside the Canvas that should stay visible (e.g. your death panel). Its parent Canvas and all of its siblings will be protected.")]
    public GameObject excludeFromHide;

    [Header("Slow Motion")]
    [Tooltip("How slow time gets at full effect (0.1 = 10% speed, GTA-style)")]
    public float targetTimeScale = 0.15f;
    [Tooltip("Unscaled seconds to reach full slow-mo")]
    public float slowMoRampDuration = 0.4f;

    [Header("Post Processing")]
    [Tooltip("The scene Volume that holds your Color Adjustments override")]
    public Volume postProcessVolume;
    [Tooltip("Your normal saturation value")]
    public float normalSaturation = 7f;
    [Tooltip("Target saturation on death (GTA IV-style drain)")]
    public float deathSaturation = -100f;
    [Tooltip("Unscaled seconds to drain to full desaturation")]
    public float desaturationDuration = 1.8f;

    [Header("Death Audio")]
    [Tooltip("One-shot sound played the moment the player dies")]
    public AudioClip deathSound;
    [Range(0f, 1f)] public float deathSoundVolume = 1f;
    [Tooltip("Seconds to fade out zone music on death (uses AreaZoneMusicController)")]
    public float musicFadeOutDuration = 1.5f;

    PlayerHealth health;
    PlayerBootstrap bootstrap;
    Animator anim;
    ColorAdjustments colorAdjustments;
    AudioSource deathAudioSource;

    // Sibling GameObjects hidden on death so only the death panel remains
    GameObject[] hiddenObjects;

    void Awake()
    {
        health    = GetComponent<PlayerHealth>();
        bootstrap = GetComponent<PlayerBootstrap>();
        anim      = bootstrap ? bootstrap.ActiveAnimator : GetComponentInChildren<Animator>(true);

        if (health) health.OnDied += HandleDied;

        // Cache the ColorAdjustments override once at startup
        if (postProcessVolume && postProcessVolume.profile.TryGet(out ColorAdjustments ca))
            colorAdjustments = ca;

        // Dedicated 2D source for the death sting
        deathAudioSource = gameObject.AddComponent<AudioSource>();
        deathAudioSource.spatialBlend = 0f;
        deathAudioSource.playOnAwake  = false;
    }

    void OnDestroy()
    {
        if (health) health.OnDied -= HandleDied;
    }

    void HandleDied()
    {
        if (!anim) anim = bootstrap ? bootstrap.ActiveAnimator : GetComponentInChildren<Animator>(true);
        if (anim && !string.IsNullOrEmpty(deathBoolName))
            anim.SetBool(deathBoolName, true);

        // Mute zone music and play death sting
        AreaZoneMusicController.Instance?.CrossfadeTo(null, 0f, false, musicFadeOutDuration, musicFadeOutDuration);
        if (deathSound && deathAudioSource)
            deathAudioSource.PlayOneShot(deathSound, deathSoundVolume);

        // Kick off all three effects in parallel
        HideOtherUI();
        StartCoroutine(SlowMoRamp());
        StartCoroutine(DesaturateRamp());
        StartCoroutine(Sequence());
    }

    // ─────────────────────────────────────────────────────────────────
    //  Slow Motion
    // ─────────────────────────────────────────────────────────────────

    IEnumerator SlowMoRamp()
    {
        float startScale = Time.timeScale;
        float t = 0f;

        while (t < slowMoRampDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / slowMoRampDuration);
            Time.timeScale = Mathf.Lerp(startScale, targetTimeScale, k);
            // Keep fixedDeltaTime in sync so physics doesn't stutter
            Time.fixedDeltaTime = 0.02f * Time.timeScale;
            yield return null;
        }

        Time.timeScale = targetTimeScale;
        Time.fixedDeltaTime = 0.02f * targetTimeScale;
    }

    // ─────────────────────────────────────────────────────────────────
    //  Desaturation
    // ─────────────────────────────────────────────────────────────────

    IEnumerator DesaturateRamp()
    {
        if (colorAdjustments == null) yield break;

        float startSat = colorAdjustments.saturation.value;
        float t = 0f;

        while (t < desaturationDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / desaturationDuration);
            colorAdjustments.saturation.value = Mathf.Lerp(startSat, deathSaturation, k);
            yield return null;
        }

        colorAdjustments.saturation.value = deathSaturation;
    }

    // ─────────────────────────────────────────────────────────────────
    //  Main Death Sequence  (launches two parallel coroutines)
    // ─────────────────────────────────────────────────────────────────

    IEnumerator Sequence()
    {
        StartCoroutine(WaitForAnimation());
        StartCoroutine(FadePanel());
        yield break;
    }

    // Waits out the death animation in scaled time (so slow-mo is respected).
    // Nothing visible depends on this finishing — it's a hook for anything
    // you want to trigger *after* the animation completes (e.g. hide body).
    IEnumerator WaitForAnimation()
    {
        if (!anim) yield break;

        // Brief settle so the Animator has time to transition
        for (float t = 0; t < 0.15f; t += Time.unscaledDeltaTime) yield return null;

        float safety = 3f;
        while (safety > 0f)
        {
            var st = anim.GetCurrentAnimatorStateInfo(animatorLayer);
            if (st.IsTag(deathStateTag))
            {
                // Ride scaled time — slow-mo stretches this naturally
                float target = Mathf.Max(0.1f, st.length * 0.95f);
                float waited = 0f;
                while (waited < target)
                {
                    waited += Time.deltaTime;
                    yield return null;
                }
                yield break;
            }
            safety -= Time.unscaledDeltaTime;
            yield return null;
        }

        // Fallback: no tagged state found
        float fb = 0f;
        while (fb < fallbackDeathTime) { fb += Time.unscaledDeltaTime; yield return null; }
    }

    // Waits panelFadeDelay unscaled seconds, then fades the panel in.
    // Runs in parallel with WaitForAnimation — tweak panelFadeDelay to
    // control exactly where in the animation the text appears.
    IEnumerator FadePanel()
    {
        // Wait the configured real-time delay (unscaled, so slow-mo doesn't push it out)
        if (panelFadeDelay > 0f)
            yield return new WaitForSecondsRealtime(panelFadeDelay);

        var hud = PlayerHUD.Instance;
        if (hud == null || hud.deathPanel == null) yield break;

        var panelGO = hud.deathPanel;

        var cv = panelGO.GetComponent<Canvas>();
        if (!cv) cv = panelGO.AddComponent<Canvas>();
        cv.overrideSorting = true;
        cv.sortingOrder = short.MaxValue - 1;

        if (!panelGO.GetComponent<GraphicRaycaster>())
            panelGO.AddComponent<GraphicRaycaster>();

        var cg = panelGO.GetComponent<CanvasGroup>();
        if (!cg) cg = panelGO.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.blocksRaycasts = true;
        cg.interactable = true;

        panelGO.SetActive(true);
        panelGO.transform.SetAsLastSibling();

        yield return null;  // one frame for Canvas/CG to register

        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Clamp01(t / Mathf.Max(0.0001f, fadeDuration));
            yield return null;
        }
        cg.alpha = 1f;
    }

    // ─────────────────────────────────────────────────────────────────
    //  UI Isolation
    // ─────────────────────────────────────────────────────────────────

    void HideOtherUI()
    {
        if (!excludeFromHide) return;

        Transform parent = excludeFromHide.transform.parent;
        if (!parent) return;

        var toHide = new System.Collections.Generic.List<GameObject>();

        foreach (Transform child in parent)
        {
            if (child.gameObject == excludeFromHide) continue;
            if (!child.gameObject.activeSelf) continue;

            child.gameObject.SetActive(false);
            toHide.Add(child.gameObject);
        }

        hiddenObjects = toHide.ToArray();
    }

    // ─────────────────────────────────────────────────────────────────
    //  Call this from your respawn / scene-reload logic
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Restores Time.timeScale and post-processing saturation to their
    /// normal values. Call this before reloading the scene or respawning.
    /// </summary>
    public void ResetEffects()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        if (colorAdjustments != null)
            colorAdjustments.saturation.value = normalSaturation;

        // Resume zone music for the current zone
        AreaZoneMusicController.Instance?.ResumeForZone(AreaZoneService.Instance?.CurrentZone);

        // Restore any UI we hid during the death sequence
        if (hiddenObjects != null)
        {
            foreach (var go in hiddenObjects)
                if (go) go.SetActive(true);
            hiddenObjects = null;
        }
    }
}