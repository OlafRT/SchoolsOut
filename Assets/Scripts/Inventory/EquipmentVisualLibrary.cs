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

        [Header("Scene Objects to ENABLE")]
        [Tooltip("All GameObjects to enable when a Nerd equips this item.")]
        public List<GameObject> nerdObjects = new();

        [Tooltip("All GameObjects to enable when a Jock equips this item.")]
        public List<GameObject> jockObjects = new();

        [Header("Scene Objects to DISABLE")]
        [Tooltip("Objects to disable when a Nerd equips this item. Useful for hiding default visuals like a starter backpack.")]
        public List<GameObject> nerdDisableObjects = new();

        [Tooltip("Objects to disable when a Jock equips this item.")]
        public List<GameObject> jockDisableObjects = new();
    }

    public List<Entry> entries = new();

    Dictionary<ItemTemplate, Entry> _map;

    void EnsureMap()
    {
        if (_map != null) return;

        _map = new Dictionary<ItemTemplate, Entry>();
        foreach (var e in entries)
        {
            if (e == null || e.template == null) continue;

            if (!_map.ContainsKey(e.template))
                _map.Add(e.template, e);

            // Start explicit item visuals hidden.
            foreach (var go in e.nerdObjects) if (go) go.SetActive(false);
            foreach (var go in e.jockObjects) if (go) go.SetActive(false);

            // Do NOT touch disable objects here.
            // Those may be default visuals that should remain active until something equips over them.
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