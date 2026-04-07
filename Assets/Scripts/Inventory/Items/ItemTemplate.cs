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

/// <summary>
/// How the Nerd's auto-attack fires when this weapon is equipped.
/// </summary>
public enum WeaponShotPattern
{
    /// <summary>Single straight-line projectile (default behaviour).</summary>
    Single,

    /// <summary>
    /// Shotgun spread: fires <c>weaponSpreadShots</c> pellets fanned across
    /// <c>weaponSpreadAngle</c> degrees. Damage is divided evenly per pellet;
    /// range is multiplied by <c>weaponSpreadRangeMultiplier</c>.
    /// Good for: water pistol, goop launcher, dust blaster.
    /// </summary>
    Spread,

    /// <summary>
    /// Rapid burst: fires <c>weaponBurstCount</c> projectiles in quick succession
    /// with <c>weaponBurstDelay</c> seconds between each shot, all in the same direction.
    /// Good for: semi-auto / machine gun feel.
    /// </summary>
    Burst,
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

    // -------------------------------------------------------------------------
    // Weapon Profile
    // Drives WeaponEquipBridge → AutoAttackAbility when this item is in the
    // Weapon slot. Enable hasWeaponProfile and leave any override at its zero /
    // null default to inherit the value already set in the Inspector.
    // -------------------------------------------------------------------------
    [Header("Weapon Profile (Nerd auto-attack)")]
    [Tooltip("Enable to let this weapon customise projectile, range, speed and shot pattern.")]
    public bool hasWeaponProfile = false;

    [Tooltip("How the auto-attack fires. Single = straight line, Spread = shotgun arc, Burst = rapid multi-shot.")]
    public WeaponShotPattern shotPattern = WeaponShotPattern.Single;

    [Tooltip("Animator trigger to fire when this weapon attacks. " +
             "Leave empty to use the default trigger set in AutoAttackAbility ('Throw').")]
    public string weaponAnimTrigger;

    [Tooltip("Projectile prefab to use. Assign a water-drop, goop blob, dust particle prefab etc. " +
             "Leave null to keep the PlayerAbilities default.")]
    public GameObject weaponProjectilePrefab;

    [Tooltip("Projectile travel speed. 0 = keep AutoAttackAbility / PlayerAbilities default.")]
    [Min(0f)] public float weaponProjectileSpeed = 0f;

    [Tooltip("How many tiles the projectile travels. 0 = keep AutoAttackAbility default.")]
    [Min(0)] public int weaponRangeTiles = 0;

    [Tooltip("Seconds between auto-attack shots. 0 = keep AutoAttackAbility default.")]
    [Min(0f)] public float weaponAttackInterval = 0f;

    // ----- Spread -----
    [Tooltip("Number of pellets fired per shot (Spread only). Damage is split evenly across pellets.")]
    [Min(2)] public int weaponSpreadShots = 3;

    [Tooltip("Total arc in degrees the pellets are spread across (Spread only).")]
    [Range(5f, 120f)] public float weaponSpreadAngle = 45f;

    [Tooltip("Fraction of weaponRangeTiles each pellet travels (Spread only). " +
             "0.6 means 60 % of the normal range — shotguns are short-ranged.")]
    [Range(0.1f, 1f)] public float weaponSpreadRangeMultiplier = 0.6f;

    [Tooltip("Damage multiplier applied to each pellet individually (Spread only).\n" +
             "1.0 = every pellet hits for full weaponDamage (high burst potential).\n" +
             "Set to 1/spreadShots (e.g. 0.33 for 3 pellets) to keep total damage equal to a single shot.")]
    [Range(0.05f, 2f)] public float weaponSpreadDamageMultiplier = 1f;

    // ----- Burst -----
    [Tooltip("How many projectiles fire per trigger pull (Burst only).")]
    [Min(2)] public int weaponBurstCount = 3;

    [Tooltip("Seconds between each shot in a burst (Burst only).")]
    [Min(0.01f)] public float weaponBurstDelay = 0.1f;

    // ----- VFX / SFX -----
    [Tooltip("Impact VFX prefab override. Leave null to keep AutoAttackAbility default.")]
    public GameObject weaponImpactVfx;

    [Tooltip("Impact SFX clip override. Leave null to keep AutoAttackAbility default.")]
    public AudioClip weaponImpactSfx;

    // -------------------------------------------------------------------------
    [Header("Consumable")]
    public bool isConsumable = false;
    public ConsumableKind consumableKind = ConsumableKind.None;

    // Food (heal over time, requires sitting still)
    [Header("Food")]
    public int foodTotalHeal = 20;
    public float foodDurationSeconds = 8f;
    public bool foodConsumeOnStart = true; // WoW-like: you "use" it and regen starts
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
