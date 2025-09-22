using UnityEngine;
using System.Collections;
using UnityEngine.Video;

public class IntroVideo : MonoBehaviour
{
    [Header("Video")]
    [SerializeField] private VideoPlayer videoPlayer;      // on the video GO
    [SerializeField] private GameObject videoRoot;         // the GO to disable at end (defaults to player GO)

    [Header("Parallel Animation")]
    [SerializeField] private Animator parallelAnimator;    // optional, runs while video plays
    [SerializeField] private string animatorTrigger = "Play"; // leave empty to do nothing
    [Tooltip("If > 0, used as animation duration (seconds). If 0, we'll try to read current state length after starting.")]
    [SerializeField] private float animationExpectedDuration = 0f;

    [Header("Audio While Playing")]
    [SerializeField] private AudioSource audioSource;      // optional; clip/loop set on the source
    [SerializeField] private float audioFadeOutSeconds = 0.25f;

    [Header("Finish Behaviour")]
    [Tooltip("If on, the sequence ends when either video OR animation finishes. If off, waits for both.")]
    [SerializeField] private bool endOnFirstFinished = true;
    [SerializeField] private GameObject[] enableOnEnd;     // UI to show when sequence ends
    [SerializeField] private GameObject[] disableOnEnd;    // anything else to hide (videoRoot is auto-added)

    private bool videoDone, animDone;

    void Reset()
    {
        videoPlayer = GetComponent<VideoPlayer>();
    }

    void Awake()
    {
        if (!videoPlayer) videoPlayer = GetComponent<VideoPlayer>();
        if (!videoRoot && videoPlayer) videoRoot = videoPlayer.gameObject;

        // Ensure end UI starts hidden
        if (enableOnEnd != null)
            foreach (var go in enableOnEnd) if (go) go.SetActive(false);

        if (videoRoot) videoRoot.SetActive(true);
    }

    void Start()
    {
        StartCoroutine(RunSequence());
    }

    private IEnumerator RunSequence()
    {
        // Prep video
        if (videoPlayer)
        {
            videoPlayer.loopPointReached += OnVideoFinished;
            if (!videoPlayer.isPrepared)
            {
                videoPlayer.Prepare();
                while (!videoPlayer.isPrepared) yield return null;
            }
            videoPlayer.Play();
        }

        // Start parallel audio
        if (audioSource)
        {
            if (!audioSource.isPlaying) audioSource.Play();
        }

        // Start parallel animation
        if (parallelAnimator)
        {
            if (!string.IsNullOrEmpty(animatorTrigger))
                parallelAnimator.SetTrigger(animatorTrigger);

            // Determine how long to wait for the animation
            if (animationExpectedDuration > 0f)
            {
                yield return new WaitForSeconds(animationExpectedDuration);
                animDone = true;
            }
            else
            {
                // Give animator a frame to enter its state, then read length
                yield return null;
                var info = parallelAnimator.GetCurrentAnimatorStateInfo(0);
                float length = info.length / Mathf.Max(0.0001f, parallelAnimator.speed);
                yield return new WaitForSeconds(length);
                animDone = true;
            }
        }

        // If we’re ending on the first to finish, we need to also listen for the video.
        // If no animator was provided, the while loop exits as soon as videoDone flips.
        while (endOnFirstFinished ? !(videoDone || animDone)
                                  : !((parallelAnimator ? animDone : true) && (videoPlayer ? videoDone : true)))
        {
            yield return null;
        }

        // Tear down
        if (videoPlayer) videoPlayer.loopPointReached -= OnVideoFinished;

        // Fade out audio and stop
        if (audioSource)
        {
            if (audioFadeOutSeconds > 0f)
                yield return StartCoroutine(FadeAudio(audioSource, audioSource.volume, 0f, audioFadeOutSeconds));
            audioSource.Stop();
            audioSource.volume = 1f; // reset for next time
        }

        // Toggle visibility
        if (videoRoot) videoRoot.SetActive(false);

        if (disableOnEnd != null)
            foreach (var go in disableOnEnd) if (go) go.SetActive(false);

        if (enableOnEnd != null)
            foreach (var go in enableOnEnd) if (go) go.SetActive(true);
    }

    private void OnVideoFinished(VideoPlayer vp)
    {
        videoDone = true;
    }

    private IEnumerator FadeAudio(AudioSource src, float from, float to, float secs)
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.0001f, secs);
            src.volume = Mathf.Lerp(from, to, t);
            yield return null;
        }
        src.volume = to;
    }
}
