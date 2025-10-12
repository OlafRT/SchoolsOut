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

    void Awake()
    {
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
        if (_map != null && t != null && _map.TryGetValue(t, out var found))
        {
            e = found;
            return true;
        }
        e = null;
        return false;
    }
}
