using System.Collections.Generic;
using UnityEngine;

public static class PriceCalculator {
    const int BasePerIlvl = 10; // each ilvl ~ $10 for Common
    static readonly Dictionary<Rarity, float> rarityMult = new(){
        { Rarity.Poor, 0.5f },
        { Rarity.Common, 1.0f },
        { Rarity.Uncommon, 1.5f },
        { Rarity.Rare, 3.0f },
        { Rarity.Epic, 7.0f },
        { Rarity.Legendary, 20.0f },
    };

    public static int Evaluate(ItemInstance inst){
        float mult = rarityMult.TryGetValue(inst.rarity, out var m) ? m : 1f;
        int dollars = Mathf.RoundToInt(BasePerIlvl * inst.itemLevel * mult);
        return Mathf.Max(1, dollars);
    }
}