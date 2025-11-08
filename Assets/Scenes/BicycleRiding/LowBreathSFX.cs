using System.Collections;
using UnityEngine;

public class LowBreathSFX : MonoBehaviour
{
    [Header("Refs")]
    public BicycleController bike;

    [Header("Audio (Unity AudioSource)")]
    public AudioSource loopSource;   // assign a looping heavy-breathing clip (loop = true)
    public AudioClip gaspEnter;      // optional one-shot when entering low state
    public float loopVolume = 1f;    // target volume for the loop

    [Header("Thresholds (fraction of max)")]
    [Range(0f,1f)] public float enterAt = 0.20f; // start SFX at or below 20%
    [Range(0f,1f)] public float exitAt  = 0.27f; // stop SFX when back above 27%

    [Header("Fade")]
    public float fadeIn  = 0.15f;
    public float fadeOut = 0.25f;

    bool isLow;
    Coroutine fadeCo;

    void Update()
    {
        if (!bike || bike.maxBreath <= 0f) return;

        float frac = bike.breath / bike.maxBreath;

        if (!isLow && frac <= enterAt)
            EnterLow();
        else if (isLow && frac >= exitAt)
            ExitLow();
    }

    void EnterLow()
    {
        isLow = true;

        if (gaspEnter && loopSource)
            loopSource.PlayOneShot(gaspEnter);

        if (loopSource)
        {
            loopSource.loop = true;
            if (!loopSource.isPlaying) loopSource.Play();
            StartFade(loopSource, loopSource.volume, loopVolume, fadeIn);
        }
    }

    void ExitLow()
    {
        isLow = false;
        if (loopSource)
            StartFade(loopSource, loopSource.volume, 0f, fadeOut, stopWhenDone:true);
    }

    void OnDisable()
    {
        if (loopSource)
        {
            loopSource.Stop();
            loopSource.volume = loopVolume;
        }
        isLow = false;
    }

    void StartFade(AudioSource src, float from, float to, float time, bool stopWhenDone=false)
    {
        if (fadeCo != null) StopCoroutine(fadeCo);
        fadeCo = StartCoroutine(FadeCo(src, from, to, time, stopWhenDone));
    }

    IEnumerator FadeCo(AudioSource src, float from, float to, float time, bool stopWhenDone)
    {
        float t = 0f;
        while (t < time)
        {
            t += Time.unscaledDeltaTime; // unaffected by timescale
            src.volume = Mathf.Lerp(from, to, t / Mathf.Max(0.0001f, time));
            yield return null;
        }
        src.volume = to;
        if (stopWhenDone && Mathf.Approximately(to, 0f)) src.Stop();
    }
}
