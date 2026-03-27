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
        TrySubscribe();
        if (text) text.gameObject.SetActive(false);
    }

    void OnEnable()  { TrySubscribe(); }

    void OnDisable() { Unsubscribe(); }

    // OnDestroy is the reliable unsubscribe point during scene unload.
    // OnDisable is not always called before OnDestroy when a scene is unloaded,
    // so we unsubscribe in both places to be safe.
    void OnDestroy()
    {
        if (waitCo != null) { StopCoroutine(waitCo); waitCo = null; }
        if (showCo != null) { StopCoroutine(showCo); showCo = null; }
        Unsubscribe();
    }

    void TrySubscribe()
    {
        if (subscribed) return;
        if (!text) { Debug.LogWarning("[QuestProgressUI] Assign a TMP_Text."); return; }

        if (QuestManager.I)
        {
            QuestManager.I.OnProgress += HandleProgress;
            subscribed = true;
        }
        else
        {
            if (waitCo == null) waitCo = StartCoroutine(WaitForManagerThenSubscribe());
        }
    }

    IEnumerator WaitForManagerThenSubscribe()
    {
        while (QuestManager.I == null) yield return null;

        // Guard: object may have been destroyed while waiting
        if (this == null || !this) yield break;

        if (!subscribed)
        {
            QuestManager.I.OnProgress += HandleProgress;
            subscribed = true;
        }
        waitCo = null;
    }

    void Unsubscribe()
    {
        if (subscribed && QuestManager.I != null)
            QuestManager.I.OnProgress -= HandleProgress;
        subscribed = false;
    }

    void HandleProgress(string questId, string line)
    {
        // Guard against being called after destruction (stale delegate)
        if (this == null || !this || text == null) return;
        if (string.IsNullOrEmpty(line)) return;

        if (showCo != null) StopCoroutine(showCo);
        showCo = StartCoroutine(Show(line));
    }

    IEnumerator Show(string line)
    {
        if (text == null) yield break;

        text.text = line;
        text.gameObject.SetActive(true);
        Canvas.ForceUpdateCanvases();
        yield return new WaitForSeconds(showSeconds);

        // Guard: might have been destroyed during the wait
        if (this == null || !this || text == null) yield break;

        text.gameObject.SetActive(false);
        showCo = null;
    }
}