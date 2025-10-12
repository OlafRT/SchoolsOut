using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ItemTooltipUI : MonoBehaviour
{
    public RectTransform panel;
    public TMP_Text titleText, statsText, descText, metaText;

    [Header("Positioning")]
    public Vector2 cursorOffset = new Vector2(32f, -24f); // right & slightly down
    public bool flipWhenOutside = true;

    bool _visible;
    Canvas _canvas;

    void Awake()
    {
        _canvas = panel.GetComponentInParent<Canvas>();

        // Tooltip must not block hover
        var cg = panel.GetComponent<CanvasGroup>();
        if (!cg) cg = panel.gameObject.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable = false;
        foreach (var g in panel.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;

        // Recommended for WoW-style
        panel.pivot = new Vector2(0f, 1f);      // top-left
        panel.anchorMin = new Vector2(0f, 1f);  // not required, but fine
        panel.anchorMax = new Vector2(0f, 1f);

        panel.gameObject.SetActive(false);
    }

    void Update()
    {
        if (_visible) MoveToMouse();
    }

    public void Show(ItemInstance item, RectTransform _ = null)
    {
        if (item == null) return;

        panel.gameObject.SetActive(true);
        _visible = true;
        panel.SetAsLastSibling();

        // Fill
        var rarity = item.rarity;
        titleText.text  = item.DisplayName + " (" + rarity + ")";
        titleText.color = RarityColors.Get(rarity);

        string stats = "";
        if (item.bonusMuscles   != 0) stats += $"+{item.bonusMuscles} Muscles\n";
        if (item.bonusIQ        != 0) stats += $"+{item.bonusIQ} IQ\n";
        if (item.bonusCrit      != 0) stats += $"+{item.bonusCrit}% Crit\n";
        if (item.bonusToughness != 0) stats += $"+{item.bonusToughness} Toughness\n";
        statsText.text = stats.TrimEnd();

        descText.text = item.template?.description ?? "";
        metaText.text = $"iLvl {item.itemLevel}  •  Req {item.requiredLevel}  •  Value ${item.value}";

        LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
        MoveToMouse();
    }

    public void Hide()
    {
        _visible = false;
        panel.gameObject.SetActive(false);
    }

    void MoveToMouse()
    {
        if (_canvas == null) return;

        // If you ever switch away from Overlay, fall back to the older local-space code,
        // but for Overlay we use raw screen pixels (most robust, zero offset surprises).
        if (_canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            float scale = _canvas.scaleFactor;                       // Canvas Scaler
            Vector2 sizePx = panel.rect.size * scale;                // tooltip size in screen pixels

            Vector2 mouse = Input.mousePosition;                     // screen pixels
            Vector2 pos = mouse + cursorOffset;                      // preferred (right & down)

            if (flipWhenOutside)
            {
                if (pos.x + sizePx.x > Screen.width)
                    pos.x = mouse.x - sizePx.x - Mathf.Abs(cursorOffset.x);
                if (pos.y - sizePx.y < 0f)
                    pos.y = mouse.y + Mathf.Abs(cursorOffset.y);
            }

            pos.x = Mathf.Clamp(pos.x, 0f, Screen.width  - sizePx.x);
            pos.y = Mathf.Clamp(pos.y, sizePx.y, Screen.height);

            // Set world/screen position directly in Overlay
            panel.position = pos;
        }
        else
        {
            // Non-overlay fallback (rare in your case)
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)_canvas.transform,
                Input.mousePosition,
                _canvas.worldCamera,
                out var local
            );
            local += cursorOffset;
            panel.anchoredPosition = local;
        }
    }
}
