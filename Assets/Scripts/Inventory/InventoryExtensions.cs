using UnityEngine;

public static class InventoryExtensions
{
    public static int CountOf(this Inventory inv, ItemTemplate tpl)
    {
        if (!inv || tpl == null) return 0;
        int total = 0;
        for (int i = 0; i < inv.Slots.Count; i++)
        {
            var s = inv.Slots[i];
            if (s.IsEmpty || s.item?.template != tpl) continue;
            total += Mathf.Max(1, s.count);
        }
        return total;
    }

    public static bool HasAtLeast(this Inventory inv, ItemTemplate tpl, int amount)
    {
        return inv.CountOf(tpl) >= amount;
    }

    // remove up to 'amount' copies (across stacks). Returns how many were removed.
    public static int RemoveItems(this Inventory inv, ItemTemplate tpl, int amount)
    {
        if (!inv || tpl == null || amount <= 0) return 0;
        int left = amount;

        for (int i = 0; i < inv.Slots.Count && left > 0; i++)
        {
            var s = inv.Slots[i];
            if (s.IsEmpty || s.item?.template != tpl) continue;

            int take = Mathf.Min(left, Mathf.Max(1, s.count));
            inv.RemoveAt(i, take); // your RemoveAt already reduces/clears stack in your project
            left -= take;
        }

        return amount - left;
    }
}
