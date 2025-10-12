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

    public bool Add(ItemInstance item, int amount){
        if(item == null || amount <= 0) return false;
        for(int i=0;i<slots.Count && amount>0;i++) if(slots[i].IsEmpty){ slots[i] = new ItemStack(item, 1); amount--; }
        OnInventoryChanged?.Invoke();
        return amount==0;
    }

    public void Clear() {
    for (int i = 0; i < slots.Count; i++) slots[i] = new ItemStack(null, 0);
    OnInventoryChanged?.Invoke();
    }

    public void RemoveAt(int index, int amount){
        if(index<0 || index>=slots.Count) return;
        slots[index] = new ItemStack(null,0);
        OnInventoryChanged?.Invoke();
    }

    public void Move(int fromIndex, int toIndex){
        if(fromIndex==toIndex) return;
        if(fromIndex<0||toIndex<0||fromIndex>=slots.Count||toIndex>=slots.Count) return;
        (slots[fromIndex], slots[toIndex]) = (slots[toIndex], slots[fromIndex]);
        OnInventoryChanged?.Invoke();
    }
}