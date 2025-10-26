using UnityEngine;
using System.Collections.Generic;

public class LootUI : MonoBehaviour
{
    [Header("Slots (top to bottom)")]
    // Assign these baked slot objects in prefab (top -> bottom)
    public List<LootSlotUI> slotUIs = new List<LootSlotUI>();

    [Header("Refs")]
    public Inventory playerInventory;      // can be assigned in prefab IF it's a ScriptableObject
    public PlayerWallet wallet;            // also ScriptableObject, so prefab can reference it
    public DragController dragController;  // scene object -> we'll auto-find if null
    public ItemTooltipUI tooltip;          // scene object -> we'll auto-find if null
    public Sprite moneyIcon;               // assign in prefab (sprite asset)

    [Header("Behaviour")]
    public float autoCloseDistance = 4f;

    Transform player;
    CorpseLoot corpse;
    bool _closing = false;

    // --------------------------------------------------
    // CorpseLoot calls this after spawning / on click
    // --------------------------------------------------
    public void Bind(CorpseLoot owner)
    {
        corpse = owner;

        // If these weren't assigned in prefab (scene refs), grab them now
        if (dragController == null)
        {
            dragController = FindAnyObjectByType<DragController>();
        }

        if (tooltip == null)
        {
            tooltip = FindAnyObjectByType<ItemTooltipUI>();
        }

        // cache player transform once
        if (player == null)
        {
            var p = GameObject.FindWithTag("Player");
            if (p) player = p.transform;
        }

        // make sure each slot knows its owner + tooltip
        foreach (var s in slotUIs)
        {
            if (!s) continue;
            s.owner = this;
            s.tooltip = tooltip;
        }

        Refresh();
    }

    void Update()
    {
        // autoclose if we wander away
        if (!_closing && player && corpse)
        {
            float dist = Vector3.Distance(player.position, corpse.transform.position);
            if (dist > autoCloseDistance)
            {
                Close();
            }
        }
    }

    // called by CorpseLoot after Bind, and also by itself later
    public void Refresh()
    {
        RebuildSlots();
    }

    // --------------------------------------------------
    // Fill the baked slots (money first, then items)
    // --------------------------------------------------
    void RebuildSlots()
    {
        if (slotUIs == null || slotUIs.Count == 0) return;

        // Hide everything first
        foreach (var s in slotUIs)
        {
            if (s != null)
                s.HideMe();
        }

        if (!corpse) return;

        int writeIndex = 0;

        // 1) money slot (if corpse still has dollars)
        if (corpse.dollars > 0 && writeIndex < slotUIs.Count)
        {
            var slot = slotUIs[writeIndex];
            if (slot != null)
            {
                slot.ShowMoney(this, corpse.dollars, moneyIcon);
            }
            writeIndex++;
        }

        // 2) item slots
        for (int i = 0; i < corpse.items.Count && writeIndex < slotUIs.Count; i++)
        {
            var inst = corpse.items[i];
            if (inst == null) continue;

            var slot = slotUIs[writeIndex];
            if (slot != null)
            {
                slot.ShowItem(this, i, inst);
            }

            writeIndex++;
        }

        // extra baked slots after writeIndex stay hidden
    }

    // --------------------------------------------------
    // Player clicked an *item* slot
    // --------------------------------------------------
    public void TakeItem(int corpseIndex)
    {
        if (!corpse) return;

        var itm = corpse.LootItem(corpseIndex); // remove from corpse list
        if (itm != null)
        {
            playerInventory.Add(itm, 1);
        }

        AfterLootAttempt();
    }

    // --------------------------------------------------
    // Player clicked the *money* slot
    // --------------------------------------------------
    public void TakeMoney()
    {
        if (!corpse) return;

        int amt = corpse.LootMoney(); // zeroes corpse.dollars
        if (amt > 0)
        {
            wallet.Add(amt);
        }

        AfterLootAttempt();
    }

    void AfterLootAttempt()
    {
        if (!corpse) return;

        if (corpse.IsEmpty)
        {
            Close();
            return;
        }

        RebuildSlots();
    }

    // Drag integration hook if you add drag-from-loot later
    public void NotifyDragEnded()
    {
        if (!corpse) return;

        if (corpse.IsEmpty)
        {
            Close();
        }
        else
        {
            RebuildSlots();
        }
    }

    public void Close()
    {
        if (_closing) return;
        _closing = true;

        // VERY IMPORTANT: hide tooltip so it doesn't stick forever
        if (tooltip != null)
        {
            tooltip.Hide();
        }

        Destroy(gameObject);
    }
}
