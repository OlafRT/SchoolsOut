using UnityEngine;
using UnityEngine.EventSystems;

public class DragPanelWithHandle : MonoBehaviour,
    IPointerDownHandler, IBeginDragHandler, IDragHandler
{
    [SerializeField] RectTransform window; // The frame to move
    Canvas canvas;
    Vector2 dragOffset;

    void Awake()
    {
        canvas = GetComponentInParent<Canvas>();
        if (!window) window = transform.parent as RectTransform;
    }

    public void OnPointerDown(PointerEventData e)
    {
        // Bring this panel to the front immediately on click
        window.SetAsLastSibling();
    }

    public void OnBeginDrag(PointerEventData e)
    {
        // Make sure we're on top when the drag actually begins too
        window.SetAsLastSibling();

        var parent = window.parent as RectTransform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parent, e.position,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : e.pressEventCamera,
            out var pointerLocal);

        dragOffset = window.anchoredPosition - pointerLocal;
    }

    public void OnDrag(PointerEventData e)
    {
        var parent = window.parent as RectTransform;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parent, e.position,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : e.pressEventCamera,
            out var pointerLocal);

        Vector2 newPos = pointerLocal + dragOffset;
        window.anchoredPosition = ClampToParent(window, parent, newPos);
    }

    static Vector2 ClampToParent(RectTransform child, RectTransform parent, Vector2 desired)
    {
        Rect pr = parent.rect;
        Rect cr = child.rect;

        float minX = pr.xMin + cr.width  * child.pivot.x;
        float maxX = pr.xMax - cr.width  * (1f - child.pivot.x);
        float minY = pr.yMin + cr.height * child.pivot.y;
        float maxY = pr.yMax - cr.height * (1f - child.pivot.y);

        desired.x = Mathf.Clamp(desired.x, minX, maxX);
        desired.y = Mathf.Clamp(desired.y, minY, maxY);
        return desired;
    }
}
