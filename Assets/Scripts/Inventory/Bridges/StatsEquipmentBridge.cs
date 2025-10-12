using System;
using UnityEngine;

public class StatsEquipmentBridge : MonoBehaviour {
    public EquipmentState equipment;
    public PlayerStats player;

    private int baseMuscles, baseIQ, baseToughness;
    private float baseCrit;
    private Action<int> _leveledHandler;

    void OnEnable() {
        if (equipment) equipment.OnEquipmentChanged += Reapply;

        if (player) {
            baseMuscles   = player.muscles;
            baseIQ        = player.iq;
            baseCrit      = player.critChance;
            baseToughness = player.toughness;

            _leveledHandler = _ => CaptureNewBaseAndReapply();
            player.OnLeveledUp += _leveledHandler;

            // DO NOT subscribe to player.OnStatsChanged here
        }

        Reapply();
    }

    void OnDisable() {
        if (equipment) equipment.OnEquipmentChanged -= Reapply;
        if (player && _leveledHandler != null) player.OnLeveledUp -= _leveledHandler;
    }

    void CaptureNewBaseAndReapply() {
        baseMuscles   = player.muscles;
        baseIQ        = player.iq;
        baseCrit      = player.critChance;
        baseToughness = player.toughness;
        Reapply();
    }

    public void Reapply() {
        if (!player || !equipment) return;

        var (bm, bi, bc, bt) = equipment.GetTotalBonuses();
        player.muscles    = baseMuscles   + bm;
        player.iq         = baseIQ        + bi;
        player.toughness  = baseToughness + bt;
        player.critChance = Mathf.Clamp01(baseCrit + (bc / 100f));

        player.RaiseStatsChanged(); // notify once, no recursion now
    }
}
