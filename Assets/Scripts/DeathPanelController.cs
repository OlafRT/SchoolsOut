using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class DeathPanelController : MonoBehaviour
{
    [Header("UI")]
    public Button restartButton;              // drag your button here (or auto-found)
    public bool enableKeyboardR = true;       // press R to restart
    public float optionalFadeOut = 0.0f;      // seconds; 0 = instant reload

    CanvasGroup cg;

    void Awake()
    {
        // Ensure EventSystem exists (for button clicks)
        if (!FindObjectOfType<EventSystem>())
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        cg = GetComponent<CanvasGroup>();
        if (!cg) cg = gameObject.AddComponent<CanvasGroup>();

        if (!restartButton)
            restartButton = GetComponentInChildren<Button>(true);

        if (restartButton)
        {
            restartButton.onClick.RemoveAllListeners();
            restartButton.onClick.AddListener(RestartNow);
        }
    }

    void OnEnable()
    {
        // Focus the button so keyboard/joypad can activate it immediately
        if (restartButton && restartButton.gameObject.activeInHierarchy)
            EventSystem.current.SetSelectedGameObject(restartButton.gameObject);
    }

    void Update()
    {
        if (enableKeyboardR && Input.GetKeyDown(KeyCode.R))
            RestartNow();
    }

    public void RestartNow()
    {
        // Always restore timescale — covers both full pause and slow-mo (e.g. 0.15f on death)
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        if (optionalFadeOut > 0f && cg)
        {
            // small fade to black using the same panel
            restartButton.interactable = false;
            StartCoroutine(FadeThenReload());
        }
        else
        {
            ReloadViaLoadSystem();
        }
    }

    System.Collections.IEnumerator FadeThenReload()
    {
        float t = 0f, d = Mathf.Max(0.0001f, optionalFadeOut);
        float a0 = cg.alpha;
        while (t < d)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(a0, 1f, t / d);
            yield return null;
        }
        cg.alpha = 1f;
        ReloadViaLoadSystem();
    }

    void ReloadViaLoadSystem()
    {
        var save = GameSaveManager.I;
        if (save != null)
            save.RespawnReloadScene();
        else
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}