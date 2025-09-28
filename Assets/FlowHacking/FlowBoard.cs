using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FlowBoard : MonoBehaviour
{
    [Header("Level + Prefab")]
    [SerializeField] private FlowLevel level;
    [SerializeField] private FlowCell cellPrefab;
    [SerializeField] private RectTransform gridParent;      // the RectTransform with GridLayoutGroup
    private GridLayoutGroup grid;                           // cached

    [Header("Solved / Timeout")]
    public UnityEngine.Events.UnityEvent onSolved;
    public UnityEngine.Events.UnityEvent onTimeExpired;

    [Header("Rendering")]
    [SerializeField] private bool renderCellArms = true;    // turned off by cable renderer
    public bool RenderCellArms { get => renderCellArms; set => renderCellArms = value; }

    [Header("Audio")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip pairConnectClip;
    [SerializeField] private AudioClip errorClip;

    [Header("Timer UI")]
    [SerializeField] private Text timerText;                // swap to TMP if you prefer
    [SerializeField] private Color timerNormal = Color.white;
    [SerializeField] private Color timerHurry  = new Color(1f, .6f, .6f);
    [SerializeField] private float hurryThreshold = 10f;

    public FlowLevel Level => level;

    // Events for renderer/fx
    public event Action<int, IReadOnlyList<Vector2Int>> PathChanged;
    public event Action<int> ErrorFeedback;

    // State
    private FlowCell[,] cells;
    private int[,] colorIndex;      // -1 empty, else pair index
    private bool[,] isEndpoint;

    private bool inputLocked;
    private bool isSolved;

    private int activeColor = -1;
    private List<Vector2Int> path;
    private readonly Dictionary<int, List<Vector2Int>> pathsByColor = new();

    // Drag polling (math hit-test instead of raycast)
    private bool isDragging;
    private Canvas rootCanvas;
    private Camera uiCam;           // may be null in Overlay

    // Grid math cache
    private Vector2 cellSize;
    private Vector2 spacing;
    private RectOffset padding;
    private GridLayoutGroup.Corner startCorner;

    // ---------------- TIMER (patched) ----------------
    private float remainingTime;
    private bool  timerRunning = false;
    private bool  timeExpiredFired = false;
    private Coroutine timerCo;

    void Awake()
    {
        grid = gridParent ? gridParent.GetComponent<GridLayoutGroup>() : null;
        rootCanvas = gridParent ? gridParent.GetComponentInParent<Canvas>() : null;
        uiCam = rootCanvas ? rootCanvas.worldCamera : null;
    }

    void Start()
    {
        BuildBoard();
        StartTimerIfNeeded(); // first-time entry when this object is enabled the first time
    }

    void Update()
    {
        if (inputLocked) return;

        // live drag ends if mouse released anywhere
        if (isDragging && !Input.GetMouseButton(0))
        {
            EndActivePath();
            return;
        }

        if (isDragging && activeColor >= 0 && Input.GetMouseButton(0))
        {
            if (TryGetCellFromMouse(Input.mousePosition, out var cellXY))
                StepTo(cellXY);
        }
    }

    // ----------------- Build -----------------
    public void BuildBoard()
    {
        inputLocked = false;
        isSolved = false;
        isDragging = false;
        lastErrorAt = -999f;
        activeColor = -1;
        path = null;

        if (!gridParent) { Debug.LogError("FlowBoard: gridParent is not assigned."); return; }
        grid = gridParent.GetComponent<GridLayoutGroup>();
        if (!grid) { Debug.LogError("FlowBoard: gridParent needs a GridLayoutGroup."); return; }

        // cache layout numbers
        cellSize = grid.cellSize;
        spacing  = grid.spacing;
        padding  = grid.padding;
        startCorner = grid.startCorner;

        ClearChildren(gridParent);

        int w = level.width, h = level.height;
        cells = new FlowCell[w, h];
        colorIndex = new int[w, h];
        isEndpoint = new bool[w, h];

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            var cell = Instantiate(cellPrefab, gridParent);
            cell.Setup(x, y, this);
            cells[x, y] = cell;
            colorIndex[x, y] = -1;
        }

        // endpoints
        pathsByColor.Clear();
        for (int i = 0; i < level.pairs.Length; i++)
        {
            var p = level.pairs[i];
            MarkEndpoint(p.a, i, p.color);
            MarkEndpoint(p.b, i, p.color);
            pathsByColor[i] = new List<Vector2Int>();
            NotifyPathChanged(i);
        }

        RefreshAllVisuals();
    }

    void ClearChildren(Transform t)
    {
        for (int i = t.childCount - 1; i >= 0; i--) Destroy(t.GetChild(i).gameObject);
    }

    bool InBounds(Vector2Int p)
        => p.x >= 0 && p.x < level.width && p.y >= 0 && p.y < level.height;

    void MarkEndpoint(Vector2Int pos, int colorIdx, Color c)
    {
        if (!InBounds(pos))
        {
            Debug.LogError($"Endpoint '{level.pairs[colorIdx].name}' at {pos} out of bounds for {level.width}x{level.height}");
            return;
        }
        isEndpoint[pos.x, pos.y] = true;
        cells[pos.x, pos.y].SetEndpoint(c, true);
    }

    // ----------------- Input from cells -----------------
    public void CellPointerDown(int x, int y)
    {
        if (inputLocked) return;

        int ci = colorIndex[x, y];
        if (isEndpoint[x, y])
        {
            int pairIdx = FindPairIndexAt(new Vector2Int(x, y));
            BeginPath(pairIdx, new Vector2Int(x, y));
            isDragging = true;
        }
        else if (ci >= 0)
        {
            BeginPath(ci, new Vector2Int(x, y));
            isDragging = true;
        }
    }

    public void CellPointerEnter(int x, int y) { /* not used anymore */ }

    public void CellPointerUp(int x, int y)
    {
        EndActivePath();
    }

    void EndActivePath()
    {
        if (!isDragging) return;
        isDragging = false;

        if (activeColor >= 0)
        {
            pathsByColor[activeColor] = new List<Vector2Int>(path);
            NotifyPathChanged(activeColor);
        }

        activeColor = -1;
        path = null;

        TryWin();
    }

    // ----------------- Path ops -----------------
    void BeginPath(int color, Vector2Int start)
    {
        ClearColorFromGrid(color);

        activeColor = color;
        path = new List<Vector2Int> { start };
        SetCellColor(start, color);
        pathsByColor[color] = new List<Vector2Int>(path);
        RefreshNeighborsAround(start);
        NotifyPathChanged(color);
    }

    void StepTo(Vector2Int next)
    {
        if (inputLocked || activeColor < 0) return;

        Vector2Int cur = path[path.Count - 1];

        // orthogonal neighbor only
        if (Mathf.Abs(next.x - cur.x) + Mathf.Abs(next.y - cur.y) != 1) return;

        int occupant = colorIndex[next.x, next.y];

        // backtrack
        if (path.Count >= 2 && next == path[path.Count - 2])
        {
            PopLastCellFromActivePath();
            RefreshNeighborsAround(next);
            RefreshNeighborsAround(cur);
            NotifyPathChanged(activeColor);
            return;
        }

        // blockers (with feedback)
        if (occupant >= 0 && occupant != activeColor && !isEndpoint[next.x, next.y]) { DeniedStepFeedback(); return; }
        if (isEndpoint[next.x, next.y] && occupant != -1 && occupant != activeColor) { DeniedStepFeedback(); return; }

        if (isEndpoint[next.x, next.y])
        {
            int pairHere = FindPairIndexAt(next);
            if (pairHere != activeColor) { DeniedStepFeedback(); return; }
        }

        if (occupant == activeColor && !IsTail(next)) { DeniedStepFeedback(); return; }

        // claim
        SetCellColor(next, activeColor);
        path.Add(next);
        pathsByColor[activeColor] = new List<Vector2Int>(path); // keep copy in sync during drag
        RefreshNeighborsAround(next);
        RefreshNeighborsAround(cur);
        NotifyPathChanged(activeColor);

        // connected?
        if (isEndpoint[next.x, next.y] && path.Count > 1)
        {
            pathsByColor[activeColor] = new List<Vector2Int>(path);
            if (pairConnectClip && sfxSource) sfxSource.PlayOneShot(pairConnectClip);
            TryWin();
        }
    }

    void ClearColorFromGrid(int color)
    {
        if (!pathsByColor.TryGetValue(color, out var list)) return;

        foreach (var c in list)
        {
            colorIndex[c.x, c.y] = -1;
            cells[c.x, c.y].SetEmptyVisual();
            if (isEndpoint[c.x, c.y])
                cells[c.x, c.y].SetEndpoint(level.pairs[color].color, true);
        }
        list.Clear();
        NotifyPathChanged(color);
    }

    void PopLastCellFromActivePath()
    {
        if (path == null || path.Count <= 1) return;
        Vector2Int last = path[path.Count - 1];
        colorIndex[last.x, last.y] = -1;
        cells[last.x, last.y].SetEmptyVisual();
        if (isEndpoint[last.x, last.y])
            cells[last.x, last.y].SetEndpoint(level.pairs[activeColor].color, true);
        path.RemoveAt(path.Count - 1);
    }

    bool IsTail(Vector2Int p) => path.Count >= 2 && p == path[path.Count - 2];

    void SetCellColor(Vector2Int p, int color)
    {
        colorIndex[p.x, p.y] = color;
        UpdateCellArms(p);
    }

    // ----------------- Visual helpers -----------------
    void RefreshAllVisuals()
    {
        int w = level.width, h = level.height;
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            var pos = new Vector2Int(x, y);
            if (colorIndex[x, y] == -1)
            {
                if (!isEndpoint[x, y]) cells[x, y].SetEmptyVisual();
                else cells[x, y].UpdateArms(Color.clear, false, false, false, false);
            }
            else
            {
                UpdateCellArms(pos);
            }
        }
    }

    void RefreshNeighborsAround(Vector2Int p)
    {
        UpdateCellArms(p);
        foreach (var n in Neighbors(p)) UpdateCellArms(n);
    }

    void UpdateCellArms(Vector2Int p)
    {
        if (!renderCellArms) return;

        int ci = colorIndex[p.x, p.y];
        var c = (ci >= 0) ? level.pairs[ci].color : Color.clear;

        bool up=false, right=false, down=false, left=false;
        if (ci >= 0)
        {
            if (TryGet(p + Vector2Int.up,    out var n1) && colorIndex[n1.x, n1.y] == ci) up    = true;
            if (TryGet(p + Vector2Int.right, out var n2) && colorIndex[n2.x, n2.y] == ci) right = true;
            if (TryGet(p + Vector2Int.down,  out var n3) && colorIndex[n3.x, n3.y] == ci) down  = true;
            if (TryGet(p + Vector2Int.left,  out var n4) && colorIndex[n4.x, n4.y] == ci) left  = true;
        }

        cells[p.x, p.y].UpdateArms(c, up, right, down, left);
    }

    bool TryGet(Vector2Int p, out Vector2Int clamped)
    {
        clamped = p;
        if (p.x < 0 || p.y < 0 || p.x >= level.width || p.y >= level.height) return false;
        return true;
    }

    IEnumerable<Vector2Int> Neighbors(Vector2Int p)
    {
        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
        foreach (var d in dirs)
        {
            var n = p + d;
            if (n.x >= 0 && n.y >= 0 && n.x < level.width && n.y < level.height) yield return n;
        }
    }

    int FindPairIndexAt(Vector2Int pos)
    {
        for (int i = 0; i < level.pairs.Length; i++)
            if (level.pairs[i].a == pos || level.pairs[i].b == pos) return i;
        return -1;
    }

    void NotifyPathChanged(int color)
    {
        IReadOnlyList<Vector2Int> list = null;
        if (color == activeColor && path != null)
        {
            list = path;
        }
        else
        {
            pathsByColor.TryGetValue(color, out var stored);
            list = stored;
        }
        PathChanged?.Invoke(color, list);
    }

    // ----------------- Feedback / Win / Timer -----------------
    void DeniedStepFeedback()
    {
        if (Time.unscaledTime - lastErrorAt < errorCooldown) return;
        lastErrorAt = Time.unscaledTime;

        ErrorFeedback?.Invoke(activeColor);
        if (errorClip && sfxSource) sfxSource.PlayOneShot(errorClip);
    }

    void TryWin()
    {
        // all cells filled?
        for (int y = 0; y < level.height; y++)
        for (int x = 0; x < level.width; x++)
            if (colorIndex[x, y] < 0) return;

        // every pair connected?
        for (int i = 0; i < level.pairs.Length; i++)
        {
            var A = level.pairs[i].a;
            var B = level.pairs[i].b;
            if (colorIndex[A.x, A.y] != i || colorIndex[B.x, B.y] != i) return;
        }

        isSolved = true;
        inputLocked = true;
        onSolved?.Invoke();
    }

    // --- TIMER control ---
    void StartTimerIfNeeded()
    {
        if (level.timeLimitSeconds <= 0f)
        {
            remainingTime = 0f;
            UpdateTimerUI();
            return;
        }

        ResetTimerAndStart();
    }

    public void ResetTimerAndStart()
    {
        // reset from current level limit
        remainingTime = Mathf.Max(0f, level.timeLimitSeconds);
        timeExpiredFired = false;
        timerRunning = true;

        // ensure any previous timer is stopped cleanly
        if (timerCo != null) { StopCoroutine(timerCo); timerCo = null; }

        UpdateTimerUI();
        timerCo = StartCoroutine(TimerRoutine());
    }

    public void StopTimerWithoutExpire()
    {
        timerRunning = false;
        if (timerCo != null)
        {
            StopCoroutine(timerCo);
            timerCo = null;
        }
        // UI stays as-is; caller decides what to show
    }

    System.Collections.IEnumerator TimerRoutine()
    {
        while (timerRunning && !isSolved && remainingTime > 0f)
        {
            remainingTime -= Time.deltaTime;
            if (remainingTime < 0f) remainingTime = 0f;
            UpdateTimerUI();
            yield return null;
        }

        // stop & fire once on timeout (if still running and not solved)
        if (timerRunning && !isSolved && remainingTime <= 0f && !timeExpiredFired)
        {
            timerRunning = false;
            timeExpiredFired = true;
            inputLocked = true;
            onTimeExpired?.Invoke();
        }

        timerCo = null;
    }

    void UpdateTimerUI()
    {
        if (!timerText) return;
        float t = Mathf.Max(0f, remainingTime);
        int mins = Mathf.FloorToInt(t / 60f);
        int secs = Mathf.FloorToInt(t % 60f);
        timerText.text = $"{mins:0}:{secs:00}";
        timerText.color = (t <= hurryThreshold) ? timerHurry : timerNormal;
    }

    // --- helper for the cable renderer ---
    public Vector3 GetCellWorldCenter(Vector2Int p) => cells[p.x, p.y].transform.position;

    // ============= MATH HIT-TEST =============
    
    // Error throttle
    private float lastErrorAt;
    private const float errorCooldown = 0.12f;

    bool TryGetCellFromMouse(Vector2 screenPos, out Vector2Int cellXY)
    {
        cellXY = default;
        if (!gridParent) return false;

        RectTransform rt = gridParent;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPos, uiCam, out var local))
            return false;

        Rect r = rt.rect;
        float stepX = cellSize.x + spacing.x;
        float stepY = cellSize.y + spacing.y;

        int col = -1, row = -1;

        switch (startCorner)
        {
            case GridLayoutGroup.Corner.UpperLeft:
            {
                float originX = r.xMin + padding.left;
                float originY = r.yMax - padding.top;
                float dx = local.x - originX;
                float dy = originY - local.y;
                if (dx < 0f || dy < 0f) return false;
                col = Mathf.FloorToInt(dx / stepX);
                row = Mathf.FloorToInt(dy / stepY);
                break;
            }
            case GridLayoutGroup.Corner.LowerLeft:
            {
                float originX = r.xMin + padding.left;
                float originY = r.yMin + padding.bottom;
                float dx = local.x - originX;
                float dy = local.y - originY;
                if (dx < 0f || dy < 0f) return false;
                col = Mathf.FloorToInt(dx / stepX);
                row = Mathf.FloorToInt(dy / stepY);
                break;
            }
            case GridLayoutGroup.Corner.UpperRight:
            {
                float originX = r.xMax - padding.right;
                float originY = r.yMax - padding.top;
                float dxR = originX - local.x;
                float dy  = originY - local.y;
                if (dxR < 0f || dy < 0f) return false;
                int fromRight = Mathf.FloorToInt(dxR / stepX);
                col = (level.width - 1) - fromRight;
                row = Mathf.FloorToInt(dy / stepY);
                break;
            }
            case GridLayoutGroup.Corner.LowerRight:
            {
                float originX = r.xMax - padding.right;
                float originY = r.yMin + padding.bottom;
                float dxR = originX - local.x;
                float dy  = local.y - originY;
                if (dxR < 0f || dy < 0f) return false;
                int fromRight = Mathf.FloorToInt(dxR / stepX);
                col = (level.width - 1) - fromRight;
                row = Mathf.FloorToInt(dy / stepY);
                break;
            }
        }

        if (col < 0 || col >= level.width || row < 0 || row >= level.height) return false;

        cellXY = new Vector2Int(col, row);
        return true;
    }
}
