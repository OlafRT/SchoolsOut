using System;
using System.Collections.Generic;
using UnityEngine;

public static class AffixRoller {
    // Rolls an instance from a template & item level using your rules + rarity effects
    public static ItemInstance Roll(ItemTemplate tpl, int itemLevel){
        var inst = new ItemInstance{ template = tpl, itemLevel = Mathf.Clamp(itemLevel,1,30) };
        inst.requiredLevel = ItemLevelRules.RequiredLevelForItemLevel(inst.itemLevel);

        // 1) Rarity (gated & weighted by iLvl)
        inst.rarity = RarityDropTable.RollRarity(inst.itemLevel);

        // 2) Base Toughness always equals iLvl
        inst.bonusToughness = inst.itemLevel;

        // 3) Pick affix from template rules
        var list = tpl.allowedAffixes;
        inst.affix = list[UnityEngine.Random.Range(0, list.Count)];

        // 4) Roll main stats from affix budget (budget = iLvl)
        switch(inst.affix){
            case AffixType.Athlete:   inst.bonusMuscles = inst.itemLevel; break;
            case AffixType.Scholar:   inst.bonusIQ = inst.itemLevel; break;
            case AffixType.Lucky:     inst.bonusCrit = inst.itemLevel; break;
            case AffixType.Power:     Split(inst.itemLevel, out inst.bonusMuscles, out inst.bonusCrit); break;
            case AffixType.Cognition: Split(inst.itemLevel, out inst.bonusIQ, out inst.bonusCrit); break;
        }

        // 5) Apply rarity-based stat modifiers
        ApplyRarityModifiers(inst);

        // 6) Compute $ value from rarity + item level
        inst.value = PriceCalculator.Evaluate(inst);
        return inst;
    }

    static void Split(int budget, out int a, out int b){ a = UnityEngine.Random.Range(1, budget); b = budget - a; }

    static void ApplyRarityModifiers(ItemInstance i){
        var stats = new List<(Action<int> add, Func<int> get)>(4){
            (v=> i.bonusMuscles += v, ()=> i.bonusMuscles),
            (v=> i.bonusIQ      += v, ()=> i.bonusIQ),
            (v=> i.bonusCrit    += v, ()=> i.bonusCrit),
            (v=> i.bonusToughness += v, ()=> i.bonusToughness)
        };
        var present = stats.FindAll(s => s.get() > 0);
        if(present.Count == 0) return;

        switch(i.rarity){
            case Rarity.Poor:
                present[UnityEngine.Random.Range(0, present.Count)].add(-1);
                i.bonusMuscles = Mathf.Max(0, i.bonusMuscles);
                i.bonusIQ = Mathf.Max(0, i.bonusIQ);
                i.bonusCrit = Mathf.Max(0, i.bonusCrit);
                i.bonusToughness = Mathf.Max(0, i.bonusToughness);
                break;
            case Rarity.Common: break; // neutral
            case Rarity.Uncommon: present[UnityEngine.Random.Range(0, present.Count)].add(+1); break;
            case Rarity.Rare:     present[UnityEngine.Random.Range(0, present.Count)].add(+2); break;
            case Rarity.Epic:     present[UnityEngine.Random.Range(0, present.Count)].add(+3); break;
            case Rarity.Legendary:
                foreach(var s in present) s.add(+4);
                break;
        }
    }
}
