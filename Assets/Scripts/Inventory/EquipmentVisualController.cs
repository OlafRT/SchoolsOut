using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class EquipmentVisualController : MonoBehaviour
{
    [Header("Refs")]
    public EquipmentState equipment;            // PlayerEquipment ScriptableObject
    public PlayerStats player;                  // your PlayerStats (for class + level)
    public EquipmentVisualLibrary library;      // the component with Nerd/Jock GOs

    readonly Dictionary<EquipSlot, GameObject> _activeForSlot = new();

    void OnEnable()
    {
        if (!library) library = GetComponent<EquipmentVisualLibrary>();
        if (equipment) equipment.OnEquipmentChanged += RefreshAll;
        if (player)    player.OnStatsChanged        += RefreshAll; // covers class swaps too
        RefreshAll();
    }

    void OnDisable()
    {
        if (equipment) equipment.OnEquipmentChanged -= RefreshAll;
        if (player)    player.OnStatsChanged        -= RefreshAll;
    }

    void RefreshAll()
    {
        if (!library || !player || equipment == null) return;

        // turn off what we previously enabled
        foreach (var kv in _activeForSlot)
            if (kv.Value) kv.Value.SetActive(false);
        _activeForSlot.Clear();

        // enable proper visuals for equipped items
        for (int i = 0; i < equipment.equipped.Count; i++)
        {
            var entry = equipment.equipped[i];
            var inst  = entry.item;
            if (inst == null || inst.template == null) continue;

            if (!library.TryGet(inst.template, out var vis)) continue;

            var go = SelectForClass(vis, player.playerClass); // <- qualified enum
            if (go)
            {
                go.SetActive(true);
                _activeForSlot[entry.slot] = go;
            }
        }
    }

    // Note the qualified enum type here
    GameObject SelectForClass(EquipmentVisualLibrary.Entry vis, PlayerStats.PlayerClass cls)
    {
        switch (cls)
        {
            case PlayerStats.PlayerClass.Nerd: return vis.nerdObject;
            case PlayerStats.PlayerClass.Jock: return vis.jockObject;
            default:                           return vis.nerdObject ?? vis.jockObject; // fallback
        }
    }
}
