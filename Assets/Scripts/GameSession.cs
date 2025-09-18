using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameSession : MonoBehaviour
{
    public enum ClassType { Nerd, Jock }

    public static GameSession Instance { get; private set; }

    [Header("Scene")]
    [SerializeField] private string gameSceneName = "Game";

    [Header("Loading UI (optional)")]
    [SerializeField] private GameObject loadingScreenRoot;
    [SerializeField] private Slider progressBar;                 // optional
    [SerializeField] private TMPro.TMP_Text progressLabel;       // optional

    [Header("Lighting Refresh")]
    [Tooltip("Call DynamicGI.UpdateEnvironment & refresh reflection probes after a scene loads.")]
    [SerializeField] private bool refreshLightingOnLoad = true;
    [Tooltip("Extra frames to wait after load before refreshing (0–2 is usually enough).")]
    [SerializeField][Range(0,5)] private int extraFramesToWait = 1;

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

    // You can still call this directly if you prefer:
    public void BeginNewGame(ClassType pick)
    {
        SelectedClass = pick;
        StartCoroutine(LoadGameCo());
    }

    IEnumerator LoadGameCo()
    {
        if (loadingScreenRoot) loadingScreenRoot.SetActive(true);

        var op = SceneManager.LoadSceneAsync(gameSceneName);
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

    // ---------- Lighting refresh after a scene loads ----------
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!refreshLightingOnLoad) return;
        StartCoroutine(RefreshLightingNextFrame());
    }

    IEnumerator RefreshLightingNextFrame()
    {
        // wait a couple of frames so everything is enabled
        yield return null;
        for (int i = 0; i < extraFramesToWait; i++) yield return null;

        // Rebuild ambient/default reflection from the skybox
        DynamicGI.UpdateEnvironment();

        // Refresh realtime reflection probes in the scene (works across Unity versions)
        RefreshRealtimeReflectionProbes();
    }

    static void RefreshRealtimeReflectionProbes()
    {
        var probes = Object.FindObjectsOfType<ReflectionProbe>();
        for (int i = 0; i < probes.Length; i++)
        {
            var p = probes[i];
            if (!p || !p.isActiveAndEnabled) continue;

            // Only re-render realtime probes; baked ones use their baked data
            if (p.mode == UnityEngine.Rendering.ReflectionProbeMode.Realtime)
            {
                p.RenderProbe();
            }
        }
    }
}
