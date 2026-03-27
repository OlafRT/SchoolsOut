using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class EquipmentVisualLibrary : MonoBehaviour
{
    [Serializable]
    public class Entry
    {
        public ItemTemplate template;
        public EquipSlot slot;
        [Header("Scene Objects to toggle")]
        public GameObject nerdObject;
        public GameObject jockObject;
    }

    public List<Entry> entries = new();

    Dictionary<ItemTemplate, Entry> _map;

    // Build (or rebuild) the lookup and hide all visuals.
    // Called lazily on first TryGet so it's always ready regardless
    // of which component's Awake/OnEnable runs first.
    void EnsureMap()
    {
        if (_map != null) return;

        _map = new Dictionary<ItemTemplate, Entry>();
        foreach (var e in entries)
        {
            if (e != null && e.template != null && !_map.ContainsKey(e.template))
                _map.Add(e.template, e);

            // start hidden
            if (e?.nerdObject) e.nerdObject.SetActive(false);
            if (e?.jockObject) e.jockObject.SetActive(false);
        }
    }

    public bool TryGet(ItemTemplate t, out Entry e)
    {
        EnsureMap();

        if (t != null && _map.TryGetValue(t, out var found))
        {
            e = found;
            return true;
        }
        e = null;
        return false;
    }
}