// LootReporter.cs
using UnityEngine;

public class LootReporter : MonoBehaviour
{
    public string itemId = "lunch_money";
    public int amount = 1;

    // Call this when the item is actually granted (e.g., picked up / added to inventory)
    public void ReportLoot(){ QuestEvents.ItemLooted?.Invoke(itemId, amount); }
}
