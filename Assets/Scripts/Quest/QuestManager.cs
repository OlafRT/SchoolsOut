using UnityEngine.SceneManagement;
using System.Collections.Generic;
using UnityEngine;

public class QuestManager : MonoBehaviour
{
    public static QuestManager I;

    [Header("Runtime")]
    public List<QuestInstance> active = new();
    public List<string> completedIds = new();

    [Header("Refs")]
    [Tooltip("Leave empty — found automatically (MonoBehaviour in scene).")]
    public PlayerStats playerStats;
    [Tooltip("Assign your PlayerWallet ScriptableObject asset here. DO NOT leave empty.")]
    public PlayerWallet wallet;
    [Tooltip("Assign your PlayerInventory ScriptableObject asset here. DO NOT leave empty.")]
    public Inventory inventory;
    [Tooltip("Leave empty — found automatically (MonoBehaviour in scene).")]
    public ScreenToast toast;

    public System.Action OnChanged;
    public System.Action<string,string> OnProgress;
    public System.Action<string> OnQuestProgress;

    void Awake()
    {
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }

        I = this;
        DontDestroyOnLoad(gameObject);

        QuestEvents.EnemyKilled  += OnEnemyKilled;
        QuestEvents.ItemLooted   += OnItemLooted;
        QuestEvents.PlaceReached += OnPlaceReached;
        QuestEvents.NpcTalked    += OnNpcTalked;
        QuestEvents.ItemRemoved  += OnItemRemoved;

        SceneManager.sceneLoaded += OnSceneLoaded;

