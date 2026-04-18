using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "RPG/Inventory", fileName = "PlayerInventory")]
public class Inventory : ScriptableObject {
    [Min(1)] public int capacity = 20; // WoW-like bag size
    [SerializeField] private List<ItemStack> slots = new List<ItemStack>();

    public event Action OnInventoryChanged;

    // Set to true by Restore() so OnEnable doesn't wipe loaded data.
    private bool _restoredThisSession = false;

    private void OnEnable()
    {
        // Skip the wipe if Restore() already populated the slots this session.
        // This prevents a Unity Editor domain-reload (or any other OnEnable
        // re-trigger) from clearing items that were just loaded from a save.
        if (_restoredThisSession) return;

        // Always reinitialize at runtime. Inventory is a ScriptableObject whose
        // serialized data persists between Editor Play sessions, so without this
        // the bag appears full at the start of every new session.
        slots.Clear();
        for (int i = 0; i < capacity; i++) slots.Add(new ItemStack(null, 0));
    }

    public IReadOnlyList<ItemStack> Slots => slots;

    public void MarkDirty() { OnInventoryChanged?.Invoke(); }

    public bool Add(ItemInstance item, int amount)
    {
        if (item == null || amount <= 0) return false;

        bool stackable = item.template != null && item.template.isStackable;
        int maxStack = stackable ? Mathf.Max(1, item.template.maxStackSize) : 1;

        // 1) If stackable, first try to top up existing stacks of the same template
        if (stackable)
        {
            for (int i = 0; i < slots.Count && amount > 0; i++)
            {
                var s = slots[i];
                if (s.IsEmpty) continue;
                if (s.item == null || s.item.template != item.template) continue;

                int spaceLeft = maxStack - s.count;
                if (spaceLeft <= 0) continue;

                int toAdd = Mathf.Min(spaceLeft, amount);
                s.count += toAdd;
                amount -= toAdd;
                slots[i] = s;
            }
        }

        // 2) Then use empty slots for whatever is left
        for (int i = 0; i < slots.Count && amount > 0; i++)
        {
            if (slots[i].IsEmpty)
            {
                int toPut = stackable ? Mathf.Min(amount, maxStack) : 1;
                slots[i] = new ItemStack(item, toPut);
                amount -= toPut;
            }
        }

        OnInventoryChanged?.Invoke();
        return amount == 0; // false means: ran out of space
    }

    /// <summary>
    /// Called by the save system to populate the inventory from a save file.
    /// Replaces the reflection hack in ReadInventory and is the ONLY correct
    /// way to restore slots from outside this class.
    ///
    /// Guarantees that slots.Count == capacity after the call, which prevents
    /// the "shows empty, reports full" bug caused by a count/capacity mismatch.
    /// </summary>
    public void Restore(int savedCapacity, List<ItemStack> loadedSlots)
    {
        capacity = savedCapacity;
        _restoredThisSession = true;

        slots.Clear();
        for (int i = 0; i < loadedSlots.Count && i < capacity; i++)
            slots.Add(loadedSlots[i] ?? new ItemStack(null, 0));

        // Pad with empty slots so slots.Count always equals capacity.
        // Without this, if the saved list is shorter than capacity for any
        // reason, Add() would iterate fewer entries than the UI slot count
        // and report "full" while some UI slots appear empty.
        while (slots.Count < capacity)
            slots.Add(new ItemStack(null, 0));

        OnInventoryChanged?.Invoke();
    }

    public void Clear()
    {
        _restoredThisSession = false; // allow OnEnable to reinitialize normally after a clear
        for (int i = 0; i < slots.Count; i++) slots[i] = new ItemStack(null, 0);
        OnInventoryChanged?.Invoke();
    }

    public void RemoveAt(int index, int amount)
    {
        if (index < 0 || index >= slots.Count) return;

        var s = slots[index];
        if (s.IsEmpty) return;

        s.count -= amount;
        if (s.count <= 0)
            s = new ItemStack(null, 0);

        slots[index] = s;
        OnInventoryChanged?.Invoke();
    }

    public void Move(int fromIndex, int toIndex){
        if(fromIndex==toIndex) return;
        if(fromIndex<0||toIndex<0||fromIndex>=slots.Count||toIndex>=slots.Count) return;
        (slots[fromIndex], slots[toIndex]) = (slots[toIndex], slots[fromIndex]);
        OnInventoryChanged?.Invoke();
    }

    public int CountByItemId(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return 0;

        int total = 0;
        for (int i = 0; i < slots.Count; i++)
        {
            var s = slots[i];
            if (s.IsEmpty) continue;

            var t = s.item?.template;
            if (t != null && t.id == itemId)
                total += s.count;
        }
        return total;
    }

    public bool RemoveByItemId(string itemId, int amount)
    {
        if (string.IsNullOrEmpty(itemId) || amount <= 0) return false;

        int remaining = amount;

        for (int i = 0; i < slots.Count && remaining > 0; i++)
        {
            var s = slots[i];
            if (s.IsEmpty) continue;

            var t = s.item?.template;
            if (t == null || t.id != itemId) continue;

            int take = Mathf.Min(s.count, remaining);
            s.count -= take;
            remaining -= take;

            if (s.count <= 0)
                s = new ItemStack(null, 0);

            slots[i] = s;
        }

        if (remaining <= 0)
        {
            OnInventoryChanged?.Invoke();
            return true;
        }

        // Not enough found (we already removed what we could; optional: revert if you want strict behavior)
        OnInventoryChanged?.Invoke();
        return false;
    }
}