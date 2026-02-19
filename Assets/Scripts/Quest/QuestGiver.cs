// QuestGiver.cs
using UnityEngine;

[DisallowMultipleComponent]
public class QuestGiver : MonoBehaviour
{
    [Header("IDs")]
    public string npcId = "NPC_Giver";
    public QuestDefinition quest;

    [Header("Role")]
    [Tooltip("If false, this NPC will never OFFER the quest (no exclamation, no offer UI). It can still be the TURN-IN npc.")]
    public bool canOfferQuest = true;

    [Header("Markers (optional)")]
    public GameObject markExclamation;
    public GameObject markQSilver;
    public GameObject markQYellow;

    [Header("Offer UI")]
    public QuestOfferUI offerUI;

    [Header("Optional: Enable/Disable when quest is accepted")]
    public GameObject[] enableOnAccept;
    public GameObject[] disableOnAccept;

    [Header("Optional: Enable/Disable when quest is completed")]
    public GameObject[] enableOnComplete;
    public GameObject[] disableOnComplete;

    bool _lastActive, _lastReady, _lastCompleted;
    bool _completeEffectsApplied;

    void Start()
    {
        RefreshMarkers();
        if (offerUI) offerUI.Hide();

        if (quest && QuestManager.I != null && QuestManager.I.IsCompleted(quest.questId))
            ApplyCompleteEffects();
    }

    void LateUpdate()
    {
        if (!quest || QuestManager.I == null) return;

        bool completed = QuestManager.I.IsCompleted(quest.questId);
        bool active = QuestManager.I.HasActive(quest.questId);

        bool readyToTurn = false;
        if (active)
        {
            var qi = QuestManager.I.active.Find(q => q.def.questId == quest.questId);
            readyToTurn = qi != null && qi.IsComplete && quest.turnInNpcId == npcId;
        }

        if (completed && !_lastCompleted)
            ApplyCompleteEffects();

        if (completed != _lastCompleted || active != _lastActive || readyToTurn != _lastReady)
        {
            _lastCompleted = completed;
            _lastActive = active;
            _lastReady = readyToTurn;
            RefreshMarkers();
        }
    }

    public void TryOpenOffer()
    {
        if (!quest) return;
        if (QuestManager.I.IsCompleted(quest.questId)) return;

        if (!offerUI)
            offerUI = FindObjectOfType<QuestOfferUI>(true);

        bool active = QuestManager.I.HasActive(quest.questId);

        // 1) Offer only if this NPC is allowed to offer
        if (!active)
        {
            if (canOfferQuest && offerUI)
                offerUI.ShowOffer(quest, this);

            return;
        }

        // 2) Turn-in only at the correct turn-in NPC
        var qi = QuestManager.I.active.Find(q => q.def.questId == quest.questId);
        bool ready = qi != null && qi.IsComplete && quest.turnInNpcId == npcId;

        if (ready)
        {
            // IMPORTANT CHANGE:
            // Show the "Turn In" UI instead of silently turning in.
            if (offerUI) offerUI.ShowTurnIn(quest, this);
            else OnTurnInConfirmed();
        }
    }

    // Called by QuestOfferUI when player presses "Turn In"
    public void OnTurnInConfirmed()
    {
        if (!quest) return;
        if (QuestManager.I == null) return;

        if (QuestManager.I.TryTurnIn(quest.questId))
        {
            ApplyCompleteEffects();
            RefreshMarkers();
        }
    }

    public void OnAccepted()
    {
        QuestManager.I.Accept(quest);

        if (enableOnAccept != null)
            foreach (var go in enableOnAccept)
                if (go) go.SetActive(true);

        if (disableOnAccept != null)
            foreach (var go in disableOnAccept)
                if (go) go.SetActive(false);

        RefreshMarkers();
    }

    public void OnDeclined()
    {
        RefreshMarkers();
    }

    void ApplyCompleteEffects()
    {
        if (_completeEffectsApplied) return;
        _completeEffectsApplied = true;

        if (enableOnComplete != null)
            foreach (var go in enableOnComplete)
                if (go) go.SetActive(true);

        if (disableOnComplete != null)
            foreach (var go in disableOnComplete)
                if (go) go.SetActive(false);
    }

    public void RefreshMarkers()
    {
        bool hasQuest = quest != null && QuestManager.I != null;
        bool completed = hasQuest && QuestManager.I.IsCompleted(quest.questId);
        bool active = hasQuest && QuestManager.I.HasActive(quest.questId);

        bool isTurnInNpc = hasQuest && quest.turnInNpcId == npcId;

        bool readyToTurn = false;
        if (active)
        {
            var qi = QuestManager.I.active.Find(q => q.def.questId == quest.questId);
            readyToTurn = qi != null && qi.IsComplete && isTurnInNpc;
        }

        if (markExclamation)
            markExclamation.SetActive(canOfferQuest && !active && !completed && hasQuest);

        bool showSilver = active && !readyToTurn && (canOfferQuest || isTurnInNpc);
        if (markQSilver) markQSilver.SetActive(showSilver);

        if (markQYellow) markQYellow.SetActive(readyToTurn);
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
            QuestManager.I.OnQuestProgress -= OnQuestProgress;
        }
    }

    void OnQuestProgress(string questId)
    {
        if (quest && quest.questId == questId) RefreshMarkers();
    }
}