        RebindSceneReferences();
    }

    void OnDestroy()
    {
        QuestEvents.EnemyKilled  -= OnEnemyKilled;
        QuestEvents.ItemLooted   -= OnItemLooted;
        QuestEvents.PlaceReached -= OnPlaceReached;
        QuestEvents.NpcTalked    -= OnNpcTalked;
        QuestEvents.ItemRemoved  -= OnItemRemoved;

        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    public bool HasActive(string questId)
    {
        foreach (var q in active) if (q.def.questId == questId) return true;
        return false;
    }

    public bool IsCompleted(string questId) => completedIds.Contains(questId);

    public void Accept(QuestDefinition def)
    {
        if (def == null || HasActive(def.questId) || IsCompleted(def.questId)) return;
        var qi = new QuestInstance(def);
        active.Add(qi);

        // Catch items the player already has
        if (inventory != null)
        {
            foreach (var o in def.objectives)
            {
                if (o.type != QuestDefinition.ObjectiveSpec.Type.Collect) continue;
                int alreadyHave = inventory.CountByItemId(o.targetId);
                if (alreadyHave > 0)
                    BumpAll(QuestDefinition.ObjectiveSpec.Type.Collect, o.targetId, alreadyHave);
            }
        }

        OnChanged?.Invoke();
        RefreshActivatedObjects();
        OnQuestProgress?.Invoke(def.questId);
        OnProgress?.Invoke(def.questId, BuildProgressLine(qi));
    }

    public void Abandon(string questId)
    {
        active.RemoveAll(q => q.def.questId == questId);
        OnChanged?.Invoke();
    }

    public bool TryTurnIn(string questId)
    {
        var qi = active.Find(q => q.def.questId == questId);
        if (qi == null || !qi.IsComplete) return false;

        // --- Validate required collect items still exist ---
        if (inventory && qi.def != null && qi.def.objectives != null)
        {
            for (int i = 0; i < qi.def.objectives.Count; i++)
            {
                var o = qi.def.objectives[i];
                if (o.type != QuestDefinition.ObjectiveSpec.Type.Collect) continue;

                int req = Mathf.Max(1, o.requiredCount);
                if (inventory.CountByItemId(o.targetId) < req)
                    return false;
            }
        }

        // --- Consume collect items FIRST so their slots are free for rewards ---
        if (inventory && qi.def != null && qi.def.objectives != null)
        {
            for (int i = 0; i < qi.def.objectives.Count; i++)
            {
                var o = qi.def.objectives[i];
                if (o.type != QuestDefinition.ObjectiveSpec.Type.Collect) continue;

                int req = Mathf.Max(1, o.requiredCount);
                inventory.RemoveByItemId(o.targetId, req);
            }
        }

        // --- Rewards ---
        // Lazy re-find: RebindSceneReferences() runs on sceneLoaded, but if the scene
        // transition timing was tight, playerStats may still be null here. Re-find once
        // so XP is never silently dropped.
        if (playerStats == null)
            playerStats = FindFirstObjectByType<PlayerStats>();

        if (playerStats) playerStats.AddXP(Mathf.Max(0, qi.def.xpReward));
        if (wallet)      wallet.Add(Mathf.Max(0, qi.def.moneyReward));

        // --- Item reward ---
        if (inventory && qi.def.itemReward)
        {
            var inst = new ItemInstance {
                template       = qi.def.itemReward,
                customName     = string.IsNullOrEmpty(qi.def.itemReward.overrideName) ? null : qi.def.itemReward.overrideName,
                itemLevel      = qi.def.itemReward.isStaticItem ? qi.def.itemReward.fixedItemLevel : 1,
                requiredLevel  = qi.def.itemReward.isStaticItem ? qi.def.itemReward.fixedRequiredLevel
                                : ItemLevelRules.RequiredLevelForItemLevel(1),
                rarity         = qi.def.itemReward.isStaticItem ? qi.def.itemReward.fixedRarity : qi.def.itemReward.rarity,
                affix          = qi.def.itemReward.isStaticItem ? qi.def.itemReward.forcedAffix : AffixType.None,
                bonusMuscles   = qi.def.itemReward.isStaticItem ? qi.def.itemReward.fixedMuscles : 0,
                bonusIQ        = qi.def.itemReward.isStaticItem ? qi.def.itemReward.fixedIQ : 0,
                bonusCrit      = qi.def.itemReward.isStaticItem ? qi.def.itemReward.fixedCrit : 0,
                bonusToughness = qi.def.itemReward.isStaticItem ? qi.def.itemReward.fixedToughness : 0,
                value          = qi.def.itemReward.fixedValue
            };

            for (int n = 0; n < Mathf.Max(1, qi.def.itemAmount); n++)
            {
                if (!inventory.Add(inst, 1))
                {
                    toast?.Show("My backpack is full!", UnityEngine.Color.yellow);
                    return false;
                }
            }
        }

        completedIds.Add(qi.def.questId);
        active.Remove(qi);
        OnChanged?.Invoke();
        RefreshActivatedObjects();
        return true;
    }

    // ── Progress handlers ─────────────────────────────────────────────────────

    void OnEnemyKilled(string enemyId) => BumpAll(QuestDefinition.ObjectiveSpec.Type.Kill, enemyId, 1);

    void OnItemLooted(string itemId, int amount) =>
        BumpAll(QuestDefinition.ObjectiveSpec.Type.Collect, itemId, Mathf.Max(1, amount));

    void OnItemRemoved(string itemId, int amount)
    {
        bool changed = false;
        var changedIds = new HashSet<string>();

        foreach (var qi in active)
        {
            for (int i = 0; i < qi.def.objectives.Count; i++)
            {
                var o = qi.def.objectives[i];
                if (o.type != QuestDefinition.ObjectiveSpec.Type.Collect) continue;
                if (o.targetId != itemId) continue;

                int before = qi.progress[i];
                qi.progress[i] = Mathf.Max(0, before - amount);

                if (qi.progress[i] != before)
                {
                    changed = true;
                    changedIds.Add(qi.def.questId);
                }
            }
        }

        if (!changed) return;

        OnChanged?.Invoke();
        foreach (var qid in changedIds)
        {
            var qi2 = active.Find(q => q.def.questId == qid);
            if (qi2 != null) { OnProgress?.Invoke(qid, BuildProgressLine(qi2)); OnQuestProgress?.Invoke(qid); }
        }
    }

    void OnPlaceReached(string placeId) => BumpAll(QuestDefinition.ObjectiveSpec.Type.Reach, placeId, 1, capToReq: true);
    void OnNpcTalked(string npcId)      => BumpAll(QuestDefinition.ObjectiveSpec.Type.Talk,  npcId,  1, capToReq: true);

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RebindSceneReferences();
        OnChanged?.Invoke();
        // Refresh world objects that start disabled — they can't self-subscribe
        // so they won't respond to OnChanged. This covers respawns and scene reloads.
        RefreshActivatedObjects();
    }

    void RebindSceneReferences()
    {
        // Only rebind MonoBehaviours — they live in the scene and change when scenes load.
        // DO NOT rebind ScriptableObjects (wallet, inventory) — they are project assets
        // assigned once in the Inspector and FindFirstObjectByType returns null for them,
        // which would silently break rewards every time a scene loads.
        playerStats = FindFirstObjectByType<PlayerStats>();
        toast       = FindFirstObjectByType<ScreenToast>();
    }

    string BuildProgressLine(QuestInstance qi)
    {
        if (qi.def.objectives.Count == 0) return "";

        var lines = new System.Text.StringBuilder();
        for (int i = 0; i < qi.def.objectives.Count; i++)
        {
            var o = qi.def.objectives[i];
            int req = (o.type == QuestDefinition.ObjectiveSpec.Type.Reach ||
                       o.type == QuestDefinition.ObjectiveSpec.Type.Talk) ? 1 : o.requiredCount;
            int cur = Mathf.Min(req, qi.progress[i]);

            string line = o.type switch {
                QuestDefinition.ObjectiveSpec.Type.Kill    => $"{cur}/{req} {o.targetId} defeated",
                QuestDefinition.ObjectiveSpec.Type.Collect => $"{cur}/{req} {o.targetId} collected",
                QuestDefinition.ObjectiveSpec.Type.Reach   => cur >= req ? $"Reached {o.targetId}" : $"Reach {o.targetId}",
                QuestDefinition.ObjectiveSpec.Type.Talk    => cur >= req ? $"Talked to {o.targetId}" : $"Talk to {o.targetId}",
                _ => ""
            };

            if (lines.Length > 0) lines.Append("\n");
            lines.Append(line);
        }
        return lines.ToString();
    }

    void BumpAll(QuestDefinition.ObjectiveSpec.Type t, string id, int delta, bool capToReq = false)
    {
        bool changed = false;
        var changedIds = new HashSet<string>();

        foreach (var qi in active)
        {
            for (int i = 0; i < qi.def.objectives.Count; i++)
            {
                var o = qi.def.objectives[i];
                if (o.type != t || o.targetId != id) continue;

                int before = qi.progress[i];
                int after  = before + delta;

                if (capToReq)
                {
                    int req = (o.type == QuestDefinition.ObjectiveSpec.Type.Reach ||
                               o.type == QuestDefinition.ObjectiveSpec.Type.Talk) ? 1 : o.requiredCount;
                    after = Mathf.Min(req, after);
                }

                qi.progress[i] = Mathf.Max(before, after);

                if (qi.progress[i] != before)
                {
                    changed = true;
                    changedIds.Add(qi.def.questId);
                }
            }
        }

        if (!changed) return;

        OnChanged?.Invoke();
        foreach (var qid in changedIds)
        {
            var qi = active.Find(q => q.def.questId == qid);
            if (qi != null) { OnProgress?.Invoke(qid, BuildProgressLine(qi)); OnQuestProgress?.Invoke(qid); }
        }
    }

    // Called after any quest state change to update ALL QuestActivatedObjects
    // in the scene, including those that start disabled (which can't self-subscribe).
    // Public so GameSaveManager can call it after restoring quest data on load,
    // which guarantees correct QAO state regardless of sceneLoaded callback order.
    public static void RefreshActivatedObjects()
    {
        var all = FindObjectsByType<QuestActivatedObject>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var obj in all)
            obj.ApplyQuestState();
    }
}
