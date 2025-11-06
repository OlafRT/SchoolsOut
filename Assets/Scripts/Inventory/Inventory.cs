using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "RPG/Inventory", fileName = "PlayerInventory")]
public class Inventory : ScriptableObject {
    [Min(1)] public int capacity = 20; // WoW-like bag size
    [SerializeField] private List<ItemStack> slots = new List<ItemStack>();

    public event Action OnInventoryChanged;

    private void OnEnable(){
        if (slots.Count == 0){
            for(int i=0;i<capacity;i++) slots.Add(new ItemStack(null,0));
        }
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

    public void Clear() {
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
}