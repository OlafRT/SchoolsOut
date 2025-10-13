using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LootUI : MonoBehaviour
{
    [Header("Layout")]
    public Transform slotsRoot;       // parent for 4 slots (vertical)
    public LootSlotUI slotPrefab;     // simple slot with icon + label

    [Header("Refs")]
    public Inventory playerInventory;
    public PlayerWallet wallet;
    public DragController dragController; // reuse for drag ghost
    public ItemTooltipUI tooltip;

    CorpseLoot _corpse;

    public void Bind(CorpseLoot corpse)
    {
        _corpse = corpse;
        Build();
        Refresh();
    }

    void Build()
    {
        foreach (Transform c in slotsRoot) Destroy(c.gameObject);

        // Slot 0 = money (if any)
        var s0 = Instantiate(slotPrefab, slotsRoot);
        s0.BindMoney(this, 0);

        // Slots 1..3 items
        for (int i = 1; i < 4; i++)
        {
            var s = Instantiate(slotPrefab, slotsRoot);
            s.BindItem(this, i - 1);
        }
    }

    public void Refresh()
    {
        int idx = 0;
        // money first
        var money = slotsRoot.GetChild(idx++).GetComponent<LootSlotUI>();
        money.SetMoney(_corpse.dollars);

        // items
        for (int i = 0; i < 3; i++)
        {
            var ui = slotsRoot.GetChild(idx++).GetComponent<LootSlotUI>();
            ItemInstance item = (i < _corpse.items.Count) ? _corpse.items[i] : null;
            ui.SetItem(item);
        }

        if (_corpse.IsEmpty) _corpse.OnLootAllTaken();
    }

    public void TakeMoney()
    {
        if (_corpse.dollars <= 0) return;
        wallet?.Add(_corpse.dollars);
        _corpse.dollars = 0;
        Refresh();
    }

    public bool TakeItem(int corpseItemIndex)
    {
        if (corpseItemIndex < 0 || corpseItemIndex >= _corpse.items.Count) return false;
        var item = _corpse.items[corpseItemIndex];
        if (playerInventory.Add(item, 1))
        {
            _corpse.items.RemoveAt(corpseItemIndex);
            Refresh();
            return true;
        }
        return false; // inventory full
    }

    public void Close() => gameObject.SetActive(false);

    // Drag support from loot → bag
    public void BeginDragFromLoot(int corpseItemIndex, UnityEngine.Sprite icon)
    {
        if (corpseItemIndex < 0 || corpseItemIndex >= _corpse.items.Count) return;
        if (!icon) return;
        // we piggy-back DragController by adding a new source type
        dragController.BeginDragFromLoot(corpseItemIndex, icon, this);
    }
}
