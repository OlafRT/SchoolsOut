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

    // runtime instance we’ll generate once
    ItemInstance rolledInstance;

    void Awake()
    {
        // Roll the instance we’re going to give
        if (template != null)
        {
            rolledInstance = AffixRoller.CreateFromTemplate(template, itemLevel);
        }

        // Ensure we have a trigger collider so the player can walk into it
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
        // We only care if the player walked into it
        if (!other.CompareTag("Player")) return;
        if (rolledInstance == null) return;
        if (playerInventory == null) return;

        // Try to add to inventory
        bool added = playerInventory.Add(rolledInstance, 1);

        if (!added)
        {
            // inventory full, optional toast
            if (toast) toast.Show("My backpack is full.", Color.yellow);
            return;
        }

        // Success pickup
        if (toast) toast.Show($"Picked up {rolledInstance.DisplayName}", Color.green);

        TutorialEvents.RaisePickedUpItem();

        if (pickupSfx)
        {
            AudioSource.PlayClipAtPoint(pickupSfx, transform.position, pickupVolume);
        }

        // Destroy the item in the world
        Destroy(gameObject);
    }
}
