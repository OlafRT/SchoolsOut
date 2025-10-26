using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "RPG/Item Template", fileName = "NewItemTemplate")]
public class ItemTemplate : ScriptableObject {
    [Header("Identity")]
    public string id;
    [Tooltip("Base display name before affix")]
    public string baseName = "Wizard Hat";
    public Sprite icon;
    public Rarity rarity = Rarity.Common;
    public ItemType itemType = ItemType.Equipment;

    [Header("Equip")]
    public bool isEquippable = true;
    public EquipSlot equipSlot;

    [Header("Allowed Affixes")]
    public List<AffixType> allowedAffixes = new(){
        AffixType.Athlete,
        AffixType.Scholar,
        AffixType.Lucky,
        AffixType.Power,
        AffixType.Cognition
    };

    [Header("Tooltip")]
    [TextArea(2,6)] public string description;

    // NEW SECTION: for handcrafted / static items
    [Header("Generation Rules")]
    public bool isStaticItem = false; // if true, don't randomize stats/affix
    public string overrideName;
    public int fixedItemLevel = 1;
    public int fixedRequiredLevel = 1;
    public Rarity fixedRarity = Rarity.Common;
    public int fixedMuscles;
    public int fixedIQ;
    public int fixedCrit;
    public int fixedToughness;
    public int fixedValue;          // if 0 we'll auto-price
    public AffixType forcedAffix = AffixType.None; // can still force "of the Scholar" if you WANT it

    private void OnValidate(){
        if (string.IsNullOrEmpty(id))
            id = System.Guid.NewGuid().ToString("N");
    }
}