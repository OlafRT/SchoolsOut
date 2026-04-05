using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class EquipmentVisualController : MonoBehaviour
{
    [Header("Refs")]
    public EquipmentState equipment;
    public PlayerStats player;
    public EquipmentVisualLibrary library;

    class ActiveVisualState
    {
        public readonly List<GameObject> enabledObjects = new();
        public readonly List<GameObject> disabledObjects = new();
    }

    readonly Dictionary<EquipSlot, ActiveVisualState> _activeForSlot = new();

    void OnEnable()
    {
        if (!library) library = GetComponent<EquipmentVisualLibrary>();

        if (equipment) equipment.OnEquipmentChanged += RefreshAll;
        if (player) player.OnStatsChanged += RefreshAll;

        RefreshAll();
    }

    void OnDisable()
    {
        if (equipment) equipment.OnEquipmentChanged -= RefreshAll;
        if (player) player.OnStatsChanged -= RefreshAll;
    }

    void RefreshAll()
    {
        if (!library || !player || equipment == null) return;

        // Undo previous visual state first
        foreach (var kv in _activeForSlot)
        {
            var state = kv.Value;
            if (state == null) continue;

            foreach (var go in state.enabledObjects)
                if (go) go.SetActive(false);

            foreach (var go in state.disabledObjects)
                if (go) go.SetActive(true);
        }

        _activeForSlot.Clear();

        // Apply current equipment visuals
        for (int i = 0; i < equipment.equipped.Count; i++)
        {
            var entry = equipment.equipped[i];
            var inst = entry.item;

            if (inst == null || inst.template == null) continue;
            if (!library.TryGet(inst.template, out var vis)) continue;

            var enableObjects = SelectEnableForClass(vis, player.playerClass);
            var disableObjects = SelectDisableForClass(vis, player.playerClass);

            var state = new ActiveVisualState();

            if (disableObjects != null)
            {
                foreach (var go in disableObjects)
                {
                    if (!go) continue;
                    go.SetActive(false);
                    state.disabledObjects.Add(go);
                }
            }

            if (enableObjects != null)
            {
                foreach (var go in enableObjects)
                {
                    if (!go) continue;
                    go.SetActive(true);
                    state.enabledObjects.Add(go);
                }
            }

            if (state.enabledObjects.Count > 0 || state.disabledObjects.Count > 0)
                _activeForSlot[entry.slot] = state;
        }
    }

    List<GameObject> SelectEnableForClass(EquipmentVisualLibrary.Entry vis, PlayerStats.PlayerClass cls)
    {
        switch (cls)
        {
            case PlayerStats.PlayerClass.Nerd:
                return vis.nerdObjects;

            case PlayerStats.PlayerClass.Jock:
                return vis.jockObjects;

            default:
                return vis.nerdObjects.Count > 0 ? vis.nerdObjects : vis.jockObjects;
        }
    }

    List<GameObject> SelectDisableForClass(EquipmentVisualLibrary.Entry vis, PlayerStats.PlayerClass cls)
    {
        switch (cls)
        {
            case PlayerStats.PlayerClass.Nerd:
                return vis.nerdDisableObjects;

            case PlayerStats.PlayerClass.Jock:
                return vis.jockDisableObjects;

            default:
                return vis.nerdDisableObjects.Count > 0 ? vis.nerdDisableObjects : vis.jockDisableObjects;
        }
    }
}