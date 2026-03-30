using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class BikeCutsceneTrigger : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Player root transform (the object entering the trigger). If left null, we use the collider's transform.")]
    public Transform playerRoot;

    [Tooltip("The bike root transform that should be moved along the path.")]
    public Transform bikeRoot;

    [Tooltip("The BicycleController on the player. We put it into cutscene mode (keeps crank animated) instead of disabling it.")]
    public BicycleController bicycleController; // direct reference — no more MonoBehaviour cast needed

    [Tooltip("Optional: if the bike has a Rigidbody, we'll move it using MovePosition for smooth physics-friendly motion.")]
    public Rigidbody bikeRigidbody;

    [Header("Path (A, B, C, D...)")]
    public Transform[] points;

    [Tooltip("Units per second.")]
    public float moveSpeed = 4.5f;

    [Tooltip("How close we need to be to consider a point reached.")]
    public float arriveDistance = 0.1f;

    [Tooltip("Rotate the bike to face movement direction.")]
    public bool rotateToPath = true;

    [Tooltip("How fast rotation follows (degrees/sec).")]
    public float rotateSpeed = 360f;

    [Header("Dialogue UI Sequence")]
    public DialogueStep[] dialogueSteps;

    [Tooltip("If true, hides ALL dialogue objects at start and end.")]
    public bool forceHideAllDialogue = true;

    [Header("End Sequence")]
    [Tooltip("Optional screen fade. Assign a CanvasGroup on a full-screen black UI panel.")]
    public CanvasGroup fadeGroup;

    [Tooltip("Seconds to fade to black.")]
    public float fadeDuration = 0.75f;

    [Tooltip("Sounds to play at the end (optional).")]
    public AudioSource sfxSource;
    public AudioClip[] endClips;
    [Header("Audio Boost")]
    [Range(0.1f, 5f)]
    public float oneShotVolumeBoost = 1.6f;

    [Tooltip("Wait this long AFTER fade/sounds before enabling the object.")]
    public float endWaitSeconds = 1.5f;

    [Tooltip("GameObject to enable at the very end.")]
    public GameObject objectToEnableAtEnd;

    [Tooltip("If you want control back after the cutscene, toggle this on.")]
    public bool reEnableBicycleControllerAtEnd = false;

    [Header("Events (optional hooks)")]
    public UnityEvent onCutsceneStart;
    public UnityEvent onReachedFinalPoint;
    public UnityEvent onCutsceneEnd;

    [Header("One-shot")]
    public bool triggerOnlyOnce = true;

    bool hasTriggered = false;
    bool fadeStarted = false;

    [Serializable]
    public class DialogueStep
    {
        public GameObject uiObject;
        [Tooltip("How long to show this line before moving to next.")]
        public float showSeconds = 2f;
    }

    void Reset()
    {
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (hasTriggered && triggerOnlyOnce) return;

        if (!playerRoot) playerRoot = other.transform;

        hasTriggered = true;
        StartCoroutine(CutsceneRoutine());
    }

    IEnumerator CutsceneRoutine()
    {
        onCutsceneStart?.Invoke();

        if (!bikeRoot)
        {
            Debug.LogError("[BikeCutsceneTrigger] bikeRoot is not assigned.");
            yield break;
        }

        if (points == null || points.Length == 0)
        {
            Debug.LogError("[BikeCutsceneTrigger] No points assigned.");
            yield break;
        }

        // Hide all dialogue at start (clean slate)
        if (forceHideAllDialogue && dialogueSteps != null)
        {
            foreach (var step in dialogueSteps)
                if (step.uiObject) step.uiObject.SetActive(false);
        }

        // Hand control to the cutscene.
        // CutsceneMode = true keeps the component alive so FixedUpdate still runs —
        // that's what drives the crank animation. It just skips input and physics.
        if (bicycleController)
        {
            bicycleController.cutsceneMode = true;

            // Zero out any momentum left from the player so movement is fully cutscene-driven
            if (bikeRigidbody)
            {
                bikeRigidbody.velocity        = Vector3.zero;
                bikeRigidbody.angularVelocity = Vector3.zero;
            }
        }

        // If bike has rigidbody, make it kinematic so cutscene positions it cleanly
        bool hadRb = bikeRigidbody != null;
        bool prevKinematic = false;
        if (hadRb)
        {
            prevKinematic = bikeRigidbody.isKinematic;
            bikeRigidbody.isKinematic = true;
        }

        // Start dialogue sequence in parallel
        Coroutine dialogueCo = null;
        if (dialogueSteps != null && dialogueSteps.Length > 0)
            dialogueCo = StartCoroutine(DialogueSequence());

        // Move bike along points (crank spins the whole time thanks to cutsceneMode)
        yield return StartCoroutine(MoveAlongPoints());

        onReachedFinalPoint?.Invoke();

        // Hide dialogue
        if (forceHideAllDialogue && dialogueSteps != null)
        {
            foreach (var step in dialogueSteps)
                if (step.uiObject) step.uiObject.SetActive(false);
        }

        if (dialogueCo != null)
            StopCoroutine(dialogueCo);

        // Fade to black
        if (fadeGroup)
            yield return StartCoroutine(FadeCanvasGroup(fadeGroup, fadeGroup.alpha, 1f, fadeDuration));

        if (endWaitSeconds > 0f)
            yield return new WaitForSeconds(endWaitSeconds);

        if (objectToEnableAtEnd)
            objectToEnableAtEnd.SetActive(true);

        // Restore rigidbody state
        if (hadRb)
            bikeRigidbody.isKinematic = prevKinematic;

        // Return control to the player (or leave in cutscene mode if not re-enabling)
        if (bicycleController)
        {
            if (reEnableBicycleControllerAtEnd)
                bicycleController.cutsceneMode = false;  // player can ride again
            // else: leave cutsceneMode = true so the player stays locked post-cutscene
        }

        onCutsceneEnd?.Invoke();

        if (triggerOnlyOnce)
            gameObject.SetActive(false);
    }

    IEnumerator DialogueSequence()
    {
        for (int i = 0; i < dialogueSteps.Length; i++)
        {
            if (forceHideAllDialogue)
            {
                for (int j = 0; j < dialogueSteps.Length; j++)
                    if (dialogueSteps[j].uiObject) dialogueSteps[j].uiObject.SetActive(false);
            }

            var step = dialogueSteps[i];
            if (step.uiObject) step.uiObject.SetActive(true);

            float dur = Mathf.Max(0.01f, step.showSeconds);
            yield return new WaitForSeconds(dur);
        }
    }

    IEnumerator MoveAlongPoints()
    {
        for (int i = 0; i < points.Length; i++)
        {
            Transform p = points[i];
            if (!p) continue;

            if (!fadeStarted && i == points.Length - 1)
            {
                fadeStarted = true;

                if (fadeGroup)
                    StartCoroutine(FadeCanvasGroup(fadeGroup, fadeGroup.alpha, 1f, fadeDuration));

                if (sfxSource && endClips != null)
                {
                    foreach (var clip in endClips)
                        if (clip) sfxSource.PlayOneShot(clip, oneShotVolumeBoost);
                }
            }

            yield return StartCoroutine(MoveTo(p.position));
        }
    }

    IEnumerator MoveTo(Vector3 targetPos)
    {
        while ((bikeRoot.position - targetPos).sqrMagnitude > arriveDistance * arriveDistance)
        {
            Vector3 current = bikeRoot.position;
            Vector3 next = Vector3.MoveTowards(current, targetPos, moveSpeed * Time.deltaTime);

            if (rotateToPath)
            {
                Vector3 dir = targetPos - current;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.0001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
                    bikeRoot.rotation = Quaternion.RotateTowards(bikeRoot.rotation, targetRot, rotateSpeed * Time.deltaTime);
                }
            }

            if (bikeRigidbody) bikeRigidbody.MovePosition(next);
            else                bikeRoot.position = next;

            yield return null;
        }

        if (bikeRigidbody) bikeRigidbody.MovePosition(targetPos);
        else               bikeRoot.position = targetPos;
    }

    IEnumerator FadeCanvasGroup(CanvasGroup cg, float from, float to, float duration)
    {
        if (!cg) yield break;

        duration = Mathf.Max(0.01f, duration);
        float t = 0f;
        cg.blocksRaycasts = true;
        cg.interactable   = true;

        while (t < duration)
        {
            t += Time.deltaTime;
            cg.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }

        cg.alpha = to;
    }
}