using UnityEngine;

public class QuestLogUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject panel;            // optional: the whole log panel
    [SerializeField] private RectTransform content;       // parent with VerticalLayoutGroup
    [SerializeField] private QuestLogRow rowPrefab;       // prefab with 3 TMP_Texts

    void OnEnable()
    {
        // Soft-guard: if manager isn't alive yet, just skip silently
        if (QuestManager.I) QuestManager.I.OnChanged += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        if (QuestManager.I) QuestManager.I.OnChanged -= Refresh;
    }

    public void Refresh()
    {
        if (!content)
        {
            Debug.LogWarning("[QuestLogUI] 'content' is not assigned. Assign a RectTransform that has a VerticalLayoutGroup.");
            return;
        }
        if (!rowPrefab)
        {
            Debug.LogWarning("[QuestLogUI] 'rowPrefab' is not assigned. Create a QuestLogRow prefab and drag it here.");
            return;
        }
        if (!QuestManager.I)
        {
            // No manager yet; clear UI and return
            foreach (Transform c in content) Destroy(c.gameObject);
            return;
        }

        foreach (Transform c in content) Destroy(c.gameObject);

        // Populate rows
        foreach (var qi in QuestManager.I.active)
        {
            var row = Instantiate(rowPrefab, content);
            row.Bind(qi);
        }
    }

    // Optional toggle method you can wire to a button
    public void Toggle()
    {
        if (!panel) { Debug.LogWarning("[QuestLogUI] No 'panel' assigned to toggle."); return; }
        panel.SetActive(!panel.activeSelf);
        if (panel.activeSelf) Refresh();
    }
}
