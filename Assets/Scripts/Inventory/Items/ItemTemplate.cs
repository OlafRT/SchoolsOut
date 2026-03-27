using System.Collections.Generic;
using UnityEngine;

public enum ConsumableKind
{
    None,
    Food,
    Healing,
    Buff,
    TeachSpell,
    Utility
}

public enum BuffStat
{
    Muscles,
    IQ,
    Toughness,
    CritChance
}

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

    [Header("Class Restriction")]
    [Tooltip("Tick to restrict this item to a specific class. Leave unticked for any class to equip.")]
    public bool hasClassRestriction = false;
    [Tooltip("Only used if hasClassRestriction is ticked.")]
    public PlayerStats.PlayerClass requiredClass = PlayerStats.PlayerClass.Nerd;

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

    [Header("Consumable")]
    public bool isConsumable = false;
    public ConsumableKind consumableKind = ConsumableKind.None;

    // Food (heal over time, requires sitting still)
    [Header("Food")]
    public int foodTotalHeal = 20;
    public float foodDurationSeconds = 8f;
    public bool foodConsumeOnStart = true; // WoW-like: you “use” it and regen starts
    [Header("Food Prop (Enable existing)")]
    public string foodPropId; // e.g. "Sandwich", "Apple", "Slushie"

    // Healing potion (instant)
    [Header("Healing Potion")]
    public int healAmount = 30;

    // Buff
    [Header("Buff")]
    public BuffStat buffStat = BuffStat.Muscles;
    public int buffAmount = 2;
    public float buffDurationSeconds = 30f;

    // Teach Spell
    [Header("Teach Spell")]
    public string teachesAbilityName;

    // Optional SFX
    [Header("SFX")]
    public AudioClip useSfx;
    [Range(0f, 1f)] public float useSfxVolume = 0.8f;

    [Header("Stacking")]
    public bool isStackable = false;

    [Min(1)]
    public int maxStackSize = 20;
}