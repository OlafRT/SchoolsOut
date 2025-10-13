using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class DragController : MonoBehaviour
{
    [Header("Refs")]
    public Canvas canvas;
    public EquipmentManager equipMgr;
    public Inventory inventory;
    public ItemTooltipUI tooltip;
    public DestroyConfirmPanel destroyConfirm;

    [Header("Ghost Icon")]
    public Image ghostIcon;
    [Range(0f, 1f)] public float ghostAlpha = 0.8f;

    // Added Loot source
    public enum Source { None, Bag, Equip, Loot }
    public bool IsDragging => _dragActive;

    public Source from = Source.None;
    public int fromBagIndex = -1;
    public EquipSlot fromEquipSlot;

    // Loot tracking
    public LootUI activeLootUI;
    public int fromLootIndex = -1;

    bool _dragActive;
    bool _handledDrop;

    void Awake()
    {
        if (!canvas) canvas = GetComponentInParent<Canvas>();
        if (!ghostIcon)
        {
            var go = new GameObject("DragGhost", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            go.transform.SetParent(canvas.transform, false);
            ghostIcon = go.GetComponent<Image>();
            ghostIcon.raycastTarget = false;
            var cg = go.GetComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;
            go.SetActive(false);
        }
    }

    // -------- Begin Drag (Bag / Equip / Loot) --------

    public void BeginDragFromBag(int bagIndex, Sprite icon)
    {
        var s = inventory?.Slots?[bagIndex];
        if (s == null || s.IsEmpty || icon == null) return;   // no empty/white drags
        from = Source.Bag;
        fromBagIndex = bagIndex;
        StartGhost(icon);
    }

    public void BeginDragFromEquip(EquipSlot slot, Sprite icon)
    {
        var itm = equipMgr?.equipment?.Get(slot);
        if (itm == null || icon == null) return;
        from = Source.Equip;
        fromEquipSlot = slot;
        StartGhost(icon);
    }

    public void BeginDragFromLoot(int lootIndex, Sprite icon, LootUI ownerUI)
    {
        if (icon == null || ownerUI == null) return;
        from = Source.Loot;
        fromLootIndex = lootIndex;
        activeLootUI = ownerUI;
        StartGhost(icon);
    }

    void StartGhost(Sprite icon)
    {
        _dragActive = true;
        _handledDrop = false;
        tooltip?.Hide();

        ghostIcon.sprite = icon;
        ghostIcon.color = new Color(1, 1, 1, ghostAlpha);
        ghostIcon.gameObject.SetActive(true);
        ghostIcon.transform.SetAsLastSibling();
        ghostIcon.transform.position = Input.mousePosition;

        equipMgr?.SetGrabCursor();
    }

    public void UpdateGhost(PointerEventData eventData)
    {
        if (_dragActive && ghostIcon) ghostIcon.transform.position = eventData.position;
    }

    // -------- End Drag helpers --------

    public void EndDragFromLoot()
    {
        if (!_dragActive) return;
        // If not dropped on a valid target, just cancel (do NOT destroy corpse loot)
        CancelDrag();
    }

    public void CancelDrag()
    {
        _dragActive = false;
        from = Source.None;
        fromBagIndex = -1;
        fromEquipSlot = default;
        fromLootIndex = -1;
        activeLootUI = null;

        if (ghostIcon) ghostIcon.gameObject.SetActive(false);
        equipMgr?.RestoreCursor();
    }

    // -------- Drop targets (called by ItemSlotUI / LootSlotUI) --------

    public void DropOnBag(int toIndex)
    {
        if (!_dragActive) return;
        _handledDrop = true;

        switch (from)
        {
            case Source.Bag:
                inventory.Move(fromBagIndex, toIndex);
                break;
            case Source.Equip:
                equipMgr.TryUnequipToInventory(fromEquipSlot);
                break;
            case Source.Loot:
                if (activeLootUI != null)
                    activeLootUI.TakeItem(fromLootIndex);
                break;
        }

        CancelDrag();
    }

    public void DropOnEquip(EquipSlot toSlot)
    {
        if (!_dragActive) return;
        _handledDrop = true;

        if (from == Source.Bag)
        {
            equipMgr.TryEquipFromInventory(fromBagIndex);
        }
        else if (from == Source.Equip)
        {
            var eq = equipMgr.equipment;
            var fromItem = eq.Get(fromEquipSlot);
            if (fromItem != null)
            {
                bool isUpgrade = (fromItem.template != null && fromItem.template.itemType == ItemType.Upgrade);
                if (isUpgrade)
                {
                    bool toIsUpgrade =
                        toSlot == EquipSlot.Upgrade1 || toSlot == EquipSlot.Upgrade2 ||
                        toSlot == EquipSlot.Upgrade3 || toSlot == EquipSlot.Upgrade4;
                    if (toIsUpgrade)
                    {
                        var prev = eq.Swap(toSlot, fromItem);
                        eq.Swap(fromEquipSlot, prev);
                    }
                }
                else if (fromItem.template != null && fromItem.template.equipSlot == toSlot)
                {
                    var prev = eq.Swap(toSlot, fromItem);
                    eq.Swap(fromEquipSlot, prev);
                }
            }
        }
        else if (from == Source.Loot)
        {
            // Just take to inventory; equip via click path if you want
            if (activeLootUI != null)
                activeLootUI.TakeItem(fromLootIndex);
        }

        CancelDrag();
    }

    // -------- Called by ItemSlotUI.OnEndDrag (for bag/equip) --------

    public void EndDragPotentiallyDestroy()
    {
        if (!_dragActive) return;

        // already dropped on a valid slot?
        if (_handledDrop) { CancelDrag(); return; }

        // Off-target drop: only offer destroy if origin was the bag and the slot still has an item
        if (from == Source.Bag && inventory != null &&
            fromBagIndex >= 0 && fromBagIndex < inventory.Slots.Count)
        {
            var s = inventory.Slots[fromBagIndex];
            if (!s.IsEmpty && destroyConfirm != null)
            {
                // CAPTURE the index & name BEFORE we cancel/reset state
                int idx = fromBagIndex;
                string name = s.item?.DisplayName ?? "this item";

                // hide ghost & restore cursor, but DO NOT rely on fromBagIndex after this
                CancelDrag();

                destroyConfirm.Show(
                    $"Do you want to destroy {name}?",
                    // YES:
                    () => { inventory.RemoveAt(idx, 1); },
                    // NO:
                    () => { /* no-op; item stays */ }
                );
                return;
            }
        }

        // Otherwise just cancel
        CancelDrag();
    }
}

