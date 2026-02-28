using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "RPG/Vendor Definition", fileName = "VendorDefinition")]
public class VendorDefinition : ScriptableObject
{
    [Serializable]
    public class StockEntry
    {
        public ItemTemplate template;
        [Min(1)] public int amount = 1;
    }

    [Header("Starting Stock")]
    public List<StockEntry> startingStock = new();

    [Header("Pricing")]
    [Tooltip("Multiplier applied to evaluated item price when BUYING from vendor.")]
    public float buyMultiplier = 1f;

    [Tooltip("Multiplier applied to evaluated item price when SELLING to vendor.")]
    public float sellMultiplier = 1f;
}