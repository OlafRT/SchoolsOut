using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class VendorBuySlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Image icon;
    public TMP_Text countText;
    public TMP_Text priceText;
    public Button button;

    Vendor vendor;
    int vendorIndex;

    ItemInstance currentItem;
    VendorUI vendorUI;
    RectTransform rect;

    void Awake()
    {
        rect = transform as RectTransform;
        vendorUI = GetComponentInParent<VendorUI>(true);

        if (button) button.onClick.AddListener(OnClick);
    }

    public void Set(Vendor v, int index, ItemInstance inst, int count, int price)
    {
        vendor = v;
        vendorIndex = index;
        currentItem = inst;

        if (icon)
        {
            icon.sprite = inst?.Icon;
            icon.enabled = (icon.sprite != null);
        }

        if (countText) countText.text = count > 1 ? count.ToString() : "";
        if (priceText) priceText.text = $"${price}";
        if (button) button.interactable = true;
    }

    public void Clear()
    {
        vendor = null;
        vendorIndex = -1;
        currentItem = null;

        if (icon) { icon.sprite = null; icon.enabled = false; }
        if (countText) countText.text = "";
        if (priceText) priceText.text = "";
        if (button) button.interactable = false;
    }

    void OnClick()
    {
        // Hide tooltip so it doesn't linger while buying
        vendorUI?.tooltip?.Hide();

        if (vendor == null) return;
        vendor.TryBuy(vendorIndex);
    }

    // ===== Tooltip hover =====

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (vendorUI == null || vendorUI.tooltip == null) return;
        if (currentItem == null || currentItem.template == null) return;

        vendorUI.tooltip.Show(currentItem, rect);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        vendorUI?.tooltip?.Hide();
    }
}