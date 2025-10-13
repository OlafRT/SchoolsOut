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

            var tpl = itemDatabase.templates[Random.Range(0, itemDatabase.templates.Count)];
            if (tpl == null) continue;

            int ilvl = Random.Range(minIlvl, maxIlvl + 1);
            var inst = AffixRoller.Roll(tpl, ilvl);
            list.Add(inst);
        }
        return list;
    }
}
