using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[ExecuteAlways]
public class FlowCell : MonoBehaviour,
    IPointerDownHandler, IPointerEnterHandler, IPointerUpHandler, IDragHandler
{
    [HideInInspector] public int x, y;
    [HideInInspector] public FlowBoard board;

    [Header("Parts")]
    [SerializeField] private Image dot;
    [SerializeField] private Image armUp, armRight, armDown, armLeft;
    [SerializeField] private Image background; // raycast-catcher

    [Header("Visual Layout")]
    [SerializeField, Min(1f)] private float armThickness = 16f;
    [SerializeField, Min(0f)] private float edgeInset = 1f;

    RectTransform rt, upRT, rightRT, downRT, leftRT;

    void Awake()  { AutoHook(); EnsureRaycastGraphic(); LayoutArms(); }
    void OnEnable(){ AutoHook(); EnsureRaycastGraphic(); LayoutArms(); }
    void OnValidate(){ AutoHook(); EnsureRaycastGraphic(); LayoutArms(); }
    void OnRectTransformDimensionsChange(){ LayoutArms(); }

    void AutoHook()
    {
        rt = (RectTransform)transform;
        if (!dot)      dot      = transform.Find("Dot")      ? transform.Find("Dot").GetComponent<Image>()      : null;
        if (!armUp)    armUp    = transform.Find("ArmUp")    ? transform.Find("ArmUp").GetComponent<Image>()    : null;
        if (!armRight) armRight = transform.Find("ArmRight") ? transform.Find("ArmRight").GetComponent<Image>() : null;
        if (!armDown)  armDown  = transform.Find("ArmDown")  ? transform.Find("ArmDown").GetComponent<Image>()  : null;
        if (!armLeft)  armLeft  = transform.Find("ArmLeft")  ? transform.Find("ArmLeft").GetComponent<Image>()  : null;

        upRT    = armUp    ? (RectTransform)armUp.transform    : null;
        rightRT = armRight ? (RectTransform)armRight.transform : null;
        downRT  = armDown  ? (RectTransform)armDown.transform  : null;
        leftRT  = armLeft  ? (RectTransform)armLeft.transform  : null;

        // cables shouldn't block input
        if (dot) dot.raycastTarget = false;
        if (armUp) armUp.raycastTarget = false;
        if (armRight) armRight.raycastTarget = false;
        if (armDown) armDown.raycastTarget = false;
        if (armLeft) armLeft.raycastTarget = false;

        SetEmptyVisual();
    }

    void EnsureRaycastGraphic()
    {
        if (!background) background = GetComponent<Image>();
        if (!background) background = gameObject.AddComponent<Image>();
        background.raycastTarget = true;
        var c = background.color; c.a = Mathf.Max(0.001f, c.a); background.color = c; // nearly invisible
    }

    void LayoutArms()
    {
        if (!rt) return;
        float size = Mathf.Min(rt.rect.width, rt.rect.height);
        float half = Mathf.Max(0f, size * 0.5f - edgeInset);

        LayoutArm(upRT,    new Vector2(armThickness, half), new Vector2(0f,  half * 0.5f));
        LayoutArm(downRT,  new Vector2(armThickness, half), new Vector2(0f, -half * 0.5f));
        LayoutArm(rightRT, new Vector2(half, armThickness), new Vector2( half * 0.5f, 0f));
        LayoutArm(leftRT,  new Vector2(half, armThickness), new Vector2(-half * 0.5f, 0f));
    }

    void LayoutArm(RectTransform a, Vector2 size, Vector2 offset)
    {
        if (!a) return;
        a.anchorMin = a.anchorMax = new Vector2(0.5f, 0.5f);
        a.pivot = new Vector2(0.5f, 0.5f);
        a.sizeDelta = size;
        a.anchoredPosition = offset;
    }

    // API used by FlowBoard
    public void Setup(int X, int Y, FlowBoard B) { x = X; y = Y; board = B; SetEmptyVisual(); }
    public void SetEndpoint(Color c, bool on) { if (dot) { dot.enabled = on; dot.color = c; } }

    public void SetEmptyVisual()
    {
        if (dot) dot.enabled = false;
        if (armUp)    armUp.enabled    = false;
        if (armRight) armRight.enabled = false;
        if (armDown)  armDown.enabled  = false;
        if (armLeft)  armLeft.enabled  = false;
    }

    public void UpdateArms(Color c, bool up, bool right, bool down, bool left)
    {
        if (armUp)    { armUp.enabled = up;     armUp.color = c; }
        if (armRight) { armRight.enabled = right; armRight.color = c; }
        if (armDown)  { armDown.enabled = down;   armDown.color = c; }
        if (armLeft)  { armLeft.enabled = left;   armLeft.color = c; }
    }

    // Input forwarding (FlowBoard polls mouse during drag now)
    public void OnPointerDown (PointerEventData e) => board?.CellPointerDown(x, y);
    public void OnPointerEnter(PointerEventData e) => board?.CellPointerEnter(x, y);
    public void OnPointerUp   (PointerEventData e) => board?.CellPointerUp(x, y);
    public void OnDrag        (PointerEventData e) { /* no-op: board polls in Update */ }
}
