using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.Video;

public class MenuController : MonoBehaviour
{
    [Header("UI Root")]
    [SerializeField] private GameObject buttonsRoot;

    [Header("Effect Pieces")]
    [SerializeField] private Animator spongeAnimator;
    [SerializeField] private string spongeTriggerName = "Erase";
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip clickSfx;

    [Header("SFX Volumes")]
    [SerializeField, Range(0f, 1f)] private float eraseSfxVolume = 1f;

    [Header("Timing")]
    [SerializeField] private float preEffectHideTime = 0.15f;

    [Header("Finish Detection")]
    [Tooltip("Prefer using an Animation Event that calls OnSpongeEffectDone(). If false, we fallback to a fixed duration.")]
    [SerializeField] private bool useSpongeAnimationEvent = true;
    [SerializeField] private float spongeEffectDurationFallback = 1.2f; // used if no anim event

    [Header("Camera kick after effect (Start Game)")]
    [SerializeField] private Animator cameraAnimator;
    [SerializeField] private string cameraStartTrigger = "start";

    [Header("Actions")]
    public UnityEvent onStartGame;   // optional: do something after camera trigger
    public UnityEvent onLoadGame;
    public UnityEvent onOptions;
    public UnityEvent onCredits;

    [Header("Class Select UI")]
    [SerializeField] private GameObject classSelectRoot; // set inactive by default in the Scene

    [Header("Credits")]
    [SerializeField] private string cameraCreditsTrigger = "credits";
    [SerializeField] private string creditsStateName = "CreditsCam"; // name of the credits state
    [SerializeField] private int cameraLayerIndex = 0;
    [SerializeField] private bool useCreditsEvent = true; // set false to ignore the anim event

    bool _busy, _waitVideo, _waitSponge;

    void Start()
    {
        if (videoPlayer)
        {
            // Make sure we can detect finish
            videoPlayer.isLooping = false;
        }
    }

    // -------- Button hooks --------
    public void OnPressStart()   { if (!_busy) StartCoroutine(RunEffectThen(onStartGame, triggerCameraAfter:true)); }
    public void OnPressLoad()    { if (!_busy) StartCoroutine(RunEffectThen(onLoadGame)); }
    public void OnPressOptions() { if (!_busy) StartCoroutine(RunEffectThen(onOptions)); }
    public void OnPressCredits() { if (!_busy) StartCoroutine(RunEffectThen(onCredits)); }

    public void OnPressQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    IEnumerator RunEffectThen(UnityEvent action, bool triggerCameraAfter = false)
    {
        _busy = true;

        // Hide buttons slightly before effect
        if (buttonsRoot) buttonsRoot.SetActive(false);
        if (preEffectHideTime > 0f) yield return new WaitForSeconds(preEffectHideTime);

        // Mark waits
        _waitVideo = videoPlayer != null;
        _waitSponge = spongeAnimator != null;

        // Start effects simultaneously
        if (spongeAnimator && !string.IsNullOrEmpty(spongeTriggerName))
        {
            spongeAnimator.ResetTrigger(spongeTriggerName);
            spongeAnimator.SetTrigger(spongeTriggerName);

            if (!useSpongeAnimationEvent)
                StartCoroutine(FinishSpongeAfter(spongeEffectDurationFallback));
            // If using an Animation Event, call OnSpongeEffectDone() at the end of the clip.
        }

        if (videoPlayer)
        {
            videoPlayer.loopPointReached -= OnVideoFinished;
            videoPlayer.loopPointReached += OnVideoFinished;

            if (!videoPlayer.isPrepared && videoPlayer.clip != null)
            {
                videoPlayer.Prepare();
                while (!videoPlayer.isPrepared) yield return null;
            }
            videoPlayer.Stop();
            videoPlayer.Play();
        }

        if (sfxSource)
        {
            if (clickSfx) sfxSource.PlayOneShot(clickSfx, eraseSfxVolume); // volumeScale
            else if (!sfxSource.isPlaying) sfxSource.Play();
        }

        // Wait until both have finished (if present)
        while ((_waitVideo) || (_waitSponge))
            yield return null;

        // NOW kick the camera (only for Start Game path)
        if (triggerCameraAfter && cameraAnimator && !string.IsNullOrEmpty(cameraStartTrigger))
            cameraAnimator.SetTrigger(cameraStartTrigger);

        // Optional: do Start/Load/Options/Credits action after the camera trigger
        action?.Invoke();

        _busy = false;
    }

    // ---- Finish signals ----
    void OnVideoFinished(VideoPlayer vp)
    {
        _waitVideo = false;
        vp.loopPointReached -= OnVideoFinished;
    }

    IEnumerator FinishSpongeAfter(float t)
    {
        yield return new WaitForSeconds(t);
        _waitSponge = false;
    }

    // Call this from an Animation Event at the end of the sponge wipe clip
    public void OnSpongeEffectDone()
    {
        _waitSponge = false;
    }

    public void OnCameraArrived_ShowClassSelect()
    {
        if (classSelectRoot) classSelectRoot.SetActive(true);
    }

    public void PlayCredits()
    {
        if (cameraAnimator && !string.IsNullOrEmpty(cameraCreditsTrigger))
        {
            cameraAnimator.ResetTrigger(cameraCreditsTrigger);
            cameraAnimator.SetTrigger(cameraCreditsTrigger);

            if (!useCreditsEvent)    // fallback path
                StartCoroutine(WaitCreditsThenShowMenu());
        }
    }

    // Call from an Animation Event at the END of the CreditsCam clip (via CameraEventReceiver).
    public void OnCreditsFinished_ShowMenu()
    {
        if (buttonsRoot) buttonsRoot.SetActive(true);
    }

    public void ShowMainMenuButtons()
    {
        if (buttonsRoot) buttonsRoot.SetActive(true);
    }

    public void OnStartBackFinished_ShowMenu()
    {
        if (classSelectRoot) classSelectRoot.SetActive(false);
        ShowMainMenuButtons();
    }

    private System.Collections.IEnumerator WaitCreditsThenShowMenu()
    {
        if (!cameraAnimator) yield break;

        int creditsHash = Animator.StringToHash(creditsStateName);

        // Wait until we ENTER the credits state
        while (true)
        {
            var st = cameraAnimator.GetCurrentAnimatorStateInfo(cameraLayerIndex);
            bool inCredits = st.shortNameHash == creditsHash || st.IsName(creditsStateName);
            if (inCredits) break;
            yield return null;
        }

        // Wait until we EXIT the credits state or it finishes (normalizedTime >= 0.99f without transition)
        while (true)
        {
            var st = cameraAnimator.GetCurrentAnimatorStateInfo(cameraLayerIndex);
            bool inCredits = st.shortNameHash == creditsHash || st.IsName(creditsStateName);
            if (!inCredits || (!cameraAnimator.IsInTransition(cameraLayerIndex) && st.normalizedTime >= 0.99f))
                break;
            yield return null;
        }

        OnCreditsFinished_ShowMenu();
    }
}
