using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class HoverGlowButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Targets")]
    [SerializeField] private Renderer[] targetRenderers;

    [Header("Glow Settings (URP/Lit default)")]
    [ColorUsage(showAlpha:false, hdr:true)]
    [SerializeField] private Color glowColor = Color.white;
    [SerializeField, Range(0f, 10f)] private float glowIntensity = 2.5f;
    [SerializeField] private bool hdrpMode = false;

    [Header("Camera Trigger on Click")]
    [SerializeField] private Animator cameraAnimator;
    [SerializeField] private string cameraTriggerName = "nerd"; // set per button

    [Header("UI Hide on Click")]
    [SerializeField] private GameObject classSelectRoot;
    [SerializeField] private bool hideRootOnClick = true;

    // NEW: lock hover after a class is chosen
    private static bool s_ClassChosen = false;

    private readonly List<Material> _instancedMats = new List<Material>();
    private readonly List<Color> _origEmission = new List<Color>();
    private bool _prepared;

    void Awake() => Prepare();

    void Prepare()
    {
        if (_prepared || targetRenderers == null) return;
        foreach (var r in targetRenderers)
        {
            if (!r) continue;
            var mats = r.materials; // instanced
            foreach (var m in mats)
            {
                if (!m) continue;
                m.EnableKeyword("_EMISSION");

                Color orig;
                if (m.HasProperty("_EmissionColor")) orig = m.GetColor("_EmissionColor");
                else if (m.HasProperty("_EmissiveColor")) orig = m.GetColor("_EmissiveColor");
                else orig = Color.black;

                _instancedMats.Add(m);
                _origEmission.Add(orig);
            }
            r.materials = mats;
        }
        _prepared = true;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (s_ClassChosen) return;     // don't re-glow after a pick
        SetGlow(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SetGlow(false);
    }

    public void OnClick()
    {
        if (s_ClassChosen) return;
        s_ClassChosen = true;

        // Ensure glow is OFF on the picked model
        SetGlow(false);

        // Fire camera trigger
        if (cameraAnimator && !string.IsNullOrEmpty(cameraTriggerName))
            cameraAnimator.SetTrigger(cameraTriggerName);

        // Hide class select UI if requested
        if (hideRootOnClick && classSelectRoot)
            classSelectRoot.SetActive(false);
    }

    // IMPORTANT: when the panel or button gets disabled, clear glow
    void OnDisable()
    {
        SetGlow(false);
    }

    void SetGlow(bool on)
    {
        if (!_prepared) Prepare();
        for (int i = 0; i < _instancedMats.Count; i++)
        {
            var m = _instancedMats[i];
            if (!m) continue;

            if (on)
            {
                var c = glowColor * Mathf.LinearToGammaSpace(glowIntensity);
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c);
                if (hdrpMode)
                {
                    if (m.HasProperty("_EmissiveColor")) m.SetColor("_EmissiveColor", c);
                    if (m.HasProperty("_EmissiveIntensity")) m.SetFloat("_EmissiveIntensity", glowIntensity);
                }
            }
            else
            {
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", _origEmission[i]);
                if (hdrpMode)
                {
                    if (m.HasProperty("_EmissiveColor")) m.SetColor("_EmissiveColor", _origEmission[i]);
                    if (m.HasProperty("_EmissiveIntensity")) m.SetFloat("_EmissiveIntensity", 0f);
                }
            }
        }
    }

    public static void ResetChoiceLock() { s_ClassChosen = false; }

#if UNITY_EDITOR
    [ContextMenu("Collect child renderers from first Transform in list")]
    void CollectChildRenderers()
    {
        if (targetRenderers == null || targetRenderers.Length == 0 || targetRenderers[0] == null) return;
        var root = targetRenderers[0].transform;
        targetRenderers = root.GetComponentsInChildren<Renderer>(true);
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
