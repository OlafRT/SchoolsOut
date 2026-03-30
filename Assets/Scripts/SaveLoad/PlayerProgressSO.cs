// PlayerProgressSO.cs
// ─────────────────────────────────────────────────────────────────────────────
// ScriptableObject that holds the player's persistent progress.
// Works exactly like Inventory and EquipmentState — it's a project asset that
// survives scene transitions without any special handling.
//
// SETUP
// ─────
//  1. Right-click in Project → Create → RPG → Player Progress
//  2. Assign it to PlayerStats (progressAsset field)
//  3. Assign it to GameSaveManager (playerProgress field)
//  4. That's it — no snapshot code needed.
// ─────────────────────────────────────────────────────────────────────────────

using UnityEngine;
using System;

[CreateAssetMenu(menuName = "RPG/Player Progress", fileName = "PlayerProgress")]
public class PlayerProgressSO : ScriptableObject
{
    [Header("Identity")]
    public string playerClass = "Nerd";
    public int    level       = 1;

    [Header("XP")]
    public int currentXP = 0;
    public int xpToNext  = 85;

    [Header("Base Stats  (before equipment bonuses)")]
    public int   muscles   = 1;
    public int   iq        = 1;
    public int   toughness = 5;
    public float critChance = 0.05f;

    [Header("Health")]
    public int currentHP = 0;   // 0 = use MaxHP on load

    // ── Runtime flag ──────────────────────────────────────────────────────────
    // Set to false when the player is just starting (class select screen).
    // Set to true once the first scene with a player loads so PlayerStats
    // knows to read from this asset instead of using Inspector defaults.
    [NonSerialized] public bool hasData = false;

    /// <summary>Reset to fresh state for a new game.</summary>
    public void ResetForNewGame()
    {
        level      = 1;
        currentXP  = 0;
        xpToNext   = 85;
        muscles    = 1;
        iq         = 1;
        toughness  = 5;
        critChance = 0.05f;
        currentHP  = 0;
        hasData    = false;
    }
}
