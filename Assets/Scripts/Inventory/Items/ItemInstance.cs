using System;
using UnityEngine;

[Serializable]
public class ItemInstance {
    public ItemTemplate template;

    // optional override for unique items / quest rewards
    public string customName;

    [Range(1,30)] public int itemLevel = 1;        // drives stat budget
    [Min(1)] public int requiredLevel = 1;
    public Rarity rarity;                          // rolled per instance
    public AffixType affix;                        // AffixType.None = no suffix

    // Rolled bonuses (your game stats)
    public int bonusMuscles;    // +Muscle
    public int bonusIQ;         // +IQ
    public int bonusCrit;       // +Crit %
    public int bonusToughness;  // +Toughness (flat)

    // Economy
    public int value; // sell value in dollars

    // What name should we show in UI / tooltip?
    public string DisplayName {
        get {
            // 1. If designer forced a name (customName), use that.
            if (!string.IsNullOrEmpty(customName))
                return customName;

            // 2. Otherwise use baseName (+ suffix if there IS a suffix)
            if (template == null)
                return "(Unknown Item)";

            if (string.IsNullOrEmpty(AffixSuffix))
                return template.baseName;

            return $"{template.baseName} {AffixSuffix}";
        }
    }

    // The suffix part, based on affix
    public string AffixSuffix => affix switch {
        AffixType.Athlete    => "of the Athlete",
        AffixType.Scholar    => "of the Scholar",
        AffixType.Lucky      => "of the Lucky",
        AffixType.Power      => "of Power",
        AffixType.Cognition  => "of Cognition",
        _ => string.Empty    // AffixType.None, etc.
    };

    public Sprite Icon => template != null ? template.icon : null;
}
