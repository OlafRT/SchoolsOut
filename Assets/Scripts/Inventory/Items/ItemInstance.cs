using System;
using UnityEngine;

[Serializable]
public class ItemInstance {
    public ItemTemplate template;
    [Range(1,30)] public int itemLevel = 1; // drives stat budget
    [Min(1)] public int requiredLevel = 1;
    public Rarity rarity; // rolled per instance (overrides template visual)
    public AffixType affix;

    // Rolled bonuses (your game stats)
    public int bonusMuscles;   // "Athlete"
    public int bonusIQ;        // "Scholar"
    public int bonusCrit;      // percentage points (7 => +7%)
    public int bonusToughness; // always-on toughness per your rule

    // Economy
    public int value; // stored in whole dollars; UI prints with '$'

    public string DisplayName =>
    (template == null)
        ? "(Unknown Item)"
        : (string.IsNullOrEmpty(AffixSuffix) ? template.baseName : $"{template.baseName} {AffixSuffix}");
    public string AffixSuffix => affix switch {
        AffixType.Athlete => "of the Athlete",
        AffixType.Scholar => "of the Scholar",
        AffixType.Lucky => "of the Lucky",
        AffixType.Power => "of Power",
        AffixType.Cognition => "of Cognition",
        _ => string.Empty
    };

    public Sprite Icon => template != null ? template.icon : null;
}
