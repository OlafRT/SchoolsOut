using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerPropRegistry : MonoBehaviour
{
    [Serializable]
    public class PropEntry
    {
        public string id;
        public GameObject go;
    }

    public List<PropEntry> props = new();

    Dictionary<string, GameObject> map;

    void Awake()
    {
        map = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in props)
        {
            if (p == null || string.IsNullOrEmpty(p.id) || !p.go) continue;
            map[p.id] = p.go;
            p.go.SetActive(false); // start disabled
        }
    }

    public bool TryGet(string id, out GameObject go)
    {
        go = null;
        if (string.IsNullOrEmpty(id) || map == null) return false;
        return map.TryGetValue(id, out go) && go != null;
    }

    public void DisableAll()
    {
        if (map == null) return;
        foreach (var kv in map)
            if (kv.Value) kv.Value.SetActive(false);
    }
}