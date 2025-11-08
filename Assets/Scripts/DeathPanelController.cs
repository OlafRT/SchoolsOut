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
        // if your game sometimes pauses on death, ensure time resumes
        if (Time.timeScale == 0f) Time.timeScale = 1f;

        if (optionalFadeOut > 0f && cg)
        {
            // small fade to black using the same panel
            restartButton.interactable = false;
            StartCoroutine(FadeThenReload());
        }
        else
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
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
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
