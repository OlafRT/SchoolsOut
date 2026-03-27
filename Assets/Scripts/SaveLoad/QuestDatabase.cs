// QuestDatabase.cs
// ─────────────────────────────────────────────────────────────────────────────
// ScriptableObject that holds every QuestDefinition in the game.
// Required by GameSaveManager to reconstruct active quests from their IDs.
//
// Setup:
//   1. Right-click in the Project window → Create → RPG → Quest Database
//   2. Drag ALL of your QuestDefinition assets into the "quests" list
//   3. Drag this asset into the GameSaveManager "Quest Database" slot
// ─────────────────────────────────────────────────────────────────────────────

using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "RPG/Quest Database", fileName = "QuestDatabase")]
public class QuestDatabase : ScriptableObject
{
    [Tooltip("Drag every QuestDefinition asset in your project into this list.")]
    public List<QuestDefinition> quests = new();

    // Internal lookup table, built lazily
    private Dictionary<string, QuestDefinition> _lookup;

    void OnEnable() => BuildLookup();

    void BuildLookup()
    {
        _lookup = new Dictionary<string, QuestDefinition>(quests.Count);
        foreach (var q in quests)
            if (q != null && !string.IsNullOrEmpty(q.questId))
                _lookup[q.questId] = q;
    }

    /// <summary>Returns the QuestDefinition with the given ID, or null if not found.</summary>
    public QuestDefinition Get(string questId)
    {
        if (_lookup == null) BuildLookup();
        _lookup.TryGetValue(questId, out var def);
        return def;
    }

    // Re-build in the Editor if the list changes
#if UNITY_EDITOR
    void OnValidate() => BuildLookup();
#endif
}
