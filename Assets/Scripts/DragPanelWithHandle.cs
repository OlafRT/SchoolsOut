using UnityEngine;
using UnityEngine.EventSystems;

public class DragPanelWithHandle : MonoBehaviour,
    IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler,
    IPointerEnterHandler, IPointerExitHandler, IPointerUpHandler
{
    [Header("Window")]
    [SerializeField] RectTransform window; // The frame to move
    Canvas canvas;
    Vector2 dragOffset;

    public enum ClampSpace { ParentRect, CanvasRoot, ScreenPixels }
    [Header("Clamping")]
    public ClampSpace clampTo = ClampSpace.ParentRect;
    [Tooltip("Extra margin kept inside the bounds.")]
    public Vector2 clampPadding = new Vector2(8, 8);

    // Cursor bits (optional; keep or remove if you already added these)
    [Header("Cursor")]
    public Texture2D handCursor;
    public Texture2D grabbingCursor;
    public Vector2 cursorHotspot = new Vector2(8, 8);
    public CursorMode cursorMode = CursorMode.Auto;
    bool pointerOver, dragging;

    void Awake()
    {
        canvas = GetComponentInParent<Canvas>();
        if (!window) window = transform.parent as RectTransform;
    }

    // ---------- Cursor helpers ----------
    void SetHoverCursor(){ if (handCursor) Cursor.SetCursor(handCursor, cursorHotspot, cursorMode); }
    void SetDraggingCursor(){ var tex = grabbingCursor ? grabbingCursor : handCursor; if (tex) Cursor.SetCursor(tex, cursorHotspot, cursorMode); }
    void RestoreCursor(){ Cursor.SetCursor(null, Vector2.zero, cursorMode); }

    // ---------- Pointer + drag ----------
    public void OnPointerEnter(PointerEventData e){ pointerOver = true; if (!dragging) SetHoverCursor(); }
    public void OnPointerExit (PointerEventData e){ pointerOver = false; if (!dragging) RestoreCursor(); }
    public void OnPointerUp   (PointerEventData e){ if (!dragging) { if (pointerOver) SetHoverCursor(); else RestoreCursor(); } }

    public void OnPointerDown(PointerEventData e)
    {
        window.SetAsLastSibling();
    }

    public void OnBeginDrag(PointerEventData e)
    {
        dragging = true;
        window.SetAsLastSibling();

        var parent = window.parent as RectTransform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parent, e.position,
            canvas && canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : e.pressEventCamera,
            out var pointerLocal);

        dragOffset = window.anchoredPosition - pointerLocal;
        SetDraggingCursor();
    }

    public void OnDrag(PointerEventData e)
    {
        var parent = window.parent as RectTransform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parent, e.position,
            canvas && canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : e.pressEventCamera,
            out var pointerLocal);

        Vector2 desiredAnchored = pointerLocal + dragOffset;

        // Move first, then clamp with world-aware method
        window.anchoredPosition = desiredAnchored;
        ClampToBounds();
    }

    public void OnEndDrag(PointerEventData e)
    {
        dragging = false;
        if (pointerOver) SetHoverCursor(); else RestoreCursor();
    }

    // ---------- Robust clamp ----------
    void ClampToBounds()
    {
        if (!window) return;

        // Get the target rect we’re clamping against (in world space)
        Rect worldBounds = clampTo switch
        {
            ClampSpace.ParentRect  => WorldRectOf(window.parent as RectTransform),
            ClampSpace.CanvasRoot  => WorldRectOf(canvas ? canvas.transform as RectTransform : window.root as RectTransform),
            ClampSpace.ScreenPixels=> ScreenWorldRect(canvas),
            _ => WorldRectOf(window.parent as RectTransform)
        };

        // Current window world rect (from corners)
        Rect w = WorldRectOf(window);

        // Compute delta needed to push window fully inside bounds (with padding)
        Vector2 pad = clampPadding;
        float dx = 0f, dy = 0f;

        // Left
        float leftGap = (w.xMin - (worldBounds.xMin + pad.x));
        if (leftGap < 0f) dx -= leftGap;

        // Right
        float rightGap = ((worldBounds.xMax - pad.x) - w.xMax);
        if (rightGap < 0f) dx += rightGap;

        // Bottom
        float bottomGap = (w.yMin - (worldBounds.yMin + pad.y));
        if (bottomGap < 0f) dy -= bottomGap;

        // Top
        float topGap = ((worldBounds.yMax - pad.y) - w.yMax);
        if (topGap < 0f) dy += topGap;

        if (Mathf.Approximately(dx, 0f) && Mathf.Approximately(dy, 0f)) return;

        // Apply in world space, then convert to anchoredPosition shift
        window.position += new Vector3(dx, dy, 0f);

        // (No extra math needed; moving Transform.position moves the rect in all modes.)
    }

    static Rect WorldRectOf(RectTransform rt)
    {
        if (!rt) return new Rect();
        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        float xMin = corners[0].x, yMin = corners[0].y;
        float xMax = corners[2].x, yMax = corners[2].y;
        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    static Rect ScreenWorldRect(Canvas canvas)
    {
        // A rect that matches the actual screen in world space (for ScreenSpaceOverlay this is screen pixels)
        // For SS Overlay we can map pixels 1:1 using camera=null; for other modes this provides a reasonable clamp to visible area.
        return new Rect(0, 0, Screen.width, Screen.height);
    }
}
