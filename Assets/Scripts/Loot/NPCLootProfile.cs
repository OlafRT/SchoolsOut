using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewNPCLootProfile")]
public class NPCLootProfile : ScriptableObject
{
    [Header("Sources")]
    public ItemDatabase itemDatabase;

    [Header("Rolls")]
    [Tooltip("How many item rolls to try (each can whiff).")]
    public Vector2Int itemRolls = new Vector2Int(1, 3);

    [Range(0f, 1f)] public float itemDropChance = 0.60f;

    [Header("Money")]
    [Tooltip("Base coin per NPC level (randomized around this).")]
    public int dollarsPerLevel = 3;
    public Vector2 moneyRandomFactor = new Vector2(0.5f, 1.5f); // 50%..150%

    [Header("Item level band")]
    [Tooltip("How far from NPC level item ilvl can vary.")]
    public int ilvlVariance = 2;

    // -------------------------------------------------------------------------
    // Rarity Weights
    // Higher weight = more likely to be picked. A weight of 0 means that rarity
    // can never drop from this profile. Tune these per enemy type in the Inspector
    // (e.g. a boss profile might have higher Epic/Legendary weights).
    // -------------------------------------------------------------------------
    [Header("Rarity Weights")]
    [Tooltip("Relative drop weight for Common items. Higher = more frequent.")]
    public float weightCommon    = 100f;
    [Tooltip("Relative drop weight for Uncommon items.")]
    public float weightUncommon  = 40f;
    [Tooltip("Relative drop weight for Rare items.")]
    public float weightRare      = 15f;
    [Tooltip("Relative drop weight for Epic items.")]
    public float weightEpic      = 4f;
    [Tooltip("Relative drop weight for Legendary items. Set to 0 to prevent legendaries from this enemy.")]
    public float weightLegendary = 1f;

    // -------------------------------------------------------------------------

    public int GetRandomMoney(int npcLevel)
    {
        float baseAmt = Mathf.Max(0, npcLevel) * Mathf.Max(0, dollarsPerLevel);
        float f = Random.Range(moneyRandomFactor.x, moneyRandomFactor.y);
        return Mathf.Max(0, Mathf.RoundToInt(baseAmt * f));
    }

    /// <summary>Generate up to N items around npcLevel (clamped 1..30).</summary>
    public List<ItemInstance> RollItems(int npcLevel, int desiredCount)
    {
        var list = new List<ItemInstance>();
        if (!itemDatabase || itemDatabase.templates == null || itemDatabase.templates.Count == 0) return list;

        int minIlvl = Mathf.Clamp(npcLevel - ilvlVariance, 1, 30);
        int maxIlvl = Mathf.Clamp(npcLevel + ilvlVariance, 1, 30);

        for (int i = 0; i < desiredCount; i++)
        {
            if (Random.value > itemDropChance) continue;

            // 1) Pick a rarity first, using the weighted chances above.
            Rarity chosenRarity = PickWeightedRarity();

            // 2) Collect all templates that match that rarity.
            //    Falls back to any rarity if none exist for the chosen one.
            var candidates = BuildCandidateList(chosenRarity);
            if (candidates.Count == 0) continue;

            // 3) Pick uniformly from the matching templates.
            var tpl = candidates[Random.Range(0, candidates.Count)];
            if (tpl == null) continue;

            int ilvl = Random.Range(minIlvl, maxIlvl + 1);
            var inst = AffixRoller.CreateFromTemplate(tpl, ilvl);
            if (inst != null)
                list.Add(inst);
        }
        return list;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Rolls a Rarity value using the weight fields above.
    /// Works correctly even if some weights are zero.
    /// </summary>
    Rarity PickWeightedRarity()
    {
        float total = weightCommon + weightUncommon + weightRare + weightEpic + weightLegendary;

        // Safety: if everything is 0, fall back to Common.
        if (total <= 0f) return Rarity.Common;

        float roll = Random.Range(0f, total);

        if (roll < weightCommon)                                   return Rarity.Common;
        roll -= weightCommon;
        if (roll < weightUncommon)                                 return Rarity.Uncommon;
        roll -= weightUncommon;
        if (roll < weightRare)                                     return Rarity.Rare;
        roll -= weightRare;
        if (roll < weightEpic)                                     return Rarity.Epic;
        return Rarity.Legendary;
    }

    /// <summary>
    /// Returns all templates whose rarity matches <paramref name="target"/>.
    /// If none exist for that rarity, returns the full template list as a fallback
    /// so a roll never simply whiffs due to a missing rarity bucket.
    /// </summary>
    List<ItemTemplate> BuildCandidateList(Rarity target)
    {
        var result = new List<ItemTemplate>();
        foreach (var tpl in itemDatabase.templates)
        {
            if (tpl != null && tpl.rarity == target)
                result.Add(tpl);
        }

        // Fallback: if nobody has that rarity, allow any template.
        if (result.Count == 0)
        {
            foreach (var tpl in itemDatabase.templates)
                if (tpl != null) result.Add(tpl);
        }

        return result;
    }
}
