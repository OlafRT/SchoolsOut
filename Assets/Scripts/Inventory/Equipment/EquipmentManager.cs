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

    public bool TryEquipFromInventory(int inventoryIndex){
        var s = inventory.Slots[inventoryIndex];
        if (s.IsEmpty || s.item==null || s.item.template==null || !s.item.template.isEquippable) return false;

        // level gate
        if (GetPlayerLevel() < s.item.requiredLevel){ DenyEquip(); return false; }

        EquipSlot targetSlot = (s.item.template.itemType == ItemType.Upgrade)
            ? (equipment.FirstEmptyUpgradeSlot() ?? EquipmentState.UpgradeSlots[0])
            : s.item.template.equipSlot;

        var previously = equipment.Swap(targetSlot, s.item);
        if(previously!=null) inventory.Add(previously, 1);
        inventory.RemoveAt(inventoryIndex, 1);
        return true;
    }

    public bool TryUnequipToInventory(EquipSlot slot){
        var itm = equipment.Get(slot); if(itm==null) return false;
        if(inventory.Add(itm,1)) { equipment.Unequip(slot); return true; }
        return false;
    }

    // (optional) cursor helpers you can call from slots/drag controller
    public void SetHoverCursor(){ if (handCursor) Cursor.SetCursor(handCursor, cursorHotspot, CursorMode.Auto); }
    public void SetGrabCursor(){ var tex = grabCursor ? grabCursor : handCursor; if (tex) Cursor.SetCursor(tex, cursorHotspot, CursorMode.Auto); }
    public void RestoreCursor(){ Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto); }
}
