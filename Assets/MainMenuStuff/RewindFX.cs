using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using TMPro;

[DisallowMultipleComponent]
public class RewindFX : MonoBehaviour
{
    [Header("URP Global Volume (with CA, Grain, Vignette, Lens Distortion)")]
    [SerializeField] Volume volume;
    ChromaticAberration _ca; FilmGrain _grain; Vignette _vig; LensDistortion _dist;

    [Header("Overlay (optional)")]
    [SerializeField] CanvasGroup overlayGroup;
    [SerializeField] TextMeshProUGUI overlayLabel;

    [Header("Audio")]
    [SerializeField] AudioSource sfx;
    [SerializeField] AudioClip rewindClip;

    [Tooltip("Master volume for this effect (0..1).")]
    [SerializeField, Range(0f, 1f)] float baseVolume = 1f;

    [Tooltip("± pitch variation (e.g., 0.05 = ±5%).")]
    [SerializeField, Range(0f, 0.2f)] float pitchJitter = 0.05f;

    float _basePitch = 1f;

    [Header("Timing & Intensity")]
    [SerializeField] float fadeIn  = 0.15f;
    [SerializeField] float hold    = 0.80f;   // used by Play()
    [SerializeField] float fadeOut = 0.25f;
    [SerializeField] AnimationCurve curve = null; // 0..1 shaping

    [SerializeField] float caMax   = 0.70f;
    [SerializeField] float grainMax= 1.00f;
    [SerializeField] float vigMax  = 0.45f;
    [SerializeField] float distMax = -0.25f;

    Coroutine _co;
    bool _active; // true between Begin() and End()

    void Awake()
    {
        if (volume && volume.profile) {
            volume.profile.TryGet(out _ca);
            volume.profile.TryGet(out _grain);
            volume.profile.TryGet(out _vig);
            volume.profile.TryGet(out _dist);
        }
        if (overlayGroup) overlayGroup.alpha = 0f;
        if (overlayLabel) overlayLabel.enabled = false;
        SetK(0f);

        if (sfx) _basePitch = sfx.pitch;
        if (curve == null) curve = AnimationCurve.EaseInOut(0,0,1,1);
    }

    void OnValidate()
    {
        baseVolume = Mathf.Clamp01(baseVolume);
    }

    // -------- Timed one-shot --------
    public void Play()
    {
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(PlayTimedCo());
    }
    IEnumerator PlayTimedCo()
    {
        yield return BeginCo();
        yield return WaitRealtime(hold);
        yield return EndCo();
        _co = null;
    }

    // -------- Event-driven --------
    public void Begin()
    {
        if (_active) return;                 // guard double-starts
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(BeginCo());
    }
    public void End()
    {
        if (!_active) return;                // guard if already ended
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(EndCo());
    }

    IEnumerator BeginCo()
    {
        _active = true;

        if (sfx && rewindClip)
        {
            sfx.loop = false;
            sfx.clip = rewindClip;

            // slight random pitch each play
            float jitter = Random.Range(-pitchJitter, pitchJitter);
            sfx.pitch = Mathf.Clamp(_basePitch * (1f + jitter), 0.5f, 2f);

            sfx.volume = baseVolume; // << use script-controlled volume
            sfx.Play();
        }

        if (overlayLabel) overlayLabel.enabled = true;

        float t = 0f;
        while (t < fadeIn)
        {
            t += Time.unscaledDeltaTime;
            float k = curve.Evaluate(Mathf.Clamp01(t / Mathf.Max(0.0001f, fadeIn)));
            SetK(k);
            if (overlayGroup) overlayGroup.alpha = k;
            yield return null;
        }

        SetK(1f);
        if (overlayGroup) overlayGroup.alpha = 1f;
        _co = null;
    }

    IEnumerator EndCo()
    {
        float startVol = (sfx ? sfx.volume : 0f);

        float t = 0f;
        while (t < fadeOut)
        {
            t += Time.unscaledDeltaTime;
            float k = 1f - curve.Evaluate(Mathf.Clamp01(t / Mathf.Max(0.0001f, fadeOut)));
            SetK(k);
            if (overlayGroup) overlayGroup.alpha = k;

            // fade audio down smoothly from whatever it's at to 0
            if (sfx && sfx.isPlaying)
                sfx.volume = Mathf.Lerp(0f, startVol, k);

            yield return null;
        }

        SetK(0f);
        if (overlayGroup) overlayGroup.alpha = 0f;
        if (overlayLabel) overlayLabel.enabled = false;

        if (sfx)
        {
            sfx.Stop();
            sfx.volume = baseVolume; // reset to your chosen base volume
            sfx.pitch  = _basePitch;
        }

        _active = false;
        _co = null;
    }

    void SetK(float k)
    {
        if (_ca)   { _ca.intensity.value    = caMax    * k; _ca.active    = k > 0f; }
        if (_grain){ _grain.intensity.value = grainMax * k; _grain.active = k > 0f; }
        if (_vig)  { _vig.intensity.value   = vigMax   * k; _vig.active   = k > 0f; }
        if (_dist) { _dist.intensity.value  = distMax  * k; _dist.active  = k > 0f; }
    }

    static IEnumerator WaitRealtime(float seconds)
    {
        float end = Time.unscaledTime + seconds;
        while (Time.unscaledTime < end) yield return null;
    }

    // Optional helper if you want to change at runtime from other scripts
    public void SetBaseVolume(float v) => baseVolume = Mathf.Clamp01(v);
    public float GetBaseVolume() => baseVolume;
}
