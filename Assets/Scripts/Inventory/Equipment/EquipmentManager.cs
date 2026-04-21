using UnityEngine;

public class EquipmentManager : MonoBehaviour {
    [Header("References")] public Inventory inventory; public EquipmentState equipment;
    [Header("Player/Feedback")] public PlayerStats player; public ScreenToast toast;
    public Texture2D handCursor, grabCursor; public Vector2 cursorHotspot = new(8,8);

    int GetPlayerLevel(){
        if (!player) return 1;
        var t = player.GetType();
        var f = t.GetField("level");             if (f!=null && f.FieldType==typeof(int)) return (int)f.GetValue(player);
        var p = t.GetProperty("Level");          if (p!=null && p.PropertyType==typeof(int)) return (int)p.GetValue(player, null);
        var f2= t.GetField("currentLevel");      if (f2!=null && f2.FieldType==typeof(int)) return (int)f2.GetValue(player);
        return 1;
    }

    void DenyEquip(){
        if (toast) toast.Show("I'm not high enough level to use that yet.", Color.red);
    }

    void DenyClass(PlayerStats.PlayerClass required){
        if (toast) toast.Show($"Only a {required} can equip that.", Color.red);
    }

    public bool TryEquipFromInventory(int inventoryIndex){
        var s = inventory.Slots[inventoryIndex];
        if (s.IsEmpty || s.item==null || s.item.template==null || !s.item.template.isEquippable) return false;

        // level gate
        if (GetPlayerLevel() < s.item.requiredLevel){ DenyEquip(); return false; }

        // class gate
        if (s.item.template.hasClassRestriction && player.playerClass != s.item.template.requiredClass)
            { DenyClass(s.item.template.requiredClass); return false; }

        EquipSlot targetSlot = (s.item.template.itemType == ItemType.Upgrade)
            ? (equipment.FirstEmptyUpgradeSlot() ?? EquipmentState.UpgradeSlots[0])
            : s.item.template.equipSlot;

        var previously = equipment.Swap(targetSlot, s.item);
        // Free the source slot FIRST so it's available if the bag is otherwise full.
        // Doing Add before RemoveAt meant the slot wasn't free yet — if the bag had no
        // other empty slots, Add would silently fail and the previously-equipped item
        // would be lost with no feedback.
        inventory.RemoveAt(inventoryIndex, 1);
        if (previously != null && !inventory.Add(previously, 1))
        {
            // No space even after freeing the slot — roll back and tell the player.
            equipment.Swap(targetSlot, previously);
            inventory.Add(s.item, 1);
            if (toast) toast.Show("My backpack is full.", Color.red);
            return false;
        }
        TutorialEvents.RaiseEquippedItem();
        return true;
    }

    public bool TryUnequipToInventory(EquipSlot slot){
        var itm = equipment.Get(slot); if(itm==null) return false;
        // If the item has a null template (e.g. loaded from a save where the template
        // ID no longer exists), adding it to inventory would create a ghost slot —
        // a slot that IsEmpty returns false for but displays nothing. Strip it silently.
        if (itm.template == null) { equipment.Unequip(slot); return true; }
        if(inventory.Add(itm,1)) { equipment.Unequip(slot); return true; }
        return false;
    }

    // (optional) cursor helpers you can call from slots/drag controller
    public void SetHoverCursor(){ if (handCursor) Cursor.SetCursor(handCursor, cursorHotspot, CursorMode.Auto); }
    public void SetGrabCursor(){ var tex = grabCursor ? grabCursor : handCursor; if (tex) Cursor.SetCursor(tex, cursorHotspot, CursorMode.Auto); }
    public void RestoreCursor(){ Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto); }
}