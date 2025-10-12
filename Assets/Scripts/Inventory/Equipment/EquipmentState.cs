using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "RPG/Equipment", fileName = "PlayerEquipment")]
public class EquipmentState : ScriptableObject {
    [Serializable] public class EquippedEntry { public EquipSlot slot; public ItemInstance item; }

    public static readonly EquipSlot[] UpgradeSlots = {
        EquipSlot.Upgrade1, EquipSlot.Upgrade2, EquipSlot.Upgrade3, EquipSlot.Upgrade4
    };

    public List<EquippedEntry> equipped = new List<EquippedEntry>(){
        new EquippedEntry{slot=EquipSlot.Head},
        new EquippedEntry{slot=EquipSlot.Neck},
        new EquippedEntry{slot=EquipSlot.RingLeft},
        new EquippedEntry{slot=EquipSlot.RingRight},
        new EquippedEntry{slot=EquipSlot.Weapon},
        new EquippedEntry{slot=EquipSlot.Trinket},
        new EquippedEntry{slot=EquipSlot.Upgrade1},
        new EquippedEntry{slot=EquipSlot.Upgrade2},
        new EquippedEntry{slot=EquipSlot.Upgrade3},
        new EquippedEntry{slot=EquipSlot.Upgrade4},
    };

    public event Action OnEquipmentChanged;

    public void NotifyChanged() { OnEquipmentChanged?.Invoke(); }

    public ItemInstance Get(EquipSlot slot){ var e = equipped.Find(x=>x.slot==slot); return e?.item; }
    public ItemInstance Unequip(EquipSlot slot){ var e = equipped.Find(x=>x.slot==slot); if(e==null) return null; var prev = e.item; e.item = null; OnEquipmentChanged?.Invoke(); return prev; }
    public ItemInstance Swap(EquipSlot slot, ItemInstance newItem){ var e = equipped.Find(x=>x.slot==slot); if(e==null) return null; var prev = e.item; e.item = newItem; OnEquipmentChanged?.Invoke(); return prev; }

    public EquipSlot? FirstEmptyUpgradeSlot(){ foreach(var s in UpgradeSlots) if(Get(s)==null) return s; return null; }

    public (int muscles,int iq,int crit,int toughness) GetTotalBonuses(){
        int m=0,i=0,c=0,t=0; foreach(var e in equipped){ if(e.item!=null){ m+=e.item.bonusMuscles; i+=e.item.bonusIQ; c+=e.item.bonusCrit; t+=e.item.bonusToughness; } }
        return (m,i,c,t);
    }
}