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
        var qm = QuestManager.I;
        if (qm == null) { Debug.Log($"[QAO] {name}: QuestManager is null"); return; }

        bool onMet  = !string.IsNullOrEmpty(questId)    && EvaluateCondition(qm, questId, condition);
        bool offMet = !string.IsNullOrEmpty(offQuestId)  && EvaluateCondition(qm, offQuestId, offCondition);

        Debug.Log($"[QAO] {name}: onMet={onMet} offMet={offMet} | " +
                $"HasActive('{questId}')={qm.HasActive(questId)} " +
                $"IsCompleted('{questId}')={qm.IsCompleted(questId)}");

        // Bail only if there's genuinely nothing to evaluate
        if (string.IsNullOrEmpty(questId) && string.IsNullOrEmpty(offQuestId)) return;

        // Off condition takes priority
        if (!string.IsNullOrEmpty(offQuestId) && EvaluateCondition(qm, offQuestId, offCondition))
        {
            gameObject.SetActive(false);
            return;
        }

        // If there's no on-condition, default to active
        if (string.IsNullOrEmpty(questId))
        {
            gameObject.SetActive(true);
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