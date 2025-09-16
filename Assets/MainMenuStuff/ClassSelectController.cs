using System.Collections;
using UnityEngine;

public class ClassSelectController : MonoBehaviour
{
    public enum ClassType { None, Nerd, Jock }

    [Header("Refs")]
    [SerializeField] private Animator cameraAnimator;
    [SerializeField] private GameObject classSelectRoot; // panel with Nerd/Jock buttons
    [SerializeField] private GameObject backButtonRoot;  // set inactive by default

    [Header("Animator State/Trigger Names")]
    //[SerializeField] private string nerdForwardState = "NerdChosen";
    //[SerializeField] private string jockForwardState = "JockChosen";
    [SerializeField] private string backNerdTrigger  = "backnerd";
    [SerializeField] private string backJockTrigger  = "backjock";
    [SerializeField] private string selectIdleState  = "CamIdle";

    [Header("Start→Menu Back")]
    [SerializeField] private string startBackTrigger = "startback";  // your trigger
    [SerializeField] private string startBackState   = "BackFromStart"; // state name
    [SerializeField] private bool  fallbackDetectStartBackDone = true;
    [SerializeField] private MenuController menu; // to re-show menu if using fallback

    [Header("Layer / Fallback")]
    [SerializeField] private int layer = 0;
    [SerializeField] private bool fallbackDetectBackDone = true;

    [SerializeField] private RewindFX rewindFX;

    private ClassType _lastPick = ClassType.None;
    private bool _busy;

    // Called together with HoverGlowButton.OnClick() on each choice
    public void PickNerd() { _lastPick = ClassType.Nerd; }
    public void PickJock() { _lastPick = ClassType.Jock; }

    // Called (via CameraEventReceiver) when forward move finishes
    public void OnArrivedAtPick_ShowBack()
    {
        if (backButtonRoot) backButtonRoot.SetActive(true);
    }

    public void BackFromStart()
    {
        if (_busy || cameraAnimator == null) return;
        if (rewindFX) rewindFX.Play();
        _busy = true;

        // Hide class-select UI while we fly back
        if (classSelectRoot) classSelectRoot.SetActive(false);
        if (backButtonRoot)  backButtonRoot.SetActive(false);

        HoverGlowButton.ResetChoiceLock();

        if (!string.IsNullOrEmpty(startBackTrigger))
            cameraAnimator.SetTrigger(startBackTrigger);

        if (fallbackDetectStartBackDone)
            StartCoroutine(WatchStartBackReturnToMenu());
    }

    public void OnStartBackFinished_ShowMenu()
    {
        FinishStartBack_ShowMenu();
    }

    private IEnumerator WatchStartBackReturnToMenu()
    {
        int hash = Animator.StringToHash(startBackState);
        // Wait until we ENTER BackFromStart
        while (true)
        {
            var st = cameraAnimator.GetCurrentAnimatorStateInfo(layer);
            bool inState = st.shortNameHash == hash || st.IsName(startBackState);
            if (inState) break;
            yield return null;
        }
        // Wait until it finishes
        while (true)
        {
            var st = cameraAnimator.GetCurrentAnimatorStateInfo(layer);
            bool inState = st.shortNameHash == hash || st.IsName(startBackState);
            if (!inState || (!cameraAnimator.IsInTransition(layer) && st.normalizedTime >= 0.99f))
                break;
            yield return null;
        }
        FinishStartBack_ShowMenu();
    }

    void FinishStartBack_ShowMenu()
    {
        // Ensure class-select stays hidden
        if (classSelectRoot) classSelectRoot.SetActive(false);

        // Re-show the main menu buttons
        if (menu) menu.ShowMainMenuButtons();

        _lastPick = ClassType.None;
        _busy = false;
    }

    // Back button OnClick
    public void BackFromChoice()
    {
        if (_busy || _lastPick == ClassType.None || cameraAnimator == null) return;
        if (rewindFX) rewindFX.Play();
        _busy = true;

        if (backButtonRoot) backButtonRoot.SetActive(false);

        // Fire the correct back trigger
        string trig = _lastPick == ClassType.Nerd ? backNerdTrigger : backJockTrigger;
        if (!string.IsNullOrEmpty(trig))
            cameraAnimator.SetTrigger(trig);

        // Either rely on an animation event OR poll for CamIdle
        if (fallbackDetectBackDone) StartCoroutine(WatchReturnToIdle());
    }

    // If you add an Animation Event at the end of BackFromNerd/Jock, call this:
    public void OnBackFinished_ShowSelect()
    {
        FinishBackToIdle();
    }

    private IEnumerator WatchReturnToIdle()
    {
        int idleHash = Animator.StringToHash(selectIdleState);
        while (true)
        {
            var st = cameraAnimator.GetCurrentAnimatorStateInfo(layer);
            bool inIdle = st.shortNameHash == idleHash || st.IsName(selectIdleState);
            if (inIdle && !cameraAnimator.IsInTransition(layer)) break;
            yield return null;
        }
        FinishBackToIdle();
    }

    void FinishBackToIdle()
    {
        if (classSelectRoot) classSelectRoot.SetActive(true);
        HoverGlowButton.ResetChoiceLock();   // re-enable hover glow
        _lastPick = ClassType.None;
        _busy = false;
    }
}
