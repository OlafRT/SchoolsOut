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
        // At this point player.muscles = (old base + level gain) + equipment bonus,
        // because LevelUp() incremented the already-bonused value.
        // Strip the bonus before storing so baseMuscles is always the true base.
        var (bm, bi, bc, bt) = equipment.GetTotalBonuses();
        baseMuscles   = player.muscles    - bm;
        baseIQ        = player.iq         - bi;
        baseToughness = player.toughness  - bt;
        baseCrit      = Mathf.Clamp01(player.critChance - (bc / 100f));
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
