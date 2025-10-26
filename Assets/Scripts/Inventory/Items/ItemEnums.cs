using System;


public enum ItemType { Consumable, Material, Equipment, Upgrade }
public enum EquipSlot { Head, Neck, RingLeft, RingRight, Weapon, Trinket, Upgrade1, Upgrade2, Upgrade3, Upgrade4 }
// Added Poor (Grey) to match WoW tiers exactly
public enum Rarity { Poor, Common, Uncommon, Rare, Epic, Legendary }

public enum AffixType
{
    None,
    Athlete,
    Scholar,
    Lucky,
    Power,
    Cognition
}