// SaveIndicator.cs
// ─────────────────────────────────────────────────────────────────────────────
// Attach to the root of your save indicator — the parent that holds both
// the 3D floppy disk and the TMP text child.
//
// ANIMATOR SETUP
// ──────────────
//   Create an Animator on the floppy disk GameObject with two states:
//     • "Idle"    — default state, disk is still
//     • "Spin"    — the spin/rotation animation
//   Add a Trigger parameter called "PlaySpin".
//   In the Idle state, add a transition to Spin triggered by PlaySpin.
//   The Spin state transitions back to Idle when it finishes (has exit time).
//
// HIERARCHY SUGGESTION
// ────────────────────
//   SaveIndicatorRoot     ← this script goes here
//   ├── FloppyDisk        ← Animator + 3D mesh
//   └── SaveText          ← TMP_Text, starts disabled
//
// SETUP
// ─────
//   1. Attach this script to SaveIndicatorRoot
//   2. Assign floppyAnimator, saveText in the Inspector
//   3. Leave SaveIndicatorRoot DISABLED in the scene — the script manages it
//   4. Drag SaveIndicatorRoot into SaveGameButton's Save Indicator slot
// ─────────────────────────────────────────────────────────────────────────────

using System.Collections;
using UnityEngine;
using TMPro;

public class SaveIndicator : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Animator on the floppy disk child.")]
    public Animator floppyAnimator;
    [Tooltip("TMP_Text that shows 'Game saved to disk!' — starts disabled.")]
    public TMP_Text saveText;

    [Header("Animator")]
    [Tooltip("Name of the Trigger parameter that kicks off the spin.")]
    public string spinTrigger = "PlaySpin";
    [Tooltip("Name of the Animator state to wait for completion of.")]
    public string spinStateName = "Spin";

    [Header("Timing")]
    [Tooltip("Seconds to wait after the spin finishes before showing the text.")]
    public float pauseBeforeText = 0.15f;
    [Tooltip("How long the text stays visible before everything fades out.")]
    public float textHoldDuration = 1.2f;
    [Tooltip("How long the fade-out takes.")]
    public float fadeDuration = 0.5f;

    [Header("Text Fade")]
    [Tooltip("If the text has a CanvasGroup you can fade it; otherwise it just toggles.")]
    public CanvasGroup textCanvasGroup;

    Coroutine _co;

    void Awake()
    {
        // Hide text but keep this GameObject active so coroutines can run.
        // The root object must be ACTIVE in the scene — only the visuals are hidden.
        if (saveText) saveText.gameObject.SetActive(false);

        // Hide all renderers and the text so the disk is invisible until needed
        SetVisible(false);
    }

    /// <summary>Call this to trigger the full save indicator sequence.</summary>
    public void Show()
    {
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        // ── Reset: make sure text is hidden and disk is visible ───────────────
        if (saveText) saveText.gameObject.SetActive(false);
        if (textCanvasGroup) textCanvasGroup.alpha = 0f;
        SetVisible(true);

        // ── Trigger spin and wait for it to finish ────────────────────────────
        if (floppyAnimator)
        {
            floppyAnimator.SetTrigger(spinTrigger);

            // Wait a couple frames for Animator to transition into Spin
            yield return null;
            yield return null;

            float timeout = 5f;
            float elapsed = 0f;
            bool enteredSpin = false;

            while (elapsed < timeout)
            {
                elapsed += Time.unscaledDeltaTime;
                var info = floppyAnimator.GetCurrentAnimatorStateInfo(0);

                if (!enteredSpin && info.IsName(spinStateName))
                    enteredSpin = true;

                if (enteredSpin && !info.IsName(spinStateName))
                    break;

                if (enteredSpin && info.IsName(spinStateName) && info.normalizedTime >= 1f && !info.loop)
                    break;

                yield return null;
            }
        }

        // ── Brief pause ───────────────────────────────────────────────────────
        if (pauseBeforeText > 0f)
            yield return new WaitForSecondsRealtime(pauseBeforeText);

        // ── Show text (fade in if CanvasGroup available, otherwise just enable)
        if (saveText) saveText.gameObject.SetActive(true);
        if (textCanvasGroup)
        {
            float t = 0f;
            float fadeInDuration = 0.3f;
            while (t < fadeInDuration)
            {
                t += Time.unscaledDeltaTime;
                textCanvasGroup.alpha = Mathf.Clamp01(t / fadeInDuration);
                yield return null;
            }
            textCanvasGroup.alpha = 1f;
        }

        // ── Hold ──────────────────────────────────────────────────────────────
        yield return new WaitForSecondsRealtime(textHoldDuration);

        // ── Fade out text then disk ───────────────────────────────────────────
        if (textCanvasGroup)
        {
            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                textCanvasGroup.alpha = Mathf.Lerp(1f, 0f, t / fadeDuration);
                yield return null;
            }
            textCanvasGroup.alpha = 0f;
        }

        if (saveText) saveText.gameObject.SetActive(false);
        SetVisible(false);
        _co = null;
    }

    void SetVisible(bool visible)
    {
        // Toggle all renderers on this object and its children
        foreach (var r in GetComponentsInChildren<Renderer>(true))
            r.enabled = visible;
    }
}