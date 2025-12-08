using UnityEngine;

public class InteractSwapItems : MonoBehaviour
{
    [Header("Refs")]
    public Inventory playerInventory;
    public ScreenToast toast;               // optional
    public AudioSource sfx;                 // optional
    public ParticleSystem vfx;              // optional

    [Header("Swap")]
    public ItemTemplate requiredItem;       // Empty Bucket
    public ItemTemplate resultItem;         // Filled Bucket
    [Min(1)] public int resultAmount = 1;

    [Header("Quest ping (optional)")]
    public string questCollectId = "bucket_filled";  // will emit ItemLooted("bucket_filled", 1)

    // Call this from your dialogue node (or button) when player picks the "Fill bucket" line
    public void TrySwap()
    {
        if (!playerInventory || !requiredItem || !resultItem) return;

        if (!playerInventory.HasAtLeast(requiredItem, 1))
        {
            toast?.Show("I need an empty bucket.", Color.yellow);
            return;
        }

        // Space check: you can tailor this. Most inventories should accept adding if we remove 1 first.
        playerInventory.RemoveItems(requiredItem, 1);

        var resultInst = new ItemInstance
        {
            template      = resultItem,
            itemLevel     = 1,
            requiredLevel = 1,
            rarity        = resultItem.rarity,
            affix         = AffixType.Scholar, // ignored if static
            value         = resultItem.fixedValue
        };

        if (!playerInventory.Add(resultInst, Mathf.Max(1, resultAmount)))
        {
            // If inventory full after removal, try to put the empty bucket back so we don't lose it
            playerInventory.Add(new ItemInstance { template = requiredItem, itemLevel = 1, requiredLevel = 1, rarity = requiredItem.rarity }, 1);
            toast?.Show("My backpack is full.", Color.yellow);
            return;
        }

        if (sfx) sfx.Play();
        if (vfx) { vfx.gameObject.SetActive(true); vfx.Play(); }

        // Optional quest progress: treat “filled” as a collect event
        QuestEvents.ItemLooted?.Invoke(questCollectId, 1);  // reuses your existing ItemLooted channel. :contentReference[oaicite:1]{index=1}
    }
}
