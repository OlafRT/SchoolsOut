using UnityEngine;

public class EquipmentUI : MonoBehaviour {
    public EquipmentManager equipMgr; public ItemTooltipUI tooltip;
    public ItemSlotUI head, neck, ringL, ringR, weapon, trinket;
    // Upgrades column (4 slots)
    public ItemSlotUI up1, up2, up3, up4;

    private void OnEnable(){
        equipMgr.equipment.OnEquipmentChanged += Refresh;
        head.BindEquipment(equipMgr.equipment, EquipSlot.Head, equipMgr, tooltip);
        neck.BindEquipment(equipMgr.equipment, EquipSlot.Neck, equipMgr, tooltip);
        ringL.BindEquipment(equipMgr.equipment, EquipSlot.RingLeft, equipMgr, tooltip);
        ringR.BindEquipment(equipMgr.equipment, EquipSlot.RingRight, equipMgr, tooltip);
        weapon.BindEquipment(equipMgr.equipment, EquipSlot.Weapon, equipMgr, tooltip);
        trinket.BindEquipment(equipMgr.equipment, EquipSlot.Trinket, equipMgr, tooltip);
        if(up1) up1.BindEquipment(equipMgr.equipment, EquipSlot.Upgrade1, equipMgr, tooltip);
        if(up2) up2.BindEquipment(equipMgr.equipment, EquipSlot.Upgrade2, equipMgr, tooltip);
        if(up3) up3.BindEquipment(equipMgr.equipment, EquipSlot.Upgrade3, equipMgr, tooltip);
        if(up4) up4.BindEquipment(equipMgr.equipment, EquipSlot.Upgrade4, equipMgr, tooltip);
        Refresh();
    }

    private void OnDisable(){ equipMgr.equipment.OnEquipmentChanged -= Refresh; }

    private void Refresh(){ head.Refresh(); neck.Refresh(); ringL.Refresh(); ringR.Refresh(); weapon.Refresh(); trinket.Refresh(); if(up1) up1.Refresh(); if(up2) up2.Refresh(); if(up3) up3.Refresh(); if(up4) up4.Refresh(); }
}