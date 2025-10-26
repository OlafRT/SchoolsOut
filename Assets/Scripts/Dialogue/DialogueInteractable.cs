using UnityEngine;

public class DialogueInteractable : MonoBehaviour
{
    [TextArea(2,5)]
    public string[] lines;

    [Tooltip("If true, NPC will rotate to face the player when dialogue starts.")]
    public bool facePlayerOnStart = true;

    // Optional: place this Transform exactly on the NPC's tile center if your model origin isn't centered.
    public Transform tileCenterOverride;

    public Vector3 GetTileCenter()
        => tileCenterOverride ? tileCenterOverride.position : transform.position;
}
