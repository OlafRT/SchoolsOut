// StaplyController.cs (patched)
using UnityEngine;
using System.Collections;

public class StaplyController : MonoBehaviour {
    [Header("Anchors (LOCAL)")]
    public Transform cornerAnchor;
    public Transform talkAnchor;

    [Header("Visual / Audio")]
    public Transform visualRoot;     // holds mesh + Animator
    public Animator animator;
    public AudioSource audioSource;
    public GameObject exclamation3D;   // optional: a little "!" above the 3D Staply
    public GameObject exclamationUI;   // optional: an "!" child on the UI proxy button

    [Header("UI Proxy")]
    public GameObject uiProxyButton; // screen-space Button GO

    [Header("Sizes/Timing")]
    public float talkScale = 28f;
    public float cornerScale = 3.5f;
    public float moveSeconds = 0.35f;

    public StaplyManager manager;

    void Reset(){
        if (visualRoot == null && transform.childCount > 0)
            visualRoot = transform.GetChild(0);
    }

    // ----- Public API -----
    public void Appear(){
        // show 3D staply, hide proxy
        SetProxy(false);
        SetVisual(true);

        StopAllCoroutines();
        animator?.ResetTrigger("PopOut");
        animator?.SetTrigger("PopIn");
        StartCoroutine(TweenLocal(talkAnchor.localPosition, talkAnchor.localRotation, talkScale));
    }

    public void HideToCorner(bool swapToProxy = true){
        StopAllCoroutines();
        animator?.SetTrigger("Scoot");
        StartCoroutine(TweenLocal(cornerAnchor.localPosition, cornerAnchor.localRotation, cornerScale, () => {
            if (swapToProxy){
                // tiny for 1 frame so you don't see a pop
                SetVisual(false);
                SetProxy(true);
            } else {
                animator?.SetTrigger("PopOut");
            }
        }));
    }

    public void OnProxyClicked(){
    // Forward to manager so it decides what to do
    if (manager != null) manager.OnStaplyClicked();
    else Appear(); // fallback (but ideally always wire the manager)
    }

    public void PlayLine(AudioClip clip){
        if (clip){
            audioSource.clip = clip; audioSource.Play();
            animator?.SetBool("Talking", true);
            CancelInvoke(nameof(StopMouth));
            Invoke(nameof(StopMouth), clip.length);
        } else StopMouth();
    }

    public void StopTalking(){
        if (audioSource && audioSource.isPlaying) audioSource.Stop();
        StopMouth();
    }

    // ----- Internals -----
    IEnumerator TweenLocal(Vector3 posTarget, Quaternion rotTarget, float scaleTarget, System.Action onDone=null){
        Vector3 p0 = transform.localPosition;
        Quaternion r0 = transform.localRotation;
        float s0 = visualRoot ? visualRoot.localScale.x : 1f;

        float t = 0f;
        while (t < moveSeconds){
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / moveSeconds);
            transform.localPosition = Vector3.Lerp(p0, posTarget, a);
            transform.localRotation = Quaternion.Slerp(r0, rotTarget, a);
            if (visualRoot){
                float s = Mathf.Lerp(s0, scaleTarget, a);
                visualRoot.localScale = new Vector3(s,s,s);
            }
            yield return null;
        }
        transform.localPosition = posTarget;
        transform.localRotation = rotTarget;
        if (visualRoot) visualRoot.localScale = new Vector3(scaleTarget, scaleTarget, scaleTarget);

        onDone?.Invoke();
    }

    void SetProxy(bool on){
        if (uiProxyButton) uiProxyButton.SetActive(on);
    }
    void SetVisual(bool on){
        if (visualRoot) visualRoot.gameObject.SetActive(on);
    }

    public void SetExclamation(bool on)
    {
        if (exclamation3D) exclamation3D.SetActive(on);
        if (exclamationUI) exclamationUI.SetActive(on);
    }

    void StopMouth(){ animator?.SetBool("Talking", false); }
}
