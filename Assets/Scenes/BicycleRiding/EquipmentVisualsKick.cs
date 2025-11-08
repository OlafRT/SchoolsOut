using UnityEngine;

public class EquipmentVisualsKick : MonoBehaviour
{
    [SerializeField] private EquipmentState equipment;
    [SerializeField] private PlayerStats player;

    void Start()
    {
        // Nudge both event paths; the controller listens to these.
        if (player)    player.RaiseStatsChanged();   // covers class-based selection
        if (equipment) equipment.NotifyChanged();    // re-emits current equipped list
    }
}