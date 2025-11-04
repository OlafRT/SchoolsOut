// QuestStateActivator.cs
using UnityEngine;

public class QuestStateActivator : MonoBehaviour
{
    public enum Mode
    {
        EnableWhenActive,        // accepted but not turned in (includes ready-to-turn)
        EnableWhenReadyToTurn,   // objective complete, before turn-in
        EnableWhenCompleted,     // after turn-in
        EnableWhenNotAccepted    // before the quest is accepted
    }

    [Header("Which quest should control these targets?")]
    public string questId;                   // e.g. "defeat_rats_10"
    public QuestGiver fallbackGiver;         // optional: takes questId from this giver if left blank

    [Header("What to toggle")]
    public Mode mode = Mode.EnableWhenActive;
    public GameObject[] targets;

    void OnEnable()
    {
        TrySubscribe(true);
        Refresh();
    }

    void OnDisable()
    {
        TrySubscribe(false);
    }

    void TrySubscribe(bool on)
    {
        if (!QuestManager.I) return;
        if (on)
        {
            QuestManager.I.OnChanged       += Refresh;
            QuestManager.I.OnQuestProgress += OnQuestProgress;
        }
        else
        {
            QuestManager.I.OnChanged       -= Refresh;
            QuestManager.I.OnQuestProgress -= OnQuestProgress;
        }
    }

    void OnQuestProgress(string changedQuestId)
    {
        if (GetQuestId() == changedQuestId) Refresh();
    }

    string GetQuestId()
    {
        if (!string.IsNullOrEmpty(questId)) return questId;
        if (fallbackGiver && fallbackGiver.quest) return fallbackGiver.quest.questId;
        return "";
    }

    public void Refresh()
    {
        if (!QuestManager.I) return;

        string id = GetQuestId();
        if (string.IsNullOrEmpty(id)) return;

        bool isCompleted = QuestManager.I.IsCompleted(id);
        bool isActive    = QuestManager.I.HasActive(id);

        bool readyToTurn = false;
        if (isActive)
        {
            var qi = QuestManager.I.active.Find(q => q.def.questId == id);
            readyToTurn = (qi != null && qi.IsComplete);
        }

        bool enable = false;
        switch (mode)
        {
            case Mode.EnableWhenActive:       enable = isActive && !isCompleted; break; // includes readyToTurn
            case Mode.EnableWhenReadyToTurn:  enable = isActive && readyToTurn;  break;
            case Mode.EnableWhenCompleted:    enable = isCompleted;              break;
            case Mode.EnableWhenNotAccepted:  enable = !isActive && !isCompleted;break;
        }

        if (targets != null)
            foreach (var t in targets) if (t) t.SetActive(enable);
    }
}
