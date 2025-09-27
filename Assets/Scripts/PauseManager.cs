using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class PauseManager : MonoBehaviour
{
    [Header("UI Roots (optional)")]
    [SerializeField] private GameObject mainCanvasRoot;
    [SerializeField] private GameObject pauseCanvasRoot;

    [Header("Projector Screen (unchanged)")]
    [SerializeField] private ProjectorScreenController projector;
    [SerializeField] private bool useProjectorOpenOnPause = true;

    [Header("Projector Durations")]
    [SerializeField] private float openTotalSeconds = 2.4f;
    [SerializeField] private float closeTotalSeconds = 1.9f;

    [Header("Player Freeze Options")]
    [SerializeField] private GameObject[] playerRootsToToggle;
    [SerializeField] private Behaviour[] behavioursToDisable;

    [Header("Cursor (optional)")]
    [SerializeField] private bool showCursorWhilePaused = true;
    [SerializeField] private CursorLockMode pausedCursorLock = CursorLockMode.None;
    [SerializeField] private CursorLockMode gameplayCursorLock = CursorLockMode.Locked;

    [Header("Main Menu")]
    [Tooltip("Name of the main menu scene as added in Build Settings.")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private bool isPaused;
    private bool isTransitioning;

    void Awake()
    {
        if (mainCanvasRoot) mainCanvasRoot.SetActive(true);
        if (pauseCanvasRoot) pauseCanvasRoot.SetActive(false);
        ApplyGameplayCursor();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (!isPaused) StartPauseFlow();
            else ResumeGame();
        }
    }

    // Hook these to buttons
    public void OnResumeButton() => ResumeGame();
    public void OnReturnToMainMenuButton() => ReturnToMainMenu();

    private void StartPauseFlow()
    {
        if (isTransitioning || projector == null) return;
        StartCoroutine(PauseRoutine());
    }

    private IEnumerator PauseRoutine()
    {
        isTransitioning = true;

        // Freeze player immediately
        FreezePlayer(true);

        // Swap canvases
        if (mainCanvasRoot) mainCanvasRoot.SetActive(false);
        if (pauseCanvasRoot) pauseCanvasRoot.SetActive(true);
        ApplyPausedCursor();

        // Roll DOWN projector
        if (useProjectorOpenOnPause) projector.OnOpenOptions();

        // Wait for open to finish
        if (openTotalSeconds > 0f)
            yield return new WaitForSeconds(openTotalSeconds);

        isPaused = true;
        isTransitioning = false;
    }

    private void ResumeGame()
    {
        if (isTransitioning || projector == null) return;
        StartCoroutine(ResumeRoutine());
    }

    private IEnumerator ResumeRoutine()
    {
        isTransitioning = true;

        // Roll UP projector
        projector.OnCloseButton();

        // Wait for close to finish
        if (closeTotalSeconds > 0f)
            yield return new WaitForSeconds(closeTotalSeconds);

        // Swap canvases back
        if (pauseCanvasRoot) pauseCanvasRoot.SetActive(false);
        if (mainCanvasRoot) mainCanvasRoot.SetActive(true);

        // Restore player
        FreezePlayer(false);
        ApplyGameplayCursor();

        isPaused = false;
        isTransitioning = false;
        yield return null;
    }

    private void ReturnToMainMenu()
    {
        if (isTransitioning) return;
        StartCoroutine(ReturnToMainMenuRoutine());
    }

    private IEnumerator ReturnToMainMenuRoutine()
    {
        isTransitioning = true;

        // Ensure gameplay systems won't run during transition
        FreezePlayer(true);

        // Roll UP projector before leaving the scene
        if (projector != null) projector.OnCloseButton();

        if (closeTotalSeconds > 0f)
            yield return new WaitForSeconds(closeTotalSeconds);

        // Optional: make sure cursor is visible for menus
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        // Load main menu (single)
        if (!string.IsNullOrEmpty(mainMenuSceneName))
            SceneManager.LoadScene(mainMenuSceneName, LoadSceneMode.Single);
        else
            Debug.LogError("PauseManager: Main Menu scene name is empty. Set it in the inspector.");

        // No need to unfreeze/restore here; the new scene will set its own state.
        yield return null;
    }

    // ---------------- helpers ----------------

    private void FreezePlayer(bool freeze)
    {
        if (playerRootsToToggle != null)
        {
            foreach (var go in playerRootsToToggle)
            {
                if (!go || go == this.gameObject) continue;
                go.SetActive(!freeze);
            }
        }

        if (behavioursToDisable != null)
        {
            foreach (var b in behavioursToDisable)
            {
                if (!b) continue;
                b.enabled = !freeze;
            }
        }
    }

    private void ApplyPausedCursor()
    {
        if (!showCursorWhilePaused) return;
        Cursor.visible = true;
        Cursor.lockState = pausedCursorLock;
    }

    private void ApplyGameplayCursor()
    {
        Cursor.visible = (gameplayCursorLock == CursorLockMode.None);
        Cursor.lockState = gameplayCursorLock;
    }
}
