using UnityEngine;

/// <summary>
/// Place this on a GameObject with a Trigger Collider to simulate a wet puddle.
/// When the player walks into the trigger, it tells the player's PuddleEffectHandler
/// to play the splash particle and sound.
///
/// SETUP:
///   1. Create a GameObject for your puddle tile (e.g. a flat quad with a water texture).
///   2. Add a BoxCollider (or any collider) — check "Is Trigger".
///   3. Attach this script.
///   4. Assign the player tag in the Inspector (default: "Player").
/// </summary>
public class PuddleTile : MonoBehaviour
{
    [Header("Player Detection")]
    [Tooltip("The tag your player GameObject uses.")]
    [SerializeField] private string playerTag = "Player";

    [Header("Cooldown")]
    [Tooltip("Seconds before the same puddle can trigger again (prevents rapid re-triggers).")]
    [SerializeField] private float triggerCooldown = 0.5f;

    private float _lastTriggerTime = -999f;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        if (Time.time - _lastTriggerTime < triggerCooldown) return;

        _lastTriggerTime = Time.time;

        // Ask the player to play the effect at the puddle's world position
        var handler = other.GetComponentInChildren<PuddleEffectHandler>();
        if (handler == null)
            handler = other.GetComponentInParent<PuddleEffectHandler>();

        if (handler != null)
            handler.PlaySplash(transform.position);
        else
            Debug.LogWarning("[PuddleTile] No PuddleEffectHandler found on player!", other.gameObject);
    }

#if UNITY_EDITOR
    // Draw a subtle blue gizmo so you can see puddle triggers in the Scene view
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 0.5f, 1f, 0.25f);
        var col = GetComponent<Collider>();
        if (col != null)
            Gizmos.DrawCube(col.bounds.center, col.bounds.size);
    }
#endif
}
