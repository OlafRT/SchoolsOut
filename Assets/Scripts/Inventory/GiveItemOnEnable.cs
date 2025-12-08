using UnityEngine;

public class GiveItemOnEnable : MonoBehaviour
{
    public Inventory playerInventory;
    public ItemTemplate item;
    [Min(1)] public int amount = 1;
    public ScreenToast toast; // optional

    bool _done;

    void OnEnable()
    {
        if (_done || !playerInventory || !item) return;
        var inst = new ItemInstance
        {
            template      = item,
            itemLevel     = 1,
            requiredLevel = 1,
            rarity        = item.rarity,
            affix         = AffixType.Scholar, // ignored for static items; any value ok
            value         = 1
        };

        // Add amount (stacks if template is stackable)
        if (!playerInventory.Add(inst, amount) && toast)
            toast.Show("My backpack is full.", Color.yellow);

        _done = true;
    }
}
