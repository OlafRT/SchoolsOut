using UnityEngine;
using UnityEngine.EventSystems;

public class VendorSellDropZone : MonoBehaviour, IDropHandler
{
    public VendorUI vendorUI;
    public DragController drag;

    void Awake()
    {
        if (!vendorUI) vendorUI = GetComponentInParent<VendorUI>();
        if (!drag) drag = GetComponentInParent<DragController>();
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (!vendorUI || !drag || !drag.IsDragging) return;
        drag.DropOnVendorSell(vendorUI);
    }
}