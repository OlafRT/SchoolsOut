// SaveData.cs
// ─────────────────────────────────────────────────────────────────────────────
// All serializable data containers used by GameSaveManager.
// Extend any class here if you need to store more fields later.
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Collections.Generic;
using UnityEngine;

// ── Top-level save file ───────────────────────────────────────────────────────
[Serializable]
public class GameSaveData
{
    public string saveVersion  = "1.0";
    /// <summary>Human-readable label shown in the load menu, e.g. "Level 5 Jock – Forest".</summary>
    public string displayName;
    /// <summary>DateTime.UtcNow.Ticks at time of save, used to sort/display save dates.</summary>
    public long   savedAtUtc;

    public PlayerStatsSaveData    playerStats     = new();
    public PlayerPositionSaveData playerPosition  = new();
    public PlayerAbilitiesSaveData playerAbilities = new();

    // Inventory, equipment and wallet re-use the types already defined in
    // InventoryPersistence.cs so nothing needs changing there.
    public InventoryData  inventory  = new();
    public EquipmentData  equipment  = new();
    public WalletData     wallet     = new();

    public QuestSaveData             quests      = new();
    public List<WorldObjectSaveData> worldObjects = new();

    // ── Helpers ───────────────────────────────────────────────────────────────
    /// <summary>Returns the saved timestamp as a local DateTime string.</summary>
    public string SavedAtString()
    {
        if (savedAtUtc == 0) return "—";
        return new DateTime(savedAtUtc, DateTimeKind.Utc)
            .ToLocalTime()
            .ToString("MMM d  h:mm tt");
    }
}

// ── Player stats ──────────────────────────────────────────────────────────────
[Serializable]
public class PlayerStatsSaveData
{
    /// <summary>Stored as the enum name string so it survives enum reordering.</summary>
    public string playerClass;
    public int    level;
    public int    currentXP;
    public int    xpToNext;

    // These are the BASE values (before equipment bonuses).
    // GameSaveManager subtracts equipment bonuses before writing
    // so that StatsEquipmentBridge can correctly re-apply them on load.
    public int   muscles;
    public int   iq;
    public int   toughness;
    public float critChance;
}

// ── Player position ───────────────────────────────────────────────────────────
[Serializable]
public class PlayerPositionSaveData
{
    public string sceneName;
    public float  x, y, z;
    public float  rotY;     // only Y rotation matters for most top-down games
}

// ── Abilities ─────────────────────────────────────────────────────────────────
[Serializable]
public class PlayerAbilitiesSaveData
{
    public List<string> learnedAbilities = new();
}

// ── Quests ────────────────────────────────────────────────────────────────────
[Serializable]
public class QuestSaveData
{
    public List<string>           completedIds = new();
    public List<ActiveQuestEntry> active       = new();
}

[Serializable]
public class ActiveQuestEntry
{
    public string      questId;
    public List<int>   progress = new();
}

// ── World objects ─────────────────────────────────────────────────────────────
[Serializable]
public class WorldObjectSaveData
{
    /// <summary>Matches SaveableObject.objectId.</summary>
    public string objectId;
    public bool   isActive;

    // Add more fields here as needed, e.g.:
    // public string stateString;   // for objects with multiple states
    // public float  health;
}

// ── Save-slot metadata (tiny file, read without loading full save) ─────────────
[Serializable]
public class SaveSlotMeta
{
    public int    lastUsedSlot = -1;
}
