// WorldPickupItem.cs  (updated for save system support)
// ─────────────────────────────────────────────────────────────────────────────
// Changes from original:
//
//  • If this GameObject has a SaveableObject component, the item hides itself
//    with SetActive(false) instead of Destroy so the save system can track
//    whether it has been picked up.
//
//  • If there is no SaveableObject (e.g. dynamically spawned loot that doesn't
//    need to persist), it still calls Destroy as before — no behaviour change.
//
// SAVE SYSTEM USAGE
// ─────────────────
//  1. Add a SaveableObject component to any WorldPickupItem you place in the
//     scene that should remain collected after the player saves and reloads.
//  2. That's it. The save manager will record isActive=false once picked up
//     and restore that state on load so the item doesn't reappear.
// ─────────────────────────────────────────────────────────────────────────────

using UnityEngine;

[DisallowMultipleComponent]
public class WorldPickupItem : MonoBehaviour
{
    [Header("What item does this pickup give?")]
    public ItemTemplate template;
    [Range(1,30)] public int itemLevel = 1;

    [Tooltip("Player inventory to receive this item.")]
    public Inventory playerInventory;

    [Tooltip("Screen toast for feedback (optional).")]
    public ScreenToast toast;

    [Tooltip("SFX played on successful pickup.")]
    public AudioClip pickupSfx;
    [Range(0f, 1f)] public float pickupVolume = 1f;

    // Runtime instance rolled once on Awake
    ItemInstance rolledInstance;

    // Cache whether we have a SaveableObject — determines Destroy vs SetActive
    bool _hasSaveable;

    void Awake()
    {
        _hasSaveable = GetComponent<SaveableObject>() != null;

        if (template != null)
            rolledInstance = AffixRoller.CreateFromTemplate(template, itemLevel);

        // Ensure we have a trigger collider
        var col = GetComponent<Collider>();
        if (!col)
        {
            col = gameObject.AddComponent<SphereCollider>();
            ((SphereCollider)col).radius = 0.5f;
        }
        col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (rolledInstance == null) return;
        if (playerInventory == null) return;

        bool added = playerInventory.Add(rolledInstance, 1);

        if (!added)
        {
            if (toast) toast.Show("My backpack is full.", Color.yellow);
            return;
        }

        // Feedback
        if (toast) toast.Show($"Picked up {rolledInstance.DisplayName}", Color.green);

        TutorialEvents.RaisePickedUpItem();

        if (pickupSfx)
            AudioSource.PlayClipAtPoint(pickupSfx, transform.position, pickupVolume);

        if (template != null)
            QuestEvents.ItemLooted?.Invoke(template.id, 1);

        // ── Remove from world ──────────────────────────────────────────────
        // If there's a SaveableObject on this GameObject, hide it so the save
        // system can record it as "collected" (isActive = false).
        // Otherwise, destroy it outright as before.
        if (_hasSaveable)
        {
            gameObject.SetActive(false);

            // Persist immediately so the item stays collected if the player
            // dies and the scene reloads before they reach a manual save point.
            var save = GameSaveManager.I;
            if (save != null && save.ActiveSlot >= 0)
                save.Save(save.ActiveSlot);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}