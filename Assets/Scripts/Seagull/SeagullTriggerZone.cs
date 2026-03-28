// SeagullTriggerZone.cs
// ─────────────────────────────────────────────────────────────────────────────
// Place this on any GameObject with a trigger Collider in your scene.
// When the player walks in, it fires the seagull sequence once and
// then quietly removes itself so it can never trigger again.
//
// SETUP
// ─────
//   1. Add a Box/Sphere Collider to a new empty GameObject, tick "Is Trigger"
//   2. Add this script
//   3. Drag your SeagullRoot (with SeagullCameraSequence on it) into "sequence"
// ─────────────────────────────────────────────────────────────────────────────

using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SeagullTriggerZone : MonoBehaviour
{
    [Tooltip("The GameObject that has SeagullCameraSequence on it.")]
    public SeagullCameraSequence sequence;

    void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (!sequence) return;

        sequence.TriggerSequence();

        // Disable the zone so it can never fire again this session
        gameObject.SetActive(false);
    }
}
