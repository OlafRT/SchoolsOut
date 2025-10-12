using System.Collections.Generic;
using UnityEngine;

public class TestItemSpawner : MonoBehaviour {
    public Inventory inventory;
    public List<ItemTemplate> templates;
    public Vector2Int itemLevelRange = new Vector2Int(1, 10);

    [ContextMenu("Give Random Rolls (One Per Template)")]
    public void GiveAll() {
        foreach (var t in templates) {
            int ilvl = Random.Range(itemLevelRange.x, itemLevelRange.y + 1);
            var inst = AffixRoller.Roll(t, ilvl);
            inventory.Add(inst, 1);
        }
    }

    [ContextMenu("Fill 20 Random Items")]
    public void FillTwentyRandom() => FillRandom(20);

    public void FillRandom(int count) {
        if (templates == null || templates.Count == 0) return;
        for (int i = 0; i < count; i++) {
            var t = templates[Random.Range(0, templates.Count)];
            int ilvl = Random.Range(itemLevelRange.x, itemLevelRange.y + 1);
            var inst = AffixRoller.Roll(t, ilvl);
            inventory.Add(inst, 1);
        }
    }

    [ContextMenu("Clear Bag")]
    public void ClearBag() {
        inventory.Clear();
    }
}