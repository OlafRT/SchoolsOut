using System;
using System.Collections.Generic;
using UnityEngine;

public static class AffixRoller
{
    public static ItemInstance CreateFromTemplate(ItemTemplate tpl, int itemLevel)
    {
        if (tpl == null) return null;

        // Hand-authored / quest / unique / fixed-drop item?
        if (tpl.isStaticItem)
        {
            return BuildStatic(tpl);
        }

        // Otherwise: normal randomized loot roll
        return Roll(tpl, itemLevel);
    }
    // -------------------------------------------------
    // RANDOM DROP / RNG ITEM
    // -------------------------------------------------
    public static ItemInstance Roll(ItemTemplate tpl, int itemLevel)
    {
        var inst = new ItemInstance {
            template   = tpl,
            itemLevel  = Mathf.Clamp(itemLevel, 1, 30),
        };

        inst.requiredLevel = ItemLevelRules.RequiredLevelForItemLevel(inst.itemLevel);

        // 1) Pick rarity
        inst.rarity = RarityDropTable.RollRarity(inst.itemLevel);

        // 2) Base toughness always = ilvl
        inst.bonusToughness = inst.itemLevel;

        // 3) Are we allowed to have a "magical" affix?
        bool allowMagicAffix =
            inst.rarity == Rarity.Uncommon ||
            inst.rarity == Rarity.Rare     ||
            inst.rarity == Rarity.Epic     ||
            inst.rarity == Rarity.Legendary;

        if (allowMagicAffix &&
            tpl.allowedAffixes != null &&
            tpl.allowedAffixes.Count > 0)
        {
            // roll an affix
            inst.affix = tpl.allowedAffixes[UnityEngine.Random.Range(0, tpl.allowedAffixes.Count)];

            // spend ilvl budget into stats based on which affix we got
            switch (inst.affix)
            {
                case AffixType.Athlete:
                    inst.bonusMuscles = inst.itemLevel;
                    break;

                case AffixType.Scholar:
                    inst.bonusIQ = inst.itemLevel;
                    break;

                case AffixType.Lucky:
                    inst.bonusCrit = inst.itemLevel;
                    break;

                case AffixType.Power:
                    Split(inst.itemLevel, out inst.bonusMuscles, out inst.bonusCrit);
                    break;

                case AffixType.Cognition:
                    Split(inst.itemLevel, out inst.bonusIQ, out inst.bonusCrit);
                    break;

                default:
                    inst.affix = AffixType.None;
                    break;
            }
        }
        else
        {
            // Poor / Common
            inst.affix = AffixType.None;

            // Common can get a tiny pity stat so it's not 100% garbage
            if (inst.rarity == Rarity.Common)
            {
                int roll = UnityEngine.Random.Range(0, 4); // 0..3
                switch (roll)
                {
                    case 0: inst.bonusMuscles = 1; break;
                    case 1: inst.bonusIQ = 1; break;
                    case 2: inst.bonusCrit = 1; break;
                    // case 3: nothing extra
                }
            }
            // Poor = just toughness baseline, no extra
        }

        // 4) Apply rarity bonuses / penalties
        ApplyRarityModifiers(inst);

        // 5) Price
        inst.value = PriceCalculator.Evaluate(inst);

        return inst;
    }

    // helper to split ilvl budget into 2 stats for Power/Cognition
    static void Split(int budget, out int a, out int b)
    {
        a = UnityEngine.Random.Range(1, budget); // at least 1
        b = budget - a;
    }

    static void ApplyRarityModifiers(ItemInstance i)
    {
        // collect all stats that are >0 so we know what exists on this item
        var stats = new List<(Action<int> add, Func<int> get)>{
            (v=> i.bonusMuscles   += v, () => i.bonusMuscles),
            (v=> i.bonusIQ        += v, () => i.bonusIQ),
            (v=> i.bonusCrit      += v, () => i.bonusCrit),
            (v=> i.bonusToughness += v, () => i.bonusToughness),
        };

        var present = stats.FindAll(s => s.get() > 0);
        if (present.Count == 0)
            return;

        switch (i.rarity)
        {
            case Rarity.Poor:
                // Pick one stat and nerf it by 1 (so trash feels bad)
                present[UnityEngine.Random.Range(0, present.Count)].add(-1);
                i.bonusMuscles   = Mathf.Max(0, i.bonusMuscles);
                i.bonusIQ        = Mathf.Max(0, i.bonusIQ);
                i.bonusCrit      = Mathf.Max(0, i.bonusCrit);
                i.bonusToughness = Mathf.Max(0, i.bonusToughness);
                break;

            case Rarity.Common:
                // Neutral. (Tiny pity stat already added above if any)
                break;

            case Rarity.Uncommon:
                present[UnityEngine.Random.Range(0, present.Count)].add(+1);
                break;

            case Rarity.Rare:
                present[UnityEngine.Random.Range(0, present.Count)].add(+2);
                break;

            case Rarity.Epic:
                present[UnityEngine.Random.Range(0, present.Count)].add(+3);
                break;

            case Rarity.Legendary:
                // Legendary = +4 to ALL stats that exist
                foreach (var s in present)
                    s.add(+4);
                break;
        }
    }

    // -------------------------------------------------
    // STATIC / HANDCRAFTED ITEM
    // -------------------------------------------------
    public static ItemInstance BuildStatic(ItemTemplate tpl)
    {
        var inst = new ItemInstance {
            template        = tpl,
            // we just take your fixed values
            customName      = string.IsNullOrWhiteSpace(tpl.overrideName) ? null : tpl.overrideName.Trim(),
            itemLevel       = Mathf.Clamp(tpl.fixedItemLevel, 1, 30),
            requiredLevel   = tpl.fixedRequiredLevel,
            rarity          = tpl.fixedRarity,
            affix           = tpl.forcedAffix,

            bonusMuscles    = tpl.fixedMuscles,
            bonusIQ         = tpl.fixedIQ,
            bonusCrit       = tpl.fixedCrit,
            bonusToughness  = tpl.fixedToughness,
        };

        // price
        if (tpl.fixedValue > 0)
        {
            inst.value = tpl.fixedValue;
        }
        else
        {
            // cheap fallback pricing if you didn't hand-set value
            inst.value = PriceCalculator.EvaluateDummy(tpl.fixedRarity, tpl.fixedItemLevel);
        }

        return inst;
    }
}