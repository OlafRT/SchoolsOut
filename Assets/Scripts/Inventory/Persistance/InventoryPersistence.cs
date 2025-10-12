using System;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

[Serializable] public class ItemInstanceData { public string templateId; public int itemLevel; public int requiredLevel; public Rarity rarity; public AffixType affix; public int bonusMuscles; public int bonusIQ; public int bonusCrit; public int bonusToughness; public int value; }
[Serializable] public class InventoryData { public int capacity; public List<SlotData> slots = new(); }
[Serializable] public class SlotData { public bool occupied; public ItemInstanceData item; }
[Serializable] public class EquipmentData { public List<EquipEntryData> equipped = new(); }
[Serializable] public class EquipEntryData { public EquipSlot slot; public bool occupied; public ItemInstanceData item; }
[Serializable] public class WalletData { public int dollars; }

public static class InventoryPersistence {
    public static void SaveInventory(Inventory inv, string filePath) {
        var data = new InventoryData { capacity = inv.capacity };
        foreach (var s in inv.Slots) {
            var sd = new SlotData { occupied = !s.IsEmpty };
            if (sd.occupied) sd.item = ToData(s.item);
            data.slots.Add(sd);
        }
        File.WriteAllText(filePath, JsonUtility.ToJson(data, true));
    }

    public static void LoadInventory(Inventory inv, ItemDatabase db, string filePath) {
        if (!File.Exists(filePath)) return;
        var json = File.ReadAllText(filePath);
        var data = JsonUtility.FromJson<InventoryData>(json);

        inv.capacity = data.capacity;

        var list = new List<ItemStack>();
        foreach (var sd in data.slots) {
            if (sd.occupied && sd.item != null) list.Add(new ItemStack(FromData(sd.item, db), 1));
            else list.Add(new ItemStack(null, 0));
        }

        var field = typeof(Inventory).GetField("slots",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field.SetValue(inv, list);

        // notify via helper
        inv.MarkDirty();
    }

    public static void SaveEquipment(EquipmentState eq, string filePath) {
        var data = new EquipmentData();
        foreach (var e in eq.equipped) {
            var ed = new EquipEntryData { slot = e.slot, occupied = (e.item != null) };
            if (ed.occupied) ed.item = ToData(e.item);
            data.equipped.Add(ed);
        }
        File.WriteAllText(filePath, JsonUtility.ToJson(data, true));
    }

    public static void LoadEquipment(EquipmentState eq, ItemDatabase db, string filePath) {
        if (!File.Exists(filePath)) return;
        var json = File.ReadAllText(filePath);
        var data = JsonUtility.FromJson<EquipmentData>(json);

        for (int i = 0; i < eq.equipped.Count; i++) {
            var slot = eq.equipped[i].slot;
            var src = data.equipped.Find(x => x.slot == slot);
            eq.equipped[i].item = (src != null && src.occupied && src.item != null) ? FromData(src.item, db) : null;
        }

        // notify via helper
        eq.NotifyChanged();
    }

    public static void SaveWallet(PlayerWallet wallet, string filePath) {
        var data = new WalletData { dollars = wallet.dollars };
        File.WriteAllText(filePath, JsonUtility.ToJson(data, true));
    }

    public static void LoadWallet(PlayerWallet wallet, string filePath) {
        if (!File.Exists(filePath)) return;
        var json = File.ReadAllText(filePath);
        var data = JsonUtility.FromJson<WalletData>(json);
        wallet.dollars = Mathf.Max(0, data.dollars);

        // notify via helper
        wallet.NotifyChanged();
    }

    static ItemInstanceData ToData(ItemInstance it) {
        return new ItemInstanceData {
            templateId     = it.template?.id,
            itemLevel      = it.itemLevel,
            requiredLevel  = it.requiredLevel,
            rarity         = it.rarity,
            affix          = it.affix,
            bonusMuscles   = it.bonusMuscles,
            bonusIQ        = it.bonusIQ,
            bonusCrit      = it.bonusCrit,
            bonusToughness = it.bonusToughness,
            value          = it.value,
        };
    }

    static ItemInstance FromData(ItemInstanceData d, ItemDatabase db) {
        var tpl = db?.Get(d.templateId);
        return new ItemInstance {
            template       = tpl,
            itemLevel      = d.itemLevel,
            requiredLevel  = d.requiredLevel,
            rarity         = d.rarity,
            affix          = d.affix,
            bonusMuscles   = d.bonusMuscles,
            bonusIQ        = d.bonusIQ,
            bonusCrit      = d.bonusCrit,
            bonusToughness = d.bonusToughness,
            value          = d.value
        };
    }
}
