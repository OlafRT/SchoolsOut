using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.EventSystems; // add

public class BreathDeathUI : MonoBehaviour
{
    [Header("Setup")]
    [SerializeField] CanvasGroup group;
    [SerializeField] Button restartButton;
    [SerializeField] float fadeSeconds = 1.0f;

    bool shown;

    // remember prior cursor state so we can restore on restart if you want
    CursorLockMode prevLock;
    bool prevVisible;

    void Awake()
    {
        if (!group) group = GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        transform.SetAsLastSibling();

        if (restartButton) restartButton.onClick.AddListener(RestartScene);
    }

    public void Show()
    {
        if (shown) return;
        shown = true;

        // show & unlock cursor so we can click the button
        prevLock = Cursor.lockState;
        prevVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        transform.SetAsLastSibling();
        StartCoroutine(FadeIn());
    }

    IEnumerator FadeIn()
    {
        float t = 0f;
        group.blocksRaycasts = true;
        group.interactable = true;
        while (t < fadeSeconds)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Clamp01(t / fadeSeconds);
            yield return null;
        }
        group.alpha = 1f;

        // focus the restart button for keyboard/joypad users
        if (restartButton) EventSystem.current?.SetSelectedGameObject(restartButton.gameObject);
    }

    public void RestartScene()
    {
        // optional: restore previous cursor state
        Cursor.lockState = prevLock;
        Cursor.visible = prevVisible;

        Time.timeScale = 1f;
        var scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.buildIndex);
    }
}
