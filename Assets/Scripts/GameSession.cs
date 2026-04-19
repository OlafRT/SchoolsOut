using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameSession : MonoBehaviour
{
    public enum ClassType { Nerd, Jock }

    public static GameSession Instance { get; private set; }

    [Header("Scene")]
    [SerializeField] private string gameSceneName = "Game";      // Fallback / shared scene
    [SerializeField] private string nerdSceneName = "Game_Nerd"; // Nerd-only scene
    [SerializeField] private string jockSceneName = "Game_Jock"; // Jock-only scene

    [Header("Loading UI (optional)")]
    [SerializeField] private GameObject loadingScreenRoot;
    [SerializeField] private Slider progressBar;
    [SerializeField] private TMPro.TMP_Text progressLabel;

    [Header("Loading Tips")]
    [Tooltip("Text element on the loading screen that displays the tip.")]
    [SerializeField] private TMPro.TMP_Text tipText;
    [Tooltip("One tip is chosen at random each time the loading screen appears. " +
             "Leave empty to hide the tip text.")]
    [SerializeField] [TextArea(2, 4)] private string[] loadingTips;

    [Header("Lighting Refresh")]
    [Tooltip("Call DynamicGI.UpdateEnvironment & refresh reflection probes after a scene loads.")]
    [SerializeField] private bool refreshLightingOnLoad = true;
    [Tooltip("Extra frames to wait after load before refreshing (0–5 is usually enough).")]
    [SerializeField][Range(0, 5)] private int extraFramesToWait = 1;

    public ClassType SelectedClass { get; private set; } = ClassType.Nerd;

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        if (Instance == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // --- Button-friendly entry points ---
    public void StartAsNerd() => BeginNewGame(ClassType.Nerd);
    public void StartAsJock() => BeginNewGame(ClassType.Jock);

    public void BeginNewGame(ClassType pick)
    {
        SelectedClass = pick;

        // Tell the save system this is a fresh session so it claims an empty
        // slot and clears any cross-scene stat snapshot from a previous run.
        GameSaveManager.I?.NotifyNewGame();

        string targetScene = GetSceneNameForClass(pick);
        StartCoroutine(LoadSceneCo(targetScene));
    }

    /// <summary>
    /// Load any scene by name with the shared loading screen and random tip.
    /// Called by <see cref="LoadSceneOnEnable"/> (and anywhere else that needs
    /// a loading screen) so the UI logic lives in one place.
    /// </summary>
    public void LoadScene(string sceneName)
    {
        StartCoroutine(LoadSceneCo(sceneName));
    }

    // Picks the scene name based on the selected class
    string GetSceneNameForClass(ClassType pick)
    {
        if (pick == ClassType.Nerd && !string.IsNullOrEmpty(nerdSceneName))
            return nerdSceneName;

        if (pick == ClassType.Jock && !string.IsNullOrEmpty(jockSceneName))
            return jockSceneName;

        // Fallback if specific one is not set
        return gameSceneName;
    }

    IEnumerator LoadSceneCo(string sceneName)
    {
        // Show loading screen and pick a random tip
        if (loadingScreenRoot) loadingScreenRoot.SetActive(true);
        ShowRandomTip();

        var op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false;

        while (op.progress < 0.9f)
        {
            float p = Mathf.InverseLerp(0f, 0.9f, op.progress);
            if (progressBar)   progressBar.value  = p;
            if (progressLabel) progressLabel.text = $"Loading {(int)(p * 100f)}%";
            yield return null;
        }

        if (progressBar)   progressBar.value  = 1f;
        if (progressLabel) progressLabel.text = "Loading 100%";
        yield return null;

        op.allowSceneActivation = true;
    }

    void ShowRandomTip()
    {
        if (!tipText) return;

        if (loadingTips == null || loadingTips.Length == 0)
        {
            tipText.gameObject.SetActive(false);
            return;
        }

        tipText.gameObject.SetActive(true);
        tipText.text = loadingTips[Random.Range(0, loadingTips.Length)];
    }

    // ---------- Lighting refresh after a scene loads ----------
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!refreshLightingOnLoad) return;
        StartCoroutine(RefreshLightingNextFrame());
    }

    IEnumerator RefreshLightingNextFrame()
    {
        yield return null;
        for (int i = 0; i < extraFramesToWait; i++) yield return null;

        DynamicGI.UpdateEnvironment();
        RefreshRealtimeReflectionProbes();
    }

    static void RefreshRealtimeReflectionProbes()
    {
        var probes = Object.FindObjectsOfType<ReflectionProbe>();
        for (int i = 0; i < probes.Length; i++)
        {
            var p = probes[i];
            if (!p || !p.isActiveAndEnabled) continue;

            if (p.mode == UnityEngine.Rendering.ReflectionProbeMode.Realtime)
            {
                p.RenderProbe();
            }
        }
    }
}
