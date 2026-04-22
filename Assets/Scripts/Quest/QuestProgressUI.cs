using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;

public class QuestProgressUI : MonoBehaviour
{
    [Tooltip("Name of the TMP_Text GameObject to find in each scene. " +
             "Leave blank to disable auto-find (assign text manually instead).")]
    public string textObjectName = "QuestProgressToast";

    public TMP_Text text;
    public float showSeconds = 1.75f;

    Coroutine showCo;
    Coroutine waitCo;
    bool subscribed;

    void Awake()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        TrySubscribe();
        if (text) text.gameObject.SetActive(false);
    }

    void OnEnable()  { TrySubscribe(); }

    void OnDisable() { Unsubscribe(); }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (waitCo != null) { StopCoroutine(waitCo); waitCo = null; }
        if (showCo != null) { StopCoroutine(showCo); showCo = null; }
        Unsubscribe();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // The old QuestProgressToast was in the previous scene and is now destroyed.
        // Find the replacement in the newly loaded scene.
        // NOTE: GameObject.Find only searches active objects, so we search by type
        // with FindObjectsInactive.Include to find it even while it's hidden.
        if (string.IsNullOrEmpty(textObjectName)) return;

        text = null;
        foreach (var t in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.gameObject.name == textObjectName)
            {
                text = t;
                text.gameObject.SetActive(false);
                break;
            }
        }
        // If not found, text stays null — HandleProgress will silently no-op.
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

        if (this == null || !this || text == null) yield break;

        text.gameObject.SetActive(false);
        showCo = null;
    }
}
