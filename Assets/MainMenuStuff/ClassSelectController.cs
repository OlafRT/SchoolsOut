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
    [SerializeField] private string nerdForwardState = "NerdChosen";
    [SerializeField] private string jockForwardState = "JockChosen";
    [SerializeField] private string backNerdTrigger  = "backnerd";
    [SerializeField] private string backJockTrigger  = "backjock";
    [SerializeField] private string selectIdleState  = "CamIdle";

    [Header("Layer / Fallback")]
    [SerializeField] private int layer = 0;
    [SerializeField] private bool fallbackDetectBackDone = true;

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

    // Back button OnClick
    public void BackFromChoice()
    {
        if (_busy || _lastPick == ClassType.None || cameraAnimator == null) return;
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
