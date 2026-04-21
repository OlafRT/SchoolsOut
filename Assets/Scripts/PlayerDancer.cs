using System.Collections;
using UnityEngine;

/// <summary>
/// Plays a dance animation on the player and cancels it the moment any
/// movement or rotation input is detected — mirroring the EatRoutine pattern
/// in PlayerConsumables.
///
/// Usage:
///   playerDancer.StartDancing("Dance");   // call from your dialogue controller
///   playerDancer.StopDancing();           // call if you ever need to force-cancel
///
/// The dialogue controller should call StartDancing() after the last dialogue
/// line is dismissed. See DialogueInteractable.danceOnFinish / danceAnimTrigger.
/// </summary>
[DisallowMultipleComponent]
public class PlayerDancer : MonoBehaviour
{
    // ─── Animation ─────────────────────────────────────────────────────────────
    [Header("Animation")]
    [Tooltip("Animator on (or under) the player. Auto-found if left empty.")]
    public Animator animator;

    [Tooltip("Default trigger name to set when StartDancing() is called without an argument.")]
    public string defaultDanceTrigger = "Dance";

    [Tooltip("Bool parameter to set TRUE while dancing and FALSE when stopped. " +
             "Leave empty if your Animator uses only a trigger.")]
    public string dancingBool = "";

    [Tooltip("Trigger or bool name that returns the player to idle " +
             "(e.g. 'Idle' trigger or clearing the dancing bool). " +
             "Leave empty to let the Animator's exit transitions handle it.")]
    public string stopDanceTrigger = "";

    // ─── Cancellation ──────────────────────────────────────────────────────────
    [Header("Cancellation")]
    [Tooltip("How far the player must drift (metres) before the dance auto-cancels. " +
             "Acts as a backup to the input check.")]
    public float cancelMoveDistance = 0.15f;

    // ─── Events ────────────────────────────────────────────────────────────────
    [Header("Optional Events")]
    [Tooltip("Optional audio played when the dance starts.")]
    public AudioClip danceStartSfx;
    [Range(0f, 1f)] public float danceStartSfxVolume = 1f;

    // ─── Public state ──────────────────────────────────────────────────────────
    public bool IsDancing { get; private set; }

    // ─── Private ───────────────────────────────────────────────────────────────
    private PlayerMovement movement;
    private Coroutine      danceRoutine;

    // ══════════════════════════════════════════════════════════════════════════
    //  Unity lifecycle
    // ══════════════════════════════════════════════════════════════════════════

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();

        if (!animator)
            animator = GetComponentInChildren<Animator>(true);
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Public API
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Begin dancing. Cancels any ongoing dance first.
    /// <paramref name="triggerName"/> overrides the Inspector default when supplied.
    /// </summary>
    public void StartDancing(string triggerName = null)
    {
        // Cancel any in-progress dance cleanly before starting a new one
        if (danceRoutine != null)
        {
            StopCoroutine(danceRoutine);
            danceRoutine = null;
        }

        string trigger = !string.IsNullOrEmpty(triggerName) ? triggerName : defaultDanceTrigger;
        danceRoutine   = StartCoroutine(DanceRoutine(trigger));
    }

    /// <summary>
    /// Force-stop the dance from outside (e.g. cutscene, combat hit).
    /// </summary>
    public void StopDancing()
    {
        if (danceRoutine != null)
        {
            StopCoroutine(danceRoutine);
            danceRoutine = null;
        }
        EndDance();
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Dance coroutine
    // ══════════════════════════════════════════════════════════════════════════

    IEnumerator DanceRoutine(string triggerName)
    {
        IsDancing = true;

        // Play start SFX
        if (danceStartSfx)
            AudioSource.PlayClipAtPoint(danceStartSfx, transform.position, danceStartSfxVolume);

        // Fire the Animator
        if (animator)
        {
            if (!string.IsNullOrEmpty(triggerName))
                animator.SetTrigger(triggerName);

            if (!string.IsNullOrEmpty(dancingBool))
                animator.SetBool(dancingBool, true);
        }

        Vector3 startPos = transform.position;

        // Loop until any cancellation condition is true
        while (true)
        {
            // 1. WASD / arrow key movement input
            if (movement && movement.HasMoveInput)
                break;

            // 2. Any turn-in-place / strafe key pressed
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.D) ||
                Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow))
                break;

            // 3. Physical position drift — cancels when the player actually moves to a new tile
            if ((transform.position - startPos).sqrMagnitude >
                cancelMoveDistance * cancelMoveDistance)
                break;

            yield return null;
        }

        EndDance();
        danceRoutine = null;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Internals
    // ══════════════════════════════════════════════════════════════════════════

    void EndDance()
    {
        IsDancing = false;

        if (!animator) return;

        // Clear the dancing bool if one was set
        if (!string.IsNullOrEmpty(dancingBool))
            animator.SetBool(dancingBool, false);

        // Fire the stop trigger to return to idle if configured
        if (!string.IsNullOrEmpty(stopDanceTrigger))
            animator.SetTrigger(stopDanceTrigger);
    }
}