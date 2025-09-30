using UnityEngine;
using System.Collections;

[DisallowMultipleComponent]
public class AreaZoneMusicController : MonoBehaviour
{
    public static AreaZoneMusicController Instance { get; private set; }

    [Header("Defaults (used when no zone music)")]
    public AudioClip defaultClip;
    [Range(0f,1f)] public float defaultVolume = 0.6f;
    public bool defaultLoop = true;
    public float defaultFadeIn  = 1.0f;
    public float defaultFadeOut = 1.0f;

    [Header("General")]
    [Range(0f,1f)] public float masterVolume = 1f;   // UI slider can drive this (0..1)

    // --- NEW: Mute control ---
    [Header("Mute")]
    public bool startMuted = false;
    public float muteFadeSeconds = 0.15f;

    AudioSource a, b;
    AudioSource active;   // currently audible
    Coroutine xfadeCo;

    // scalar applied to both sources: totalScale = masterVolume * muteScalar
    float muteScalar = 1f;
    float lastTotalScale = 1f;
    Coroutine muteCo;

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        a = CreateSource("Music_A");
        b = CreateSource("Music_B");
        active = a;

        // initial scale (handles startMuted)
        muteScalar = startMuted ? 0f : 1f;
        lastTotalScale = Mathf.Max(0.0001f, masterVolume * muteScalar);

        if (AreaZoneService.Instance)
            AreaZoneService.Instance.OnZoneChangedZone += HandleZoneChanged;
    }

    void OnDestroy()
    {
        if (AreaZoneService.Instance)
            AreaZoneService.Instance.OnZoneChangedZone -= HandleZoneChanged;
    }

    AudioSource CreateSource(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f; // 2D music
        src.loop = true;
        src.volume = 0f;
        return src;
    }

    void HandleZoneChanged(ZoneVolume zone)
    {
        // Determine target clip/params
        AudioClip clip;
        float vol;
        bool loop;
        float fadeIn, fadeOut;

        if (zone && zone.music)
        {
            clip   = zone.music;
            vol    = Mathf.Clamp01(zone.musicVolume);
            loop   = zone.musicLoop;
            fadeIn = Mathf.Max(0f, zone.musicFadeIn);
            fadeOut= Mathf.Max(0f, zone.musicFadeOut);
        }
        else
        {
            clip   = defaultClip;
            vol    = defaultVolume;
            loop   = defaultLoop;
            fadeIn = defaultFadeIn;
            fadeOut= defaultFadeOut;
        }

        CrossfadeTo(clip, vol, loop, fadeIn, fadeOut);
    }

    // targetVol here is the per-track base volume (0..1) BEFORE master/mute scaling
    public void CrossfadeTo(AudioClip clip, float targetVol, bool loop, float fadeIn, float fadeOut)
    {
        if (xfadeCo != null) StopCoroutine(xfadeCo);
        xfadeCo = StartCoroutine(CrossfadeRoutine(clip, targetVol, loop, fadeIn, fadeOut));
    }

    IEnumerator CrossfadeRoutine(AudioClip clip, float targetVol, bool loop, float fadeIn, float fadeOut)
    {
        AudioSource from = active;
        AudioSource to   = (active == a) ? b : a;
        active = to;

        // Prepare "to"
        to.Stop();
        to.clip = clip;
        to.loop = loop;
        to.volume = 0f;

        if (clip) to.Play();

        float t   = 0f;
        float fin = Mathf.Max(0.01f, fadeIn);
        float fout= Mathf.Max(0.01f, fadeOut);
        float dur = Mathf.Max(fin, fout);

        while (t < dur)
        {
            t += Time.unscaledDeltaTime; // music ignores timescale changes
            float nin = Mathf.Clamp01(t / fin);
            float nout= Mathf.Clamp01(t / fout);

            float scale = masterVolume * muteScalar;

            if (clip)  to.volume   = targetVol * Mathf.Lerp(0f, scale, nin);
            if (from)  from.volume = (from.clip ? 1f : 0f) * Mathf.Lerp(from.volume, 0f, nout); // fade whatever is left

            yield return null;
        }

        if (from) { from.volume = 0f; from.Stop(); }
        if (clip) to.volume = targetVol * (masterVolume * muteScalar);

        xfadeCo = null;
    }

    // ------------------ PUBLIC UI METHODS ------------------

    /// <summary>Toggle mute (fade), ideal for a Button OnClick.</summary>
    public void ToggleMute()
    {
        SetMuted(!(muteScalar > 0.5f));
    }

    /// <summary>Mute/unmute with a smooth fade.</summary>
    public void SetMuted(bool muted)
    {
        if (muteCo != null) StopCoroutine(muteCo);
        muteCo = StartCoroutine(MuteRoutine(muted ? 0f : 1f, Mathf.Max(0f, muteFadeSeconds)));
    }

    IEnumerator MuteRoutine(float targetScalar, float duration)
    {
        float start = muteScalar;
        float t = 0f;
        duration = Mathf.Max(0.001f, duration);

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            muteScalar = Mathf.Lerp(start, targetScalar, t / duration);
            ApplyScaleImmediate();
            yield return null;
        }

        muteScalar = targetScalar;
        ApplyScaleImmediate();
        muteCo = null;
    }

    /// <summary>Set overall music volume (0..1). Perfect for a UI slider.</summary>
    public void SetMasterVolume(float v)
    {
        v = Mathf.Clamp01(v);
        if (Mathf.Approximately(v, masterVolume)) return;
        masterVolume = v;
        ApplyScaleImmediate();
    }

    // -------------------------------------------------------

    void ApplyScaleImmediate()
    {
        float newScale = Mathf.Max(0f, masterVolume * muteScalar);
        float ratio = (lastTotalScale <= 0.0001f) ? newScale : newScale / lastTotalScale;

        if (a) a.volume *= ratio;
        if (b) b.volume *= ratio;

        lastTotalScale = Mathf.Max(0.0001f, newScale);
    }
}
