using UnityEngine;

/// <summary>
/// Attach this to the same GameObject as the Animator (the mesh/rig child).
/// Unity fires Animation Events on components that share a GameObject with the
/// Animator — this relay forwards them up to NPCCallForBackup on the root.
/// </summary>
public class NPCAnimEventRelay : MonoBehaviour
{
    NPCCallForBackup callForBackup;

    void Awake()
    {
        // Walk up the hierarchy to find the ability script on the root NPC.
        callForBackup = GetComponentInParent<NPCCallForBackup>();
    }

    /// <summary>
    /// Called by the Animation Event on the last frame of the phone-call clip.
    /// </summary>
    public void OnPhoneCallComplete()
    {
        if (callForBackup) callForBackup.OnPhoneCallComplete();
    }
}
