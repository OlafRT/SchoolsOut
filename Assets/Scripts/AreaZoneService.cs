using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-50)]
public class AreaZoneService : MonoBehaviour
{
    public static AreaZoneService Instance { get; private set; }

    public event Action<string> OnZoneChanged;         // existing
    public event Action<ZoneVolume> OnZoneChangedZone; // NEW

    [Header("Player discovery")]
    public bool autoFindPlayer = true;

    Transform player;
    readonly List<ZoneVolume> zones = new();
    ZoneVolume current;
    int seqCounter = 0;

    public string     CurrentZoneName => current ? current.zoneName : string.Empty;
    public ZoneVolume CurrentZone     => current;

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        zones.AddRange(FindObjectsOfType<ZoneVolume>(true));
    }

    void Update()
    {
        if (!player && autoFindPlayer) player = FindPlayer();
        if (!player) return;

        Vector3 pos = player.position;

        for (int i = 0; i < zones.Count; i++)
        {
            var z = zones[i];
            if (!z) continue;
            bool nowInside = z.isActiveAndEnabled && z.Contains(pos);
            if (nowInside && !z._inside) { z._inside = true; z._enterSeq = ++seqCounter; }
            else if (!nowInside && z._inside) { z._inside = false; }
        }

        ZoneVolume best = null;
        for (int i = 0; i < zones.Count; i++)
        {
            var z = zones[i];
            if (z == null || !z._inside) continue;
            if (best == null || z.priority > best.priority ||
               (z.priority == best.priority && z._enterSeq > best._enterSeq))
            {
                best = z;
            }
        }

        if (best != current)
        {
            current = best;
            OnZoneChanged?.Invoke(CurrentZoneName);
            OnZoneChangedZone?.Invoke(current);
        }
    }

    public void Register(ZoneVolume z)   { if (z && !zones.Contains(z)) zones.Add(z); }
    public void Unregister(ZoneVolume z) { zones.Remove(z); if (current == z) { current = null; OnZoneChanged?.Invoke(CurrentZoneName); OnZoneChangedZone?.Invoke(null); } }

    Transform FindPlayer()
    {
        var go = GameObject.FindGameObjectWithTag("Player");
        if (go) return go.transform;
        var pa = FindObjectOfType<PlayerAbilities>();
        return pa ? pa.transform : null;
    }
}

