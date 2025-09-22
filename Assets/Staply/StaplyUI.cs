using UnityEngine;
using System.Collections;

public class StaplyUI : MonoBehaviour
{
    [Header("Play 'No' Animation + Sound")]
    [SerializeField] Animator targetAnimator;
    [SerializeField] string triggerName = "No";

    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip clip;          // Optional: assign here, or leave null to use AudioSource.clip
    [SerializeField] GameObject uiToShow;     // The UI part to show while the sound plays

    [Tooltip("If a clip is assigned, use its length; otherwise fall back to Show Seconds.")]
    [SerializeField] bool useClipLength = true;
    [SerializeField] float showSecondsFallback = 5f;

    Coroutine running;

    // Hook this to the first button's OnClick()
    public void OnPlayNo()
    {
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(PlayNoRoutine());
    }

    IEnumerator PlayNoRoutine()
    {
        // Trigger animation
        if (targetAnimator && !string.IsNullOrEmpty(triggerName))
            targetAnimator.SetTrigger(triggerName);

        // Play audio
        if (audioSource)
        {
            if (clip) audioSource.clip = clip;
            audioSource.Stop();
            audioSource.Play();
        }

        // Show UI
        if (uiToShow) uiToShow.SetActive(true);

        // Wait for duration
        float wait = showSecondsFallback;
        if (useClipLength && (clip || (audioSource && audioSource.clip)))
            wait = clip ? clip.length : audioSource.clip.length;

        yield return new WaitForSeconds(wait);

        // Hide UI
        if (uiToShow) uiToShow.SetActive(false);
        running = null;
    }

    // Hook this to the quit button's OnClick()
    public void OnQuitGame()
    {
    #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // stops Play Mode in editor
    #else
        Application.Quit();
    #endif
    }
}
