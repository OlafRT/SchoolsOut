// SaveableObject.cs
// ─────────────────────────────────────────────────────────────────────────────
// Attach to any world object whose ACTIVE/INACTIVE state needs to be saved and
// restored — e.g. permanently opened doors, destroyed terrain props, one-time
// environmental triggers.
//
// ═══════════════════════════════════════════════════════════════════════════
//  ⚠️  DO NOT attach this to:
//
//  • WorldPickupItem  →  use WorldPickupItem's built-in save support instead
//                        (it uses SetActive(false); SaveableObject handles it)
//
//  • Quest-triggered NPC parents  →  use QuestActivatedObject instead.
//                        SaveableObject saves a snapshot of active state.  If
//                        the player saves BEFORE accepting a quest, loads, and
//                        then the world state is restored, those NPCs will be
//                        inactive even though the quest is active — causing a
//                        soft-lock.  QuestActivatedObject derives the correct
//                        state from quest progress instead of snapshotting it.
// ═══════════════════════════════════════════════════════════════════════════
//
// SETUP
// ─────
//  1. Add this component to the GameObject.
//  2. The Object ID is filled in automatically in the Editor.
//     !! Never change it after you have shipped a save file !!
//
// EXTENDING
// ─────────
//  Override CollectState() and ApplyState() in a subclass to save more than
//  just active state.  Store extras in WorldObjectSaveData.stateString.
// ─────────────────────────────────────────────────────────────────────────────

using UnityEngine;

[DisallowMultipleComponent]
public class SaveableObject : MonoBehaviour
{
    [Tooltip("Unique stable ID — auto-generated in the Editor. Do not edit.")]
    public string objectId;

#if UNITY_EDITOR
    void OnValidate()
    {
        if (string.IsNullOrEmpty(objectId))
        {
            objectId = System.Guid.NewGuid().ToString("N").Substring(0, 16);
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
#endif

    /// <summary>Called by GameSaveManager while saving.  Returns this object's current state.</summary>
    public virtual WorldObjectSaveData CollectState() =>
        new WorldObjectSaveData { objectId = objectId, isActive = gameObject.activeSelf };

    /// <summary>Called by GameSaveManager while loading.  Apply the saved state to this object.</summary>
    public virtual void ApplyState(WorldObjectSaveData data)
    {
        if (data != null) gameObject.SetActive(data.isActive);
    }
}