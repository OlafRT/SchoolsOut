using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "RPG/Item Template", fileName = "NewItemTemplate")]
public class ItemTemplate : ScriptableObject {
    [Header("Identity")] public string id; // GUID-ish id for save/load
    [Tooltip("Base display name before affix")] public string baseName = "Wizard Hat";
    public Sprite icon; public Rarity rarity = Rarity.Common; public ItemType itemType = ItemType.Equipment; // template default
    [Header("Equip")] public bool isEquippable = true; public EquipSlot equipSlot;
    [Header("Allowed Affixes")] public List<AffixType> allowedAffixes = new(){ AffixType.Athlete, AffixType.Scholar, AffixType.Lucky, AffixType.Power, AffixType.Cognition };
    [Header("Tooltip")] [TextArea(2,6)] public string description;

    private void OnValidate(){ if(string.IsNullOrEmpty(id)) id = System.Guid.NewGuid().ToString("N"); }
}