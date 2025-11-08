using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
#if UNITY_RENDER_PIPELINE_UNIVERSAL
using UnityEngine.Rendering.Universal;
#endif

[DisallowMultipleComponent]
public class SceneLightingRefresher : MonoBehaviour
{
    private static SceneLightingRefresher _instance;

    [Header("When to run")]
    [Tooltip("Leave empty to run on every new scene, or set to only run on a specific scene (e.g., 'Bicycle').")]
    [SerializeField] private string onlyForScene = "";

    [Header("Skybox & GI refresh")]
    [SerializeField] private bool reapplySkybox = true;
    [SerializeField] private bool updateEnvironmentGI = true;
    [SerializeField] private bool rerenderRealtimeReflectionProbes = true;

    [Header("Clean up carry-over (DDOL scene)")]
    [SerializeField] private bool disableDontDestroyLights = true;
    [SerializeField] private bool disableDontDestroyReflectionProbes = true;
    [SerializeField] private bool disableDontDestroyVolumes = true;

#if UNITY_RENDER_PIPELINE_UNIVERSAL
    [Header("URP camera & volumes")]
    [Tooltip("Ensure the active camera renders post-processing.")]
    [SerializeField] private bool enforceURPPostFX = true;

    [Tooltip("Force the camera's Volume Layer Mask (set to the layer that your volumes live on, e.g. PostFX).")]
    [SerializeField] private LayerMask forceVolumeLayerMask = default;

    [Tooltip("If you use multiple URP renderers, set to >= 0 to force the renderer index the scene expects.")]
    [SerializeField] private int desiredRendererIndex = -1;

    [Tooltip("Briefly toggle volumes to rebuild the stack after load.")]
    [SerializeField] private bool rebuildVolumeStack = true;
#endif

    private void Awake()
    {
        if (_instance) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (_instance == this) SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!string.IsNullOrEmpty(onlyForScene) && scene.name != onlyForScene) return;
        StartCoroutine(RefreshLightingNextFrames(scene));
    }

    private System.Collections.IEnumerator RefreshLightingNextFrames(Scene loadedScene)
    {
        // Give Unity a couple frames for objects/volumes/probes to appear.
        yield return null;
        yield return null;

        // 1) Re-apply skybox & GI (original behavior)
        if (reapplySkybox) RenderSettings.skybox = RenderSettings.skybox;
        if (updateEnvironmentGI) DynamicGI.UpdateEnvironment();

        if (rerenderRealtimeReflectionProbes)
        {
            var probes = FindObjectsOfType<ReflectionProbe>(true);
            foreach (var p in probes)
                if (p && p.mode != UnityEngine.Rendering.ReflectionProbeMode.Baked)
                    p.RenderProbe();
        }

        // 2) Neutralize anything leaking from the DontDestroyOnLoad scene
        var ddol = SceneManager.GetSceneByName("DontDestroyOnLoad");
        if (ddol.IsValid())
        {
            foreach (var root in ddol.GetRootGameObjects())
            {
                if (disableDontDestroyLights)
                    foreach (var l in root.GetComponentsInChildren<Light>(true)) l.enabled = false;

                if (disableDontDestroyReflectionProbes)
                    foreach (var rp in root.GetComponentsInChildren<ReflectionProbe>(true)) rp.enabled = false;

                if (disableDontDestroyVolumes)
                    foreach (var v in root.GetComponentsInChildren<Volume>(true)) v.enabled = false;
            }
        }

#if UNITY_RENDER_PIPELINE_UNIVERSAL
        // 3) URP camera + volume sanity (post, volume mask, renderer)
        var cam = Camera.main; // or pick your scene camera explicitly if needed
        var camData = cam ? cam.GetComponent<UniversalAdditionalCameraData>() : null;
        if (camData)
        {
            if (enforceURPPostFX) camData.renderPostProcessing = true;

            if (desiredRendererIndex >= 0 && desiredRendererIndex < camData.scriptableRendererDataList.Count)
                camData.SetRenderer(desiredRendererIndex);

            // Force volume layer mask (and toggle once to rebuild)
            if (forceVolumeLayerMask.value != 0)
            {
                var old = camData.volumeLayerMask;
                camData.volumeLayerMask = 0;  // force a rebuild next frame
                yield return null;
                camData.volumeLayerMask = forceVolumeLayerMask;
            }
        }

        // Optional: briefly toggle all scene volumes to ensure the stack rebuilds
        if (rebuildVolumeStack)
        {
            var vols = FindObjectsOfType<Volume>(true);
            foreach (var v in vols) { var was = v.enabled; v.enabled = false; v.enabled = was; }
        }

        // Warn if URP asset is missing required buffers (can make AO darken incorrectly)
        var urpAsset = GraphicsSettings.renderPipelineAsset as UniversalRenderPipelineAsset;
        if (urpAsset)
        {
            if (!urpAsset.supportsCameraDepthTexture || !urpAsset.supportsCameraOpaqueTexture)
                Debug.LogWarning("[SceneLightingRefresher] URP asset lacks Depth/Opaque textures. AO/tonemapping may look darker after scene load.");
        }
#endif
    }
}

