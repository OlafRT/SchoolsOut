using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(FlowBoard))]
public class FlowCableRenderer : MonoBehaviour
{
    public enum SpaceMode { World, Local }

    [Header("Space")]
    [SerializeField] private SpaceMode space = SpaceMode.World; // Start in World for reliability

    [Header("Visuals")]
    [SerializeField] private float cableWidth = 0.18f;
    [SerializeField] private int cornerVerts = 6;
    [SerializeField] private int capVerts = 6;

    [Header("Material")]
    [SerializeField] private bool useCustomMaterial = false;
    [SerializeField] private Material customMaterial; // leave empty to auto-make Sprites/Default

    [Header("Sorting")]
    [SerializeField] private string sortingLayerName = "UI";
    [SerializeField] private int sortingOrder = 100;     // > your canvas Order (e.g. canvas=5)
    [SerializeField] private float zOffsetWorld = 0.015f; // push toward camera in world units
    [SerializeField] private float zOffsetLocal = -0.0015f;

    [Header("Error Flash")]
    [SerializeField] private Color errorColor = new Color(1f, .25f, .25f);
    [SerializeField] private float flashSeconds = 0.15f;
    [SerializeField] private float flashWidthMultiplier = 1.15f;

    [Header("Solved/Timeout")]
    [SerializeField] private bool hideLinesOnSolved = true;
    [SerializeField] private bool hideLinesOnTimeout = true;

    private FlowBoard board;
    private Transform linesRoot;
    private LineRenderer[] lines;
    private Color[] normalColors;
    private float[] normalWidths;
    private Material workingMat;
    private Canvas rootCanvas;

    void Awake()
    {
        board = GetComponent<FlowBoard>();
        board.RenderCellArms = false; // use cables, not per-cell arms

        rootCanvas = GetComponentInParent<Canvas>();
        EnsureMaterial();
        EnsureLinesRoot();
        CreateOrSyncLines();
    }

    void OnEnable()
    {
        board.PathChanged += OnPathChanged;
        board.ErrorFeedback += OnErrorFlash;
        board.onSolved.AddListener(OnSolved);
        board.onTimeExpired.AddListener(OnTimeout);

        EnsureMaterial();
        EnsureLinesRoot();
        CreateOrSyncLines();
        ShowLines();
        ClearAllLines();
        ApplySpaceModeToAll();
    }

    void OnDisable()
    {
        board.PathChanged -= OnPathChanged;
        board.ErrorFeedback -= OnErrorFlash;
        board.onSolved.RemoveListener(OnSolved);
        board.onTimeExpired.RemoveListener(OnTimeout);
    }

    // ---------- Build / Sync ----------
    void EnsureMaterial()
    {
        if (useCustomMaterial && customMaterial) { workingMat = customMaterial; return; }
        var sh = Shader.Find("Sprites/Default"); // tintable in URP/Built-in
        workingMat = new Material(sh) { name = "CableMat (Sprites/Default)" };
        workingMat.color = Color.white;
    }

    void EnsureLinesRoot()
    {
        if (!linesRoot) // Unity "destroyed" objects compare == null
        {
            var t = transform.Find("CableLines");
            if (t) linesRoot = t;
            else
            {
                var go = new GameObject("CableLines");
                go.transform.SetParent(transform, false);
                linesRoot = go.transform;
            }
        }
    }

    void CreateOrSyncLines()
    {
        if (board.Level == null) return;

        var pairs = board.Level.pairs;
        if (lines != null && lines.Length == pairs.Length)
        {
            for (int i = 0; i < lines.Length; i++)
                if (!lines[i]) RecreateLine(i);
            return;
        }

        EnsureLinesRoot();
        for (int i = linesRoot.childCount - 1; i >= 0; i--)
            Destroy(linesRoot.GetChild(i).gameObject);

        lines = new LineRenderer[pairs.Length];
        normalColors = new Color[pairs.Length];
        normalWidths = new float[pairs.Length];

        for (int i = 0; i < pairs.Length; i++) RecreateLine(i);
    }

    void RecreateLine(int i)
    {
        EnsureLinesRoot();

        var pairs = board.Level.pairs;
        var go = new GameObject($"Line_{i}_{pairs[i].name}");
        go.transform.SetParent(linesRoot, false);

        var lr = go.AddComponent<LineRenderer>();
        lr.material = workingMat;
        lr.textureMode = LineTextureMode.Stretch;
        lr.alignment = LineAlignment.View;
        lr.numCornerVertices = cornerVerts;
        lr.numCapVertices = capVerts;
        lr.positionCount = 0;

        lr.startWidth = lr.endWidth = cableWidth;
        lr.startColor = lr.endColor = pairs[i].color;

        // Sorting stays ON in both modes so we can be in front of the canvas
        lr.sortingLayerName = sortingLayerName;
        lr.sortingOrder = sortingOrder;

        lines[i] = lr;
        normalColors[i] = pairs[i].color;
        normalWidths[i] = cableWidth;
    }

    void ApplySpaceModeToAll()
    {
        if (lines == null) return;
        bool world = (space == SpaceMode.World);
        for (int i = 0; i < lines.Length; i++)
        {
            var lr = lines[i];
            if (!lr) continue;
            lr.useWorldSpace = world;

            // keep sorting even in Local mode
            lr.sortingLayerName = sortingLayerName;
            lr.sortingOrder     = sortingOrder;
        }
    }

    void ClearAllLines()
    {
        if (lines == null) return;
        for (int i = 0; i < lines.Length; i++)
            if (lines[i]) lines[i].positionCount = 0;
    }

    void ShowLines()
    {
        EnsureLinesRoot();
        if (!linesRoot.gameObject.activeSelf)
            linesRoot.gameObject.SetActive(true);
    }

    void HideLines()
    {
        if (linesRoot && linesRoot.gameObject.activeSelf)
            linesRoot.gameObject.SetActive(false);
    }

    // ---------- Events ----------
    void OnPathChanged(int colorIndex, IReadOnlyList<Vector2Int> path)
    {
        if (board.Level == null) return;
        if (lines == null || colorIndex < 0 || colorIndex >= lines.Length) return;

        var lr = lines[colorIndex];
        if (!lr) { RecreateLine(colorIndex); lr = lines[colorIndex]; }
        if (path == null || path.Count == 0) { lr.positionCount = 0; return; }

        // ensure visible even with a single point (duplicate cap)
        int count = Mathf.Max(2, path.Count);
        lr.positionCount = count;

        var cam = rootCanvas && rootCanvas.worldCamera ? rootCanvas.worldCamera : Camera.main;
        Vector3 towardCam = cam ? (-cam.transform.forward) : Vector3.forward;

        if (space == SpaceMode.World)
        {
            for (int i = 0; i < count; i++)
            {
                int idx = Mathf.Min(i, path.Count - 1);
                var w = board.GetCellWorldCenter(path[idx]);
                w += towardCam * zOffsetWorld; // in front of UI
                lr.SetPosition(i, w);
            }
        }
        else // Local
        {
            EnsureLinesRoot();
            for (int i = 0; i < count; i++)
            {
                int idx = Mathf.Min(i, path.Count - 1);
                var w = board.GetCellWorldCenter(path[idx]);
                var l = linesRoot.InverseTransformPoint(w);
                l.z += zOffsetLocal;
                lr.SetPosition(i, l);
            }
        }
    }

    void OnErrorFlash(int colorIndex)
    {
        if (lines == null || colorIndex < 0 || colorIndex >= lines.Length) return;
        var lr = lines[colorIndex];
        if (!lr) { RecreateLine(colorIndex); lr = lines[colorIndex]; }
        StartCoroutine(FlashRoutine(lr, normalColors[colorIndex], normalWidths[colorIndex]));
    }

    void OnSolved()
    {
        if (!hideLinesOnSolved) return;
        ClearAllLines();
        HideLines();
    }

    void OnTimeout()
    {
        if (!hideLinesOnTimeout) return;
        ClearAllLines();
        HideLines();
    }

    IEnumerator FlashRoutine(LineRenderer lr, Color baseColor, float baseWidth)
    {
        if (!lr) yield break;

        int original = lr.positionCount;
        if (original < 2)
        {
            if (original == 0) yield break;
            var p = lr.GetPosition(0);
            lr.positionCount = 2;
            lr.SetPosition(0, p);
            lr.SetPosition(1, p);
        }

        lr.startColor = lr.endColor = errorColor;
        lr.startWidth = lr.endWidth = baseWidth * flashWidthMultiplier;
        yield return new WaitForSeconds(flashSeconds);

        if (!lr) yield break;
        lr.startColor = lr.endColor = baseColor;
        lr.startWidth = lr.endWidth = baseWidth;
        if (lr.positionCount != original && original >= 1)
            lr.positionCount = original;
    }
}
