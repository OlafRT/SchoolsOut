using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Drop this on any GameObject in the scene.
/// It overrides the URP shadow distance when the scene loads and restores
/// the original value automatically when the scene unloads (OnDestroy).
///
/// No other scripts need to know about this — it is fully self-contained.
/// </summary>
public class SceneShadowDistance : MonoBehaviour
{
    [Tooltip("Shadow distance (metres) to use for this scene only.")]
    public float sceneShadowDistance = 90f;

    float _originalDistance;
    UniversalRenderPipelineAsset _urpAsset;

    void Awake()
    {
        _urpAsset = QualitySettings.renderPipeline as UniversalRenderPipelineAsset
                 ?? GraphicsSettings.renderPipelineAsset as UniversalRenderPipelineAsset;

        if (_urpAsset == null)
        {
            Debug.LogWarning("[SceneShadowDistance] No URP asset is active — component disabled.");
            enabled = false;
            return;
        }

        _originalDistance        = _urpAsset.shadowDistance;
        _urpAsset.shadowDistance = sceneShadowDistance;

        Debug.Log($"[SceneShadowDistance] Shadow distance: {_originalDistance}m → {sceneShadowDistance}m");
    }

    void OnDestroy()
    {
        // Fires when the scene unloads (or this GameObject is destroyed).
        // Restores whatever was set before, even if another script changed it in the meantime.
        if (_urpAsset != null)
        {
            _urpAsset.shadowDistance = _originalDistance;
            Debug.Log($"[SceneShadowDistance] Shadow distance restored to {_originalDistance}m");
        }
    }
}