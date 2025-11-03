// QuestManager.cs
using System.Collections.Generic;
using UnityEngine;

public class QuestManager : MonoBehaviour
{
    public static QuestManager I;

    [Header("Runtime")]
    public List<QuestInstance> active = new();
    public List<string> completedIds = new();

    [Header("Refs")]
    public PlayerStats playerStats;       // drop your PlayerStats here
    public PlayerWallet wallet;           // ScriptableObject
    public Inventory inventory;           // ScriptableObject (optional)

    public System.Action OnChanged;       // UI refresh hook
    public System.Action<string,string> OnProgress;   // (questId, "1/5 rats defeated")
    public System.Action<string> OnQuestProgress;  

    void Awake(){
        I = this;
        QuestEvents.EnemyKilled += OnEnemyKilled;
        QuestEvents.ItemLooted  += OnItemLooted;
        QuestEvents.PlaceReached+= OnPlaceReached;
        QuestEvents.NpcTalked   += OnNpcTalked;
    }
    void OnDestroy(){
        QuestEvents.EnemyKilled -= OnEnemyKilled;
        QuestEvents.ItemLooted  -= OnItemLooted;
        QuestEvents.PlaceReached-= OnPlaceReached;
        QuestEvents.NpcTalked   -= OnNpcTalked;
    }

    public bool HasActive(string questId){
        foreach (var q in active) if (q.def.questId == questId) return true;
        return false;
    }
    public bool IsCompleted(string questId) => completedIds.Contains(questId);

    public void Accept(QuestDefinition def){
    if (def==null || HasActive(def.questId) || IsCompleted(def.questId)) return;
    var qi = new QuestInstance(def);
    active.Add(qi);
    OnChanged?.Invoke();
    OnQuestProgress?.Invoke(def.questId);                 // ping now
    OnProgress?.Invoke(def.questId, BuildProgressLine(qi)); // seed toast once
    }

    public void Abandon(string questId){
        active.RemoveAll(q => q.def.questId == questId);
        OnChanged?.Invoke();
    }

    public bool TryTurnIn(string questId){
        var qi = active.Find(q => q.def.questId == questId);
        if (qi==null || !qi.IsComplete) return false;

        // rewards
        if (playerStats) playerStats.AddXP(Mathf.Max(0, qi.def.xpReward));               // :contentReference[oaicite:0]{index=0}
        if (wallet) wallet.Add(Mathf.Max(0, qi.def.moneyReward));                        // :contentReference[oaicite:1]{index=1}

        completedIds.Add(qi.def.questId);
        active.Remove(qi);
        OnChanged?.Invoke();
        return true;
    }

    // --- Progress handling ---
    void OnEnemyKilled(string enemyId){
        BumpAll(QuestDefinition.ObjectiveSpec.Type.Kill, enemyId, 1);
    }
    void OnItemLooted(string itemId, int amount){
        BumpAll(QuestDefinition.ObjectiveSpec.Type.Collect, itemId, Mathf.Max(1,amount));
    }
    void OnPlaceReached(string placeId){
        BumpAll(QuestDefinition.ObjectiveSpec.Type.Reach, placeId, 1, capToReq:true);
    }
    void OnNpcTalked(string npcId){
        BumpAll(QuestDefinition.ObjectiveSpec.Type.Talk, npcId, 1, capToReq:true);
    }
    string BuildProgressLine(QuestInstance qi)
    {
        if (qi.def.objectives.Count == 0) return "";
        var o = qi.def.objectives[0];
        int req = (o.type==QuestDefinition.ObjectiveSpec.Type.Reach || o.type==QuestDefinition.ObjectiveSpec.Type.Talk) ? 1 : o.requiredCount;
        int cur = Mathf.Min(req, qi.progress[0]);

        switch (o.type)
        {
            case QuestDefinition.ObjectiveSpec.Type.Kill:    return $"{cur}/{req} {o.targetId} defeated";
            case QuestDefinition.ObjectiveSpec.Type.Collect: return $"{cur}/{req} {o.targetId} collected";
            case QuestDefinition.ObjectiveSpec.Type.Reach:   return cur>=req ? $"Reached {o.targetId}" : $"Reach {o.targetId}";
            case QuestDefinition.ObjectiveSpec.Type.Talk:    return cur>=req ? $"Talked to {o.targetId}" : $"Talk to {o.targetId}";
        }
        return "";
    }

    void BumpAll(QuestDefinition.ObjectiveSpec.Type t, string id, int delta, bool capToReq=false){
    bool changed = false;
    var changedIds = new System.Collections.Generic.HashSet<string>();

    foreach (var qi in active){
        for (int i=0;i<qi.def.objectives.Count;i++){
            var o = qi.def.objectives[i];
            if (o.type != t || o.targetId != id) continue;

            int before = qi.progress[i];
            int after  = before + delta;

            if (capToReq){
                int req = (o.type==QuestDefinition.ObjectiveSpec.Type.Reach || o.type==QuestDefinition.ObjectiveSpec.Type.Talk) ? 1 : o.requiredCount;
                after = Mathf.Min(req, after);
            }
            qi.progress[i] = Mathf.Max(before, after);

            if (qi.progress[i] != before){
                changed = true;
                changedIds.Add(qi.def.questId);
            }
        }
    }

    if (!changed) return;

    OnChanged?.Invoke();

    // emit toasts + per-giver pings
    foreach (var qid in changedIds){
        var qi = active.Find(q => q.def.questId == qid);
        if (qi != null){
            OnProgress?.Invoke(qid, BuildProgressLine(qi));
            OnQuestProgress?.Invoke(qid);
        }
    }
    }
}
