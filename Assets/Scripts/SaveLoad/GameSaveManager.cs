// GameSaveManager.cs
// ─────────────────────────────────────────────────────────────────────────────
// Central singleton that handles all saving and loading.
// Persists across scenes via DontDestroyOnLoad.

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class GameSaveManager : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────────────
    public static GameSaveManager I { get; private set; }

    // ── Constants ─────────────────────────────────────────────────────────────
    public const int    SlotCount = 4;
    private const string SlotFile = "save_slot_{0}.json";
    private const string MetaFile = "save_meta.json";

    // ── Inspector ─────────────────────────────────────────────────────────────
    [Header("ScriptableObject Assets  (assign in Inspector)")]
    public Inventory      inventory;
    public EquipmentState equipment;
    public PlayerWallet   wallet;
    public QuestDatabase  questDatabase;

    // ItemDatabase loads itself from Resources — no Inspector assignment needed.
    // Place your ItemDatabase asset at:  Assets/Resources/ItemDatabase.asset
    private ItemDatabase _itemDatabase;
    private ItemDatabase GetItemDatabase()
    {
        if (_itemDatabase) return _itemDatabase;
        _itemDatabase = Resources.Load<ItemDatabase>("ItemDatabase");
        if (!_itemDatabase) Debug.LogError("[SaveSystem] ItemDatabase not found in Resources. "
            + "Place it at Assets/Resources/ItemDatabase.asset");
        return _itemDatabase;
    }

    [Header("Scene Settings")]
    [Tooltip("Scene name to load when the player starts a New Game.")]
    public string defaultGameScene = "GameScene";

    // ── Runtime refs (found automatically after each scene load) ──────────────
    private PlayerStats     _stats;
    private PlayerAbilities _abilities;
    private QuestManager    _quests;

    // Pending save data to apply once the target scene finishes loading
    private GameSaveData _pendingLoad;

    // Lightweight snapshot carried between scenes so stats/XP/level
    // survive scene transitions without requiring a manual save.
    private PlayerStatsSaveData _statSnapshot;
    private PlayerAbilitiesSaveData _abilitiesSnapshot;

    /// <summary>
    /// The slot this play session belongs to.
    /// Set automatically when loading a slot or starting a new game.
    /// SaveGameButton uses this so it always overwrites the correct slot.
    /// </summary>
    public int ActiveSlot { get; private set; } = -1;

    // ═══════════════════════════════════════════════════════════════════════════
    //  Unity lifecycle
    // ═══════════════════════════════════════════════════════════════════════════

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded   += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    void OnDestroy()
    {
        if (I == this)
        {
            SceneManager.sceneLoaded   -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RebindRuntimeRefs();

        if (_pendingLoad != null)
        {
            string target = _pendingLoad.playerPosition?.sceneName;
            if (string.IsNullOrEmpty(target) || scene.name == target)
            {
                // A full load is happening — clear the snapshot so it
                // doesn't overwrite the loaded stats
                _statSnapshot       = null;
                _abilitiesSnapshot  = null;
                ApplyPendingLoad();
                return;
            }
        }

        // No full load — delay one frame so Start() runs on all scene objects
        // (UI panels subscribe to OnStatsChanged in OnEnable/Start, and we need
        // them subscribed before we raise the event)
        StartCoroutine(ApplyStatSnapshotNextFrame());
    }

    void OnSceneUnloaded(Scene scene)
    {
        // Capture stats before the player is destroyed with the scene
        RebindRuntimeRefs();
        TakeStatSnapshot();
    }

    void TakeStatSnapshot()
    {
        if (!_stats) return;

        // Strip equipment bonuses — same logic as WriteStats
        int eqM = 0, eqI = 0, eqT = 0; float eqC = 0f;
        if (_stats.GetComponent<StatsEquipmentBridge>() != null && equipment != null)
        {
            var (bm, bi, bc, bt) = equipment.GetTotalBonuses();
            eqM = bm; eqI = bi; eqT = bt; eqC = bc / 100f;
        }

        var snapHealth = _stats.GetComponent<PlayerHealth>();
        _statSnapshot = new PlayerStatsSaveData
        {
            playerClass = _stats.playerClass.ToString(),
            level       = _stats.level,
            currentXP   = _stats.currentXP,
            xpToNext    = _stats.xpToNext,
            muscles     = _stats.muscles   - eqM,
            iq          = _stats.iq        - eqI,
            toughness   = _stats.toughness - eqT,
            critChance  = Mathf.Clamp01(_stats.critChance - eqC),
            currentHP   = snapHealth ? snapHealth.currentHP : 0,
        };

        if (_abilities)
            _abilitiesSnapshot = new PlayerAbilitiesSaveData
            {
                learnedAbilities = new System.Collections.Generic.List<string>(_abilities.learnedAbilities),
            };

        Debug.Log($"[SaveSystem] Stat snapshot taken: Level {_statSnapshot.level} {_statSnapshot.playerClass}");
    }

    System.Collections.IEnumerator ApplyStatSnapshotNextFrame()
    {
        yield return null; // wait one frame for Start() to run on scene objects
        RebindRuntimeRefs(); // re-find player now that Start() has run
        ApplyStatSnapshot();
    }

    void ApplyStatSnapshot()
    {
        if (_statSnapshot == null) return;

        if (!_stats) return;

        // Use the same ReadStats logic
        var bridge = _stats.GetComponent<StatsEquipmentBridge>();
        if (bridge) bridge.enabled = false;

        if (System.Enum.TryParse<PlayerStats.PlayerClass>(_statSnapshot.playerClass, out var cls))
            _stats.playerClass = cls;

        _stats.level      = _statSnapshot.level;
        _stats.currentXP  = _statSnapshot.currentXP;
        _stats.xpToNext   = _statSnapshot.xpToNext;
        _stats.muscles    = _statSnapshot.muscles;
        _stats.iq         = _statSnapshot.iq;
        _stats.toughness  = _statSnapshot.toughness;
        _stats.critChance = _statSnapshot.critChance;

        if (bridge) bridge.enabled = true;

        if (_abilities && _abilitiesSnapshot != null)
            _abilities.learnedAbilities = new System.Collections.Generic.List<string>(_abilitiesSnapshot.learnedAbilities);

        // Restore HP after bridge re-applies equipment (so MaxHP is correct)
        if (_statSnapshot.currentHP > 0)
        {
            var health = _stats.GetComponent<PlayerHealth>();
            if (health)
            {
                health.currentHP = Mathf.Clamp(_statSnapshot.currentHP, 0, health.stats ? health.stats.MaxHP : _statSnapshot.currentHP);
                PlayerHUD.TryUpdateHealth(health.currentHP, health.stats ? health.stats.MaxHP : health.currentHP);
            }
        }

        _stats.RaiseStatsChanged();

        Debug.Log($"[SaveSystem] Stat snapshot applied: Level {_statSnapshot.level} {_statSnapshot.playerClass}");
    }

    void RebindRuntimeRefs()
    {
        // Include inactive objects — handles players that are briefly disabled
        // during scene setup or spawned after sceneLoaded fires.
        var allStats = FindObjectsByType<PlayerStats>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        _stats = allStats.Length > 0 ? allStats[0] : null;

        // Fallback: find by "Player" tag if component search failed
        if (!_stats)
        {
            var go = GameObject.FindWithTag("Player");
            if (go) _stats = go.GetComponent<PlayerStats>();
        }

        if (!_stats)
            Debug.LogWarning("[SaveSystem] PlayerStats not found — stats and position will not be saved. " +
                "Ensure the player GameObject has the 'Player' tag.");

        var allAbilities = FindObjectsByType<PlayerAbilities>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        _abilities = allAbilities.Length > 0 ? allAbilities[0] : null;

        _quests = QuestManager.I;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  PUBLIC API
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Saves the current game state to the given slot (0–3).</summary>
    public void Save(int slot)
    {
        if (!ValidSlot(slot)) return;
        RebindRuntimeRefs();

        var data = new GameSaveData { savedAtUtc = DateTime.UtcNow.Ticks };

        WriteStats(data);
        WritePosition(data);
        WriteAbilities(data);
        WriteInventory(data);
        WriteEquipment(data);
        WriteWallet(data);
        WriteQuests(data);
        WriteWorldObjects(data);

        data.displayName = BuildDisplayName(data);

        string path = SlotPath(slot);
        File.WriteAllText(path, JsonUtility.ToJson(data, true));
        WriteSlotMeta(slot);

        Debug.Log($"[SaveSystem] Saved slot {slot} → {path}");
    }

    /// <summary>
    /// Loads the given slot.  Transitions to the saved scene if needed,
    /// then restores all game state once the scene is ready.
    /// </summary>
    public void Load(int slot)
    {
        if (!ValidSlot(slot)) return;

        string path = SlotPath(slot);
        if (!File.Exists(path)) { Debug.LogWarning($"[SaveSystem] No save in slot {slot}."); return; }

        GameSaveData data;
        try   { data = JsonUtility.FromJson<GameSaveData>(File.ReadAllText(path)); }
        catch { Debug.LogError($"[SaveSystem] Failed to parse save slot {slot}."); return; }

        if (data == null) return;

        _pendingLoad = data;
        ActiveSlot = slot;

        string target = data.playerPosition?.sceneName;
        if (string.IsNullOrEmpty(target)) target = defaultGameScene;

        if (SceneManager.GetActiveScene().name == target)
            ApplyPendingLoad();
        else
            SceneManager.LoadScene(target);
    }

    /// <summary>Loads the most recently saved slot, or starts a new game if none exist.</summary>
    public void Continue()
    {
        int last = GetLastUsedSlot();
        if (last >= 0 && HasSave(last)) Load(last);
        else NewGame();
    }

    /// <summary>Loads the default game scene without restoring any save data.</summary>
    public void NewGame()
    {
        _pendingLoad = null;

        // Claim the first empty slot for this new session.
        // If all slots are full, fall back to slot 0 (oldest save gets overwritten).
        ActiveSlot = 0;
        for (int i = 0; i < SlotCount; i++)
        {
            if (!HasSave(i)) { ActiveSlot = i; break; }
        }

        SceneManager.LoadScene(defaultGameScene);
    }

    /// <returns>True if a save file exists in this slot.</returns>
    public bool HasSave(int slot) => ValidSlot(slot) && File.Exists(SlotPath(slot));

    /// <summary>
    /// Returns the full save data for a slot — useful for showing info in the UI.
    /// Returns null if the slot is empty.
    /// </summary>
    public GameSaveData PeekSlot(int slot)
    {
        if (!HasSave(slot)) return null;
        try   { return JsonUtility.FromJson<GameSaveData>(File.ReadAllText(SlotPath(slot))); }
        catch { return null; }
    }

    /// <returns>Index of the last-saved slot, or -1 if none.</returns>
    public int GetLastUsedSlot()
    {
        string path = MetaPath();
        if (!File.Exists(path)) return -1;
        try   { return JsonUtility.FromJson<SaveSlotMeta>(File.ReadAllText(path))?.lastUsedSlot ?? -1; }
        catch { return -1; }
    }

    /// <summary>Permanently deletes the save file for this slot.</summary>
    public void DeleteSlot(int slot)
    {
        if (!ValidSlot(slot)) return;
        string path = SlotPath(slot);
        if (File.Exists(path)) { File.Delete(path); Debug.Log($"[SaveSystem] Deleted slot {slot}."); }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  WRITE HELPERS
    // ═══════════════════════════════════════════════════════════════════════════

    void WriteStats(GameSaveData d)
    {
        if (!_stats) return;

        // Strip equipment bonuses so we store only the BASE values.
        // StatsEquipmentBridge will re-add the bonuses when it re-enables on load.
        int eqM = 0, eqI = 0, eqT = 0;
        float eqC = 0f;
        if (_stats.GetComponent<StatsEquipmentBridge>() != null && equipment != null)
        {
            var (bm, bi, bc, bt) = equipment.GetTotalBonuses();
            eqM = bm; eqI = bi; eqT = bt; eqC = bc / 100f;
        }

        var health = _stats.GetComponent<PlayerHealth>();

        d.playerStats = new PlayerStatsSaveData
        {
            playerClass = _stats.playerClass.ToString(),
            level       = _stats.level,
            currentXP   = _stats.currentXP,
            xpToNext    = _stats.xpToNext,
            muscles     = _stats.muscles   - eqM,
            iq          = _stats.iq        - eqI,
            toughness   = _stats.toughness - eqT,
            critChance  = Mathf.Clamp01(_stats.critChance - eqC),
            currentHP   = health ? health.currentHP : 0,
        };
    }

    void WritePosition(GameSaveData d)
    {
        if (!_stats) return;
        var t = _stats.transform;
        d.playerPosition = new PlayerPositionSaveData
        {
            sceneName = SceneManager.GetActiveScene().name,
            x = t.position.x, y = t.position.y, z = t.position.z,
            rotY = t.eulerAngles.y,
        };
    }

    void WriteAbilities(GameSaveData d)
    {
        if (!_abilities) return;
        d.playerAbilities = new PlayerAbilitiesSaveData
        {
            learnedAbilities = new List<string>(_abilities.learnedAbilities),
        };
    }

    void WriteInventory(GameSaveData d)
    {
        if (!inventory) return;
        var inv = new InventoryData { capacity = inventory.capacity };
        foreach (var s in inventory.Slots)
        {
            var sd = new SlotData { occupied = !s.IsEmpty };
            if (sd.occupied) sd.item = ItemToData(s.item);
            inv.slots.Add(sd);
        }
        d.inventory = inv;
    }

    void WriteEquipment(GameSaveData d)
    {
        if (!equipment) return;
        var eq = new EquipmentData();
        foreach (var e in equipment.equipped)
        {
            var ed = new EquipEntryData { slot = e.slot, occupied = e.item != null };
            if (ed.occupied) ed.item = ItemToData(e.item);
            eq.equipped.Add(ed);
        }
        d.equipment = eq;
    }

    void WriteWallet(GameSaveData d)
    {
        if (!wallet) return;
        d.wallet = new WalletData { dollars = wallet.dollars };
    }

    void WriteQuests(GameSaveData d)
    {
        if (_quests == null) _quests = QuestManager.I;
        if (_quests == null) return;

        d.quests = new QuestSaveData
        {
            completedIds = new List<string>(_quests.completedIds),
        };

        foreach (var qi in _quests.active)
        {
            if (qi?.def == null) continue;
            d.quests.active.Add(new ActiveQuestEntry
            {
                questId  = qi.def.questId,
                progress = new List<int>(qi.progress),
            });
        }
    }

    void WriteWorldObjects(GameSaveData d)
    {
        d.worldObjects = new List<WorldObjectSaveData>();
        // includeInactive=true so we catch already-disabled objects
        var saveables = FindObjectsOfType<SaveableObject>(true);
        foreach (var s in saveables)
        {
            if (string.IsNullOrEmpty(s.objectId)) continue;
            d.worldObjects.Add(s.CollectState());
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  APPLY HELPERS
    // ═══════════════════════════════════════════════════════════════════════════

    void ApplyPendingLoad()
    {
        var data = _pendingLoad;
        _pendingLoad = null;
        RebindRuntimeRefs();

        // Order matters:
        //  1. Equipment  — bridge needs bonuses ready before it re-enables
        //  2. Stats      — bridge cycled to capture fresh base values
        //  3. Everything else
        //  4. Position last — teleport after world is set up
        ReadEquipment(data);
        ReadStats(data);
        ReadAbilities(data);
        ReadInventory(data);
        ReadWallet(data);
        ReadQuests(data);
        ReadWorldObjects(data);
        ReadPosition(data);

        Debug.Log($"[SaveSystem] Load complete. Scene: {SceneManager.GetActiveScene().name}");
    }

    void ReadStats(GameSaveData d)
    {
        if (!_stats || d.playerStats == null) return;
        var s = d.playerStats;

        // Disable the bridge so it doesn't fight our direct writes.
        // Re-enabling it causes OnEnable to capture the new base values
        // and call Reapply(), which adds equipment bonuses back on top.
        var bridge = _stats.GetComponent<StatsEquipmentBridge>();
        if (bridge) bridge.enabled = false;

        if (Enum.TryParse<PlayerStats.PlayerClass>(s.playerClass, out var cls))
            _stats.playerClass = cls;

        _stats.level      = s.level;
        _stats.currentXP  = s.currentXP;
        _stats.xpToNext   = s.xpToNext;
        _stats.muscles    = s.muscles;
        _stats.iq         = s.iq;
        _stats.toughness  = s.toughness;
        _stats.critChance = s.critChance;

        if (bridge) bridge.enabled = true;

        if (_abilities) _abilities.playerLevel = s.level;

        // Restore HP after equipment bonuses are re-applied so MaxHP is correct
        if (s.currentHP > 0)
        {
            var health = _stats.GetComponent<PlayerHealth>();
            if (health)
            {
                health.currentHP = Mathf.Clamp(s.currentHP, 0, health.stats ? health.stats.MaxHP : s.currentHP);
                PlayerHUD.TryUpdateHealth(health.currentHP, health.stats ? health.stats.MaxHP : health.currentHP);
            }
        }

        _stats.RaiseStatsChanged();
    }

    void ReadPosition(GameSaveData d)
    {
        if (!_stats || d.playerPosition == null) return;
        var p = d.playerPosition;
        _stats.transform.SetPositionAndRotation(
            new Vector3(p.x, p.y, p.z),
            Quaternion.Euler(0f, p.rotY, 0f));
    }

    void ReadAbilities(GameSaveData d)
    {
        if (!_abilities || d.playerAbilities == null) return;
        _abilities.learnedAbilities = new List<string>(d.playerAbilities.learnedAbilities);
    }

    void ReadInventory(GameSaveData d)
    {
        if (!inventory || d.inventory == null) return;
        inventory.capacity = d.inventory.capacity;

        var list = new List<ItemStack>();
        foreach (var sd in d.inventory.slots)
            list.Add(sd.occupied && sd.item != null ? new ItemStack(ItemFromData(sd.item), 1) : new ItemStack(null, 0));

        var field = typeof(Inventory).GetField("slots",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(inventory, list);

        inventory.MarkDirty();
    }

    void ReadEquipment(GameSaveData d)
    {
        if (!equipment || d.equipment == null) return;

        for (int i = 0; i < equipment.equipped.Count; i++)
        {
            var slot = equipment.equipped[i].slot;
            var src  = d.equipment.equipped.Find(x => x.slot == slot);
            equipment.equipped[i].item = (src != null && src.occupied && src.item != null)
                ? ItemFromData(src.item) : null;
        }
        equipment.NotifyChanged();
    }

    void ReadWallet(GameSaveData d)
    {
        if (!wallet || d.wallet == null) return;
        wallet.dollars = Mathf.Max(0, d.wallet.dollars);
        wallet.NotifyChanged();
    }

    void ReadQuests(GameSaveData d)
    {
        if (d.quests == null) return;
        if (_quests == null) _quests = QuestManager.I;
        if (_quests == null) { Debug.LogWarning("[SaveSystem] QuestManager not found — quests not restored."); return; }

        _quests.completedIds = new List<string>(d.quests.completedIds);
        _quests.active.Clear();

        foreach (var entry in d.quests.active)
        {
            if (string.IsNullOrEmpty(entry.questId)) continue;

            var def = questDatabase ? questDatabase.Get(entry.questId) : null;
            if (def == null) { Debug.LogWarning($"[SaveSystem] Quest '{entry.questId}' not in QuestDatabase — skipped."); continue; }

            var qi = new QuestInstance(def);
            for (int i = 0; i < entry.progress.Count && i < qi.progress.Count; i++)
                qi.progress[i] = entry.progress[i];

            _quests.active.Add(qi);
        }

        // Notify UI — but do NOT use OnChanged here as it can interfere with
        // other scene-load callbacks.  UI panels subscribe to OnChanged and will
        // pick this up on their own OnEnable.
        _quests.OnChanged?.Invoke();
    }

    void ReadWorldObjects(GameSaveData d)
    {
        if (d.worldObjects == null || d.worldObjects.Count == 0) return;

        var lookup = new Dictionary<string, WorldObjectSaveData>(d.worldObjects.Count);
        foreach (var w in d.worldObjects)
            if (!string.IsNullOrEmpty(w.objectId)) lookup[w.objectId] = w;

        var saveables = FindObjectsOfType<SaveableObject>(true);
        foreach (var so in saveables)
            if (lookup.TryGetValue(so.objectId, out var state))
                so.ApplyState(state);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  ITEM SERIALIZATION
    // ═══════════════════════════════════════════════════════════════════════════

    static ItemInstanceData ItemToData(ItemInstance it) => new ItemInstanceData
    {
        templateId     = it.template?.id,
        itemLevel      = it.itemLevel,
        requiredLevel  = it.requiredLevel,
        rarity         = it.rarity,
        affix          = it.affix,
        bonusMuscles   = it.bonusMuscles,
        bonusIQ        = it.bonusIQ,
        bonusCrit      = it.bonusCrit,
        bonusToughness = it.bonusToughness,
        value          = it.value,
    };

    ItemInstance ItemFromData(ItemInstanceData d) => new ItemInstance
    {
        template       = GetItemDatabase()?.Get(d.templateId),
        itemLevel      = d.itemLevel,
        requiredLevel  = d.requiredLevel,
        rarity         = d.rarity,
        affix          = d.affix,
        bonusMuscles   = d.bonusMuscles,
        bonusIQ        = d.bonusIQ,
        bonusCrit      = d.bonusCrit,
        bonusToughness = d.bonusToughness,
        value          = d.value,
    };

    // ═══════════════════════════════════════════════════════════════════════════
    //  UTILITIES
    // ═══════════════════════════════════════════════════════════════════════════

    static string SlotPath(int slot) =>
        Path.Combine(Application.persistentDataPath, string.Format(SlotFile, slot));

    static string MetaPath() =>
        Path.Combine(Application.persistentDataPath, MetaFile);

    void WriteSlotMeta(int slot)
    {
        try { File.WriteAllText(MetaPath(), JsonUtility.ToJson(new SaveSlotMeta { lastUsedSlot = slot })); }
        catch (Exception e) { Debug.LogWarning($"[SaveSystem] Could not write meta: {e.Message}"); }
    }

    static bool ValidSlot(int slot)
    {
        if (slot >= 0 && slot < SlotCount) return true;
        Debug.LogWarning($"[SaveSystem] Invalid slot {slot} (must be 0–{SlotCount - 1})");
        return false;
    }

    static string BuildDisplayName(GameSaveData d)
    {
        string cls   = d.playerStats?.playerClass ?? "?";
        int    level = d.playerStats?.level ?? 0;
        string scene = d.playerPosition?.sceneName ?? "?";
        return $"Level {level} {cls}  –  {scene}";
    }
}