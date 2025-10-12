using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BagUI : MonoBehaviour {
    public Inventory inventory; public EquipmentManager equipMgr; public ItemTooltipUI tooltip;
    public ItemSlotUI slotPrefab; public GridLayoutGroup grid;
    private List<ItemSlotUI> slots = new();

    private void OnEnable(){ BuildIfNeeded(); inventory.OnInventoryChanged += Refresh; Refresh(); }
    private void OnDisable(){ inventory.OnInventoryChanged -= Refresh; }

    private void BuildIfNeeded(){ if(slots.Count>0) return; for(int i=0;i<inventory.capacity;i++){ var s = Instantiate(slotPrefab, grid.transform); s.BindInventory(inventory, i, equipMgr, tooltip); slots.Add(s);} }
    private void Refresh(){ foreach(var s in slots) s.Refresh(); }
}