using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class NPCStatusHost : MonoBehaviour
{
    public class StatusEntry
    {
        public string tag;
        public UnityEngine.Object source; // who applied it
        public Sprite icon;
    }

    // Map: tag → list of sources providing it
    readonly Dictionary<string, List<StatusEntry>> active = new();

    public IReadOnlyDictionary<string, List<StatusEntry>> Active => active;

    public event Action OnStatusesChanged;

    /// <summary>
    /// Add or refresh a status coming from a source (like an AoE field).
    /// Multiple sources with same tag can coexist.
    /// </summary>
    public void AddOrRefreshAura(string tag, UnityEngine.Object source, Sprite icon = null)
    {
        if (string.IsNullOrEmpty(tag) || source == null) return;

        if (!active.TryGetValue(tag, out var list))
        {
            list = new List<StatusEntry>();
            active[tag] = list;
        }

        // Check if same source already in list
        var found = list.Find(x => x.source == source);
        if (found != null)
        {
            // Refresh (replace icon if provided)
            if (icon) found.icon = icon;
        }
        else
        {
            // Add new entry
            list.Add(new StatusEntry { tag = tag, source = source, icon = icon });
        }

        OnStatusesChanged?.Invoke();
    }

    /// <summary>
    /// Remove a status if this source no longer applies it.
    /// </summary>
    public void RemoveAura(string tag, UnityEngine.Object source)
    {
        if (!active.TryGetValue(tag, out var list)) return;

        int removed = list.RemoveAll(x => x.source == source);
        if (removed > 0)
        {
            if (list.Count == 0)
                active.Remove(tag);

            OnStatusesChanged?.Invoke();
        }
    }

    /// <summary>
    /// Clears all statuses coming from this source (e.g. AoE despawned).
    /// </summary>
    public void ClearAura(UnityEngine.Object source)
    {
        bool changed = false;
        var tagsToRemove = new List<string>();

        foreach (var kv in active)
        {
            int removed = kv.Value.RemoveAll(x => x.source == source);
            if (removed > 0)
            {
                changed = true;
                if (kv.Value.Count == 0) tagsToRemove.Add(kv.Key);
            }
        }

        foreach (var tag in tagsToRemove)
            active.Remove(tag);

        if (changed) OnStatusesChanged?.Invoke();
    }
}
