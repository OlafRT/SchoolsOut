using UnityEngine;
using TMPro;
using System.Collections;

public class QuestProgressUI : MonoBehaviour
{
    public TMP_Text text;
    public float showSeconds = 1.75f;

    Coroutine showCo;
    Coroutine waitCo;
    bool subscribed;

    void Awake()
    {
        // If the manager isn’t ready yet, we’ll wait below.
        TrySubscribe();
        // Keep the text hidden initially (UI can be active)
        if (text) text.gameObject.SetActive(false);
    }

    void OnEnable()  { TrySubscribe(); }
    void OnDisable() { Unsubscribe(); }

    void TrySubscribe()
    {
        if (subscribed) return;

        if (!text)
        {
            Debug.LogWarning("[QuestProgressUI] Assign a TMP_Text.");
            return;
        }

        if (QuestManager.I)
        {
            QuestManager.I.OnProgress += HandleProgress;
            subscribed = true;
        }
        else
        {
            // Manager not ready yet — wait until it exists, then subscribe once.
            if (waitCo == null) waitCo = StartCoroutine(WaitForManagerThenSubscribe());
        }
    }

    IEnumerator WaitForManagerThenSubscribe()
    {
        // Wait until QuestManager.I is assigned (Awake runs); avoids execution-order races
        while (QuestManager.I == null) yield return null;

        if (!subscribed)
        {
            QuestManager.I.OnProgress += HandleProgress;
            subscribed = true;
        }
        waitCo = null;
    }

    void Unsubscribe()
    {
        if (subscribed && QuestManager.I)
            QuestManager.I.OnProgress -= HandleProgress;
        subscribed = false;
    }

    void HandleProgress(string questId, string line)
    {
        if (string.IsNullOrEmpty(line) || text == null) return;

        if (showCo != null) StopCoroutine(showCo);
        showCo = StartCoroutine(Show(line));
    }

    IEnumerator Show(string line)
    {
        text.text = line;
        text.gameObject.SetActive(true);
        Canvas.ForceUpdateCanvases();  // ensure immediate layout/visibility
        yield return new WaitForSeconds(showSeconds);
        text.gameObject.SetActive(false);
    }
}
