// QuestGiver.cs
using UnityEngine;

[DisallowMultipleComponent]
public class QuestGiver : MonoBehaviour
{
    [Header("IDs")]
    public string npcId = "NPC_Giver";
    public QuestDefinition quest;

    [Header("Markers (optional)")]
    public GameObject markExclamation;   // show when available
    public GameObject markQSilver;       // active
    public GameObject markQYellow;       // ready to turn in

    [Header("Offer UI")]
    public QuestOfferUI offerUI;         // drag the panel script here

    [Header("Optional: Enable these when quest is accepted")]
    public GameObject[] enableOnAccept;

    bool _lastActive, _lastReady, _lastCompleted;

    void Start(){ RefreshMarkers(); if (offerUI) offerUI.Hide(); }

    void LateUpdate()
    {
        if (!quest || QuestManager.I == null) return;

        bool completed   = QuestManager.I.IsCompleted(quest.questId);
        bool active      = QuestManager.I.HasActive(quest.questId);
        bool readyToTurn = false;

        if (active){
            var qi = QuestManager.I.active.Find(q => q.def.questId == quest.questId);
            readyToTurn = qi != null && qi.IsComplete && quest.turnInNpcId == npcId;
        }

        if (completed != _lastCompleted || active != _lastActive || readyToTurn != _lastReady)
        {
            _lastCompleted = completed;
            _lastActive = active;
            _lastReady = readyToTurn;
            RefreshMarkers(); // reuse your existing toggle logic
        }
    }

    public void TryOpenOffer()
    {
        if (!quest) return;
        if (QuestManager.I.IsCompleted(quest.questId)) return;

        // Fallback: if no reference set, try to find any QuestOfferUI in scene, including inactive ones
        if (!offerUI)
            offerUI = FindObjectOfType<QuestOfferUI>(true); // 'true' includes inactive

        if (!QuestManager.I.HasActive(quest.questId))
        {
            if (offerUI) offerUI.ShowOffer(quest, this);
        }
        else
        {
            var qi = QuestManager.I.active.Find(q => q.def.questId == quest.questId);
            bool ready = qi != null && qi.IsComplete && quest.turnInNpcId == npcId;
            if (ready)
            {
                if (QuestManager.I.TryTurnIn(quest.questId))
                    RefreshMarkers();
            }
        }
    }

    public void OnAccepted(){
    QuestManager.I.Accept(quest);
    if (enableOnAccept != null)
        foreach (var go in enableOnAccept) if (go) go.SetActive(true);
    RefreshMarkers();
    }
    public void OnDeclined(){
        RefreshMarkers();
    }

    public void RefreshMarkers(){
        bool completed   = QuestManager.I.IsCompleted(quest ? quest.questId : "");
        bool active      = QuestManager.I.HasActive(quest ? quest.questId : "");
        bool readyToTurn = false;
        if (active){
            var qi = QuestManager.I.active.Find(q=>q.def.questId==quest.questId);
            readyToTurn = qi!=null && qi.IsComplete && quest.turnInNpcId == npcId;
        }

        if (markExclamation) markExclamation.SetActive(!active && !completed && quest);
        if (markQSilver)     markQSilver.SetActive(active && !readyToTurn);
        if (markQYellow)     markQYellow.SetActive(readyToTurn);
    }

    void OnEnable()
    {
    if (QuestManager.I)
        {
            QuestManager.I.OnChanged += RefreshMarkers; 
            QuestManager.I.OnQuestProgress += OnQuestProgress;
        }
    }
    void OnDisable()
    {
    if (QuestManager.I)
        {
            QuestManager.I.OnChanged -= RefreshMarkers;
            QuestManager.I.OnQuestProgress -= OnQuestProgress;  // NEW
        }
    }
    void OnQuestProgress(string questId)
    {
        if (quest && quest.questId == questId) RefreshMarkers();
    }
}
