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
    public EquipmentManager cursorOwner;
    public ScreenToast toast;

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

        // pull cursor owner from the corpse if we don't already have one
        if (cursorOwner == null)
        {
            cursorOwner = owner.cursorOwner;
        }

        // If these weren't assigned in prefab (scene refs), grab them now
        if (dragController == null)
        {
            dragController = FindAnyObjectByType<DragController>();
        }

        if (tooltip == null)
        {
            tooltip = FindAnyObjectByType<ItemTooltipUI>();
        }

        if (toast == null)
        toast = FindAnyObjectByType<ScreenToast>();

        // cache player transform once
        if (player == null)
        {
            var p = GameObject.FindWithTag("Player");
            if (p) player = p.transform;
        }

        // make sure each slot knows its owner + tooltip + cursor
        foreach (var s in slotUIs)
        {
            if (!s) continue;
            s.owner = this;
            s.tooltip = tooltip;
            s.cursorOwner = cursorOwner;
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
        // kill any stale tooltip/cursor before we refresh slots
        if (tooltip) tooltip.Hide();
        if (cursorOwner) cursorOwner.RestoreCursor();

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

        // clear UI state before we mutate slots
        if (tooltip) tooltip.Hide();
        if (cursorOwner) cursorOwner.RestoreCursor();
        UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);

        // Remove from corpse *temporarily*
        var itm = corpse.LootItem(corpseIndex);
        if (itm != null)
        {
            bool added = playerInventory.Add(itm, 1);

            if (!added)
            {
                // Inventory full: put it back on the corpse
                corpse.PutBackItem(corpseIndex, itm);

                if (toast)
                    toast.Show("My backpack is full.", Color.yellow);

                RebuildSlots();
                return;
            }
        }

        AfterLootAttempt();
    }

    // --------------------------------------------------
    // Player clicked the *money* slot
    // --------------------------------------------------
    public void TakeMoney()
    {
        if (!corpse) return;

        // clear UI state before we mutate slots
        if (tooltip) tooltip.Hide();
        if (cursorOwner) cursorOwner.RestoreCursor();
        UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);

        int amt = corpse.LootMoney();
        if (amt > 0) wallet.Add(amt);
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

        // hide tooltip so it doesn't get stuck
        if (tooltip != null)
        {
            tooltip.Hide();
        }

        // restore cursor in case we were hovering a loot slot
        if (cursorOwner != null)
        {
            cursorOwner.RestoreCursor();
        }

        // also clear any custom cursor we set from the corpse hover path
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);

        Destroy(gameObject);
    }
}
