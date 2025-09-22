using UnityEngine;
using System.Collections;

public class ProjectorScreenController : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private GameObject smallScreenMesh;
    [SerializeField] private GameObject fullScreenMesh;
    [SerializeField] private GameObject projectorFXParent;
    [SerializeField] private Light projectorSpot;

    [Header("UI (under ProjectorFX/Canvas)")]
    [SerializeField] private CanvasGroup noSignalGroup; // fades
    [SerializeField] private GameObject optionsPanel;   // root object
    [SerializeField] private GameObject loadPanel;      // root object

    [Header("Movement")]
    [SerializeField] private bool useLocalPosition = true;
    [SerializeField] private float closedY = 0.03149102f;
    [SerializeField] private float openY   = 0.00448f;
    [SerializeField] private float moveSeconds = 1.25f;
    [SerializeField] private AnimationCurve ease = AnimationCurve.EaseInOut(0,0,1,1);

    [Header("Flicker + Timing")]
    [SerializeField] private float flickerSeconds = 0.75f;
    [SerializeField] private float noSignalSeconds = 1.25f;
    [SerializeField, Range(0f,3f)] private float lightJitter = 0.45f;

    [Header("Look / Light")]
    [SerializeField, Range(0f,1f)] private float maxCanvasAlpha = 0.8f; // cap so it feels like light
    [SerializeField, Range(0f,1f)] private float interactableAlphaThreshold = 0.5f;
    [SerializeField] private Color noSignalLightColor = new Color32(0x1D,0x27,0xFB,255); // #1D27FB
    [SerializeField] private Color contentLightColor  = new Color(0.93f, 0.93f, 0.93f);
    [SerializeField] private float colorSwapSeconds   = 0.25f;

    [Header("Audio")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource humSource;   // loop=true
    [SerializeField] private AudioClip rollDownClip, rollUpClip, powerOnClip, powerOffClip;
    [SerializeField] private float humFadeSeconds = 0.5f;

    private bool isAnimating;
    private CanvasGroup optionsGroup;
    private CanvasGroup loadGroup;
    private CanvasGroup targetGroup; // which one to fade in this time

    void Awake()
    {
        // Ensure panels have CanvasGroups so we can fade them cleanly
        optionsGroup = GetOrAddGroup(optionsPanel);
        loadGroup    = GetOrAddGroup(loadPanel);

        // Start hidden/disabled
        SetAlpha(noSignalGroup, 0f); SafeSetActive(noSignalGroup?.gameObject, false);
        SetAlpha(optionsGroup, 0f);  SafeSetActive(optionsPanel, false);
        SetAlpha(loadGroup, 0f);     SafeSetActive(loadPanel, false);

        if (smallScreenMesh) smallScreenMesh.SetActive(true);
        if (fullScreenMesh)  fullScreenMesh.SetActive(false);
        if (projectorFXParent) projectorFXParent.SetActive(false);
        if (projectorSpot) projectorSpot.enabled = false;

        var p = useLocalPosition ? transform.localPosition : transform.position;
        p.y = closedY;
        if (useLocalPosition) transform.localPosition = p; else transform.position = p;

        if (humSource) { humSource.volume = 0f; humSource.loop = true; }
    }

    // -------- UI hooks --------
    public void OnOpenOptions() { targetGroup = optionsGroup; if (!isAnimating) StartCoroutine(OpenRoutine()); }
    public void OnOpenLoad()    { targetGroup = loadGroup;    if (!isAnimating) StartCoroutine(OpenRoutine()); }
    public void OnCloseButton() { if (!isAnimating) StartCoroutine(CloseRoutine()); }

    // -------- Sequences --------
    private IEnumerator OpenRoutine()
    {
        if (targetGroup == null) yield break;
        isAnimating = true;

        // make sure both panels are off before we start
        SafeSetActive(optionsPanel, false);
        SafeSetActive(loadPanel, false);
        SetAlpha(optionsGroup, 0f);
        SetAlpha(loadGroup, 0f);

        // swap meshes + start roll and FX in parallel
        if (smallScreenMesh) smallScreenMesh.SetActive(false);
        if (fullScreenMesh)  fullScreenMesh.SetActive(true);
        PlayOneShot(rollDownClip);

        bool moveDone = false, fxDone = false;
        StartCoroutine(RunAndFlag(MoveY(closedY, openY), () => moveDone = true));
        StartCoroutine(RunAndFlag(OpenFXFlow(),          () => fxDone  = true));

        while (!(moveDone && fxDone)) yield return null;
        isAnimating = false;
    }

    private IEnumerator CloseRoutine()
    {
        isAnimating = true;

        // hide everything UI-wise right away
        SetAlpha(noSignalGroup, 0f); SafeSetActive(noSignalGroup?.gameObject, false);
        SetAlpha(optionsGroup, 0f);  SafeSetActive(optionsPanel, false);
        SetAlpha(loadGroup, 0f);     SafeSetActive(loadPanel, false);

        // power down FX
        PlayOneShot(powerOffClip);
        yield return FadeHum(humSource ? humSource.volume : 0f, 0f, humFadeSeconds);
        if (projectorSpot) projectorSpot.enabled = false;
        if (projectorFXParent) projectorFXParent.SetActive(false);

        // roll up
        PlayOneShot(rollUpClip);
        yield return MoveY(openY, closedY);

        // mesh swap back
        if (smallScreenMesh) smallScreenMesh.SetActive(true);
        if (fullScreenMesh)  fullScreenMesh.SetActive(false);

        isAnimating = false;
    }

    // ----- FX flow (NO SIGNAL shows immediately, then chosen panel fades in) -----
    private IEnumerator OpenFXFlow()
    {
        if (projectorFXParent) projectorFXParent.SetActive(true);
        PlayOneShot(powerOnClip);

        if (projectorSpot)
        {
            projectorSpot.enabled = true;
            projectorSpot.color   = noSignalLightColor; // blue immediately
        }

        // NO SIGNAL on instantly while we ramp hum and flicker
        if (noSignalGroup)
        {
            noSignalGroup.gameObject.SetActive(true);
            noSignalGroup.blocksRaycasts = false;
            noSignalGroup.interactable   = false;
            noSignalGroup.alpha = Mathf.Min(maxCanvasAlpha, 1f);
        }

        // Run hum fade + flicker simultaneously
        bool a=false,b=false;
        StartCoroutine(RunAndFlag(FadeHum(0f, 1f, humFadeSeconds), () => a = true));
        StartCoroutine(RunAndFlag(FlickerOnly(),                    () => b = true));
        while (!(a && b)) yield return null;

        // Hold blue "no signal" briefly
        yield return new WaitForSeconds(noSignalSeconds);

        // Activate only the desired panel, fade it in while NO SIGNAL fades out, and tint light
        var targetGO = targetGroup ? targetGroup.gameObject : null;
        SafeSetActive(optionsPanel, targetGO == optionsPanel);
        SafeSetActive(loadPanel,    targetGO == loadPanel);

        if (targetGroup)
        {
            // start from 0 alpha
            targetGroup.alpha = 0f;
            targetGroup.blocksRaycasts = false;
            targetGroup.interactable   = false;
        }

        bool c=false,d=false,e=false;
        StartCoroutine(RunAndFlag(FadeCanvas(noSignalGroup, noSignalGroup ? noSignalGroup.alpha : 0f, 0f, 0.15f), () => c = true));
        if (targetGroup)
            StartCoroutine(RunAndFlag(FadeCanvas(targetGroup, 0f, maxCanvasAlpha, 0.25f), () => d = true));
        else d = true;
        StartCoroutine(RunAndFlag(FadeLightColor(noSignalLightColor, contentLightColor, colorSwapSeconds),        () => e = true));

        while (!(c && d && e)) yield return null;
    }

    // ----- Helpers -----
    private CanvasGroup GetOrAddGroup(GameObject root)
    {
        if (!root) return null;
        var cg = root.GetComponent<CanvasGroup>();
        if (!cg) cg = root.AddComponent<CanvasGroup>();
        cg.alpha = 0f; cg.blocksRaycasts = false; cg.interactable = false;
        return cg;
    }

    private static void SafeSetActive(GameObject go, bool state)
    {
        if (!go) return;
        if (go.activeSelf != state) go.SetActive(state);
    }

    private IEnumerator RunAndFlag(IEnumerator r, System.Action done) { yield return r; done?.Invoke(); }

    private IEnumerator FlickerOnly()
    {
        float baseInt = projectorSpot ? projectorSpot.intensity : 0f;
        float t = 0f;
        while (t < flickerSeconds)
        {
            t += Time.deltaTime;
            if (projectorSpot)
                projectorSpot.intensity = baseInt + Random.Range(-lightJitter, lightJitter);
            yield return null;
        }
        if (projectorSpot) projectorSpot.intensity = baseInt;
    }

    private IEnumerator FadeLightColor(Color from, Color to, float secs)
    {
        if (!projectorSpot) yield break;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.0001f, secs);
            projectorSpot.color = Color.LerpUnclamped(from, to, t);
            yield return null;
        }
        projectorSpot.color = to;
    }

    private IEnumerator MoveY(float fromY, float toY)
    {
        Vector3 start = useLocalPosition ? transform.localPosition : transform.position;
        start.y = fromY;
        Vector3 end = start; end.y = toY;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.0001f, moveSeconds);
            float k = ease.Evaluate(Mathf.Clamp01(t));
            var pos = Vector3.LerpUnclamped(start, end, k);
            if (useLocalPosition) transform.localPosition = pos; else transform.position = pos;
            yield return null;
        }
        if (useLocalPosition) { var lp = transform.localPosition; lp.y = toY; transform.localPosition = lp; }
        else { var p = transform.position; p.y = toY; transform.position = p; }
    }

    private IEnumerator FadeCanvas(CanvasGroup g, float a, float b, float secs)
    {
        if (!g) yield break;
        a = Mathf.Min(Mathf.Clamp01(a), maxCanvasAlpha);
        b = Mathf.Min(Mathf.Clamp01(b), maxCanvasAlpha);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.0001f, secs);
            float cur = Mathf.Lerp(a, b, t);
            g.alpha = cur;
            bool interact = cur >= interactableAlphaThreshold;
            g.blocksRaycasts = interact;
            g.interactable   = interact;
            yield return null;
        }
        g.alpha = b;
        bool interactFinal = b >= interactableAlphaThreshold;
        g.blocksRaycasts = interactFinal;
        g.interactable   = interactFinal;
    }

    private IEnumerator FadeHum(float from, float to, float secs)
    {
        if (!humSource) yield break;
        if (!humSource.isPlaying) humSource.Play();
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.0001f, secs);
            humSource.volume = Mathf.Lerp(from, to, t);
            yield return null;
        }
        humSource.volume = to;
        if (Mathf.Approximately(to, 0f)) humSource.Stop();
    }

    private void PlayOneShot(AudioClip clip)
    { if (clip && sfxSource) sfxSource.PlayOneShot(clip); }

    private void SetAlpha(CanvasGroup g, float a)
    {
        if (!g) return;
        a = Mathf.Min(Mathf.Clamp01(a), maxCanvasAlpha);
        g.alpha = a;
        bool interact = a >= interactableAlphaThreshold;
        g.blocksRaycasts = interact;
        g.interactable   = interact;
    }
}
