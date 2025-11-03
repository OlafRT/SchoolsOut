using UnityEngine;
using TMPro;

public class QuestLogRow : MonoBehaviour
{
    [Header("Texts (auto-find by name if left empty)")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descText;
    [SerializeField] private TMP_Text progressText;

    bool triedAutoFind = false;

    void AutoFind()
    {
        if (triedAutoFind) return;
        triedAutoFind = true;

        if (!titleText)    titleText    = transform.Find("Title")?.GetComponent<TMP_Text>();
        if (!descText)     descText     = transform.Find("Description")?.GetComponent<TMP_Text>();
        if (!progressText) progressText = transform.Find("Progress")?.GetComponent<TMP_Text>();

        if (!titleText || !descText || !progressText)
            Debug.LogWarning($"[QuestLogRow] Could not auto-find one or more TMP_Texts on '{name}'. " +
                             "Name them exactly 'Title', 'Description', 'Progress' or assign in Inspector.");
    }

    public void Bind(QuestInstance qi)
    {
        if (qi == null || qi.def == null) return;
        AutoFind();

        bool isReady = qi.IsComplete;
        bool isTurnedIn = QuestManager.I.IsCompleted(qi.def.questId);

        if (titleText) titleText.text = qi.def.title;

        if (descText)
        {
            if (!isTurnedIn && isReady)
                descText.text = $"Go to {qi.def.turnInNpcId} for your reward.";
            else
                descText.text = qi.def.description;
        }

        if (progressText)
            progressText.text = isReady && !isTurnedIn ? "Complete!" : BuildProgressLine(qi);
    }

    string BuildProgressLine(QuestInstance qi)
    {
        if (qi.def.objectives.Count == 0) return "";
        var o = qi.def.objectives[0];
        int req = (o.type == QuestDefinition.ObjectiveSpec.Type.Reach ||
                   o.type == QuestDefinition.ObjectiveSpec.Type.Talk) ? 1 : o.requiredCount;
        int cur = Mathf.Min(req, qi.progress[0]);

        switch (o.type)
        {
            case QuestDefinition.ObjectiveSpec.Type.Kill:    return $"{cur}/{req} {o.targetId} defeated";
            case QuestDefinition.ObjectiveSpec.Type.Collect: return $"{cur}/{req} {o.targetId} collected";
            case QuestDefinition.ObjectiveSpec.Type.Reach:   return cur >= req ? $"Reached {o.targetId}" : $"Reach {o.targetId}";
            case QuestDefinition.ObjectiveSpec.Type.Talk:    return cur >= req ? $"Talked to {o.targetId}" : $"Talk to {o.targetId}";
        }
        return "";
    }
}

