// QuestActivatedObject.cs
using UnityEngine;

[DisallowMultipleComponent]
public class QuestActivatedObject : MonoBehaviour
{
    public enum ActivationCondition
    {
        QuestActive,
        QuestCompleted,
        QuestAcceptedOrDone,
        NotStarted,
        NotCompleted,
    }

    [Header("Turn ON when...")]
    public string questId;
    public ActivationCondition condition = ActivationCondition.QuestActive;

    [Header("Turn OFF when... (optional)")]
    [Tooltip("If set, this object will be forced inactive when this second quest meets its condition — " +
             "useful when one quest enables this object and a later quest should disable it.")]
    public string offQuestId;
    public ActivationCondition offCondition = ActivationCondition.QuestCompleted;

    void Start()
    {
        ApplyQuestState();
    }

    void OnEnable()
    {
        if (QuestManager.I != null)
            QuestManager.I.OnChanged += ApplyQuestState;
    }

    void OnDisable()
    {
        if (QuestManager.I != null)
            QuestManager.I.OnChanged -= ApplyQuestState;
    }

    public void ApplyQuestState()
    {
        if (string.IsNullOrEmpty(questId)) return;
        var qm = QuestManager.I;
        if (qm == null) return;

        // If the "off" quest condition is met, that takes priority
        if (!string.IsNullOrEmpty(offQuestId) && EvaluateCondition(qm, offQuestId, offCondition))
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(EvaluateCondition(qm, questId, condition));
    }

    bool EvaluateCondition(QuestManager qm, string id, ActivationCondition cond)
    {
        bool active    = qm.HasActive(id);
        bool completed = qm.IsCompleted(id);

        return cond switch
        {
            ActivationCondition.QuestActive         => active,
            ActivationCondition.QuestCompleted      => completed,
            ActivationCondition.QuestAcceptedOrDone => active || completed,
            ActivationCondition.NotStarted          => !active && !completed,
            ActivationCondition.NotCompleted        => !completed,
            _                                       => false,
        };
    }
}