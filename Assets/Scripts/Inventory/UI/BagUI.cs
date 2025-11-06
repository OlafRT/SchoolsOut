using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BagUI : MonoBehaviour
{
    public Inventory inventory;
    public EquipmentManager equipMgr;
    public ItemTooltipUI tooltip;
    public ItemSlotUI slotPrefab;
    public GridLayoutGroup grid;

    private readonly List<ItemSlotUI> slots = new();

    void OnEnable()
    {
        BuildIfNeeded();
        inventory.OnInventoryChanged += Refresh;
        Refresh();
        RebuildLayoutNow();   // ensure fresh rects when the panel opens
    }

    void OnDisable()
    {
        inventory.OnInventoryChanged -= Refresh;
        if (tooltip) tooltip.Hide();
        if (equipMgr) equipMgr.RestoreCursor();
    }

    void BuildIfNeeded()
    {
        if (slots.Count > 0) return;

        for (int i = 0; i < inventory.capacity; i++)
        {
            var s = Instantiate(slotPrefab, grid.transform);
            // Make sure each child is top-left anchored & sane (defensive)
            var rt = (RectTransform)s.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0f, 1f);
            rt.localScale = Vector3.one;
            rt.sizeDelta = Vector2.zero;

            s.BindInventory(inventory, i, equipMgr, tooltip);
            slots.Add(s);
        }

        RebuildLayoutNow(); // layout right after building
    }

    void Refresh()
    {
        foreach (var s in slots) s.Refresh();
        RebuildLayoutNow(); // layout after content changes
    }

    // The key: force the grid to recompute and push correct rects to the EventSystem
    void RebuildLayoutNow()
    {
        if (!grid) return;
        var rect = grid.transform as RectTransform;
        Canvas.ForceUpdateCanvases();                        // flush pending UI changes
        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);   // rebuild this hierarchy now
        grid.SetLayoutHorizontal();                          // extra nudge for GridLayoutGroup
        grid.SetLayoutVertical();
        Canvas.ForceUpdateCanvases();                        // finalize before hits happen
    }
}
