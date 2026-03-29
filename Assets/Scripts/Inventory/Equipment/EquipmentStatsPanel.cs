using UnityEngine;
using TMPro;

public class EquipmentStatsPanel : MonoBehaviour
{
    [Header("Refs")]
    public PlayerStats player;          // assign your PlayerStats on the character
    public EquipmentState equipment;    // optional; only to force a refresh on equip change

    [Header("Labels")]
    public TMP_Text toughnessText;
    public TMP_Text iqText;
    public TMP_Text critText;
    public TMP_Text muscleText;
    public TMP_Text levelText;

    void OnEnable()
    {
        // Always re-find — the Inspector reference becomes stale after a scene change
        // because the old PlayerStats GameObject was destroyed with the previous scene.
        player = FindFirstObjectByType<PlayerStats>();

        if (player)
        {
            player.OnStatsChanged += Refresh;
            player.OnLeveledUp += _ => Refresh();
        }
        if (equipment) equipment.OnEquipmentChanged += Refresh;

        Refresh();
    }

    void OnDisable()
    {
        // Use the cached reference — it may already be null if scene unloaded
        if (player)
        {
            player.OnStatsChanged -= Refresh;
            player.OnLeveledUp -= _ => Refresh();
            player = null;  // clear so OnEnable always re-finds fresh
        }
        if (equipment) equipment.OnEquipmentChanged -= Refresh;
    }

    public void Refresh()
    {
        if (!player) return;

        // Numbers from PlayerStats (your bridge already applies equipment bonuses)
        if (toughnessText) toughnessText.text = $"Toughness: {player.toughness}";
        if (iqText)        iqText.text        = $"IQ: {player.iq}";
        if (muscleText)    muscleText.text    = $"Muscle: {player.muscles}";
        if (critText)      critText.text      = $"Crit: {Mathf.RoundToInt(player.critChance * 100f)}%";

        if (levelText)
        {
            // “Level 5 Nerd” or “Level 5 Jock”
            string cls = player.playerClass.ToString();
            levelText.text = $"Level {player.level} {cls}";
        }
    }
}