using UnityEngine;
using UnityEngine.UI;

public class VendorUI : MonoBehaviour
{
    [Header("Root")]
    public GameObject panel;

    [Header("Tabs")]
    public Button buyButton;
    public Button sellButton;
    public GameObject buyRoot;
    public GameObject sellRoot;

    [Header("Buy slots")]
    public VendorBuySlotUI[] buySlots; // assign your Slot objects that have VendorBuySlotUI

    public ItemTooltipUI tooltip;

    Vendor vendor;
    bool sellTab;

    void Awake()
    {
        if (!panel) panel = gameObject;
        panel.SetActive(false);

        if (buyButton) buyButton.onClick.AddListener(() => SetTab(false));
        if (sellButton) sellButton.onClick.AddListener(() => SetTab(true));
    }

    public void Show(Vendor v)
    {
        vendor = v;
        panel.SetActive(true);
        DialogueController.I?.SetExternalLock(true);
        SetTab(false);
        Refresh();
    }

    public void Hide()
    {
        if (tooltip) tooltip.Hide();
        panel.SetActive(false);
        vendor = null;
    }
    void SetTab(bool selling)
    {
        sellTab = selling;
        if (buyRoot) buyRoot.SetActive(!selling);
        if (sellRoot) sellRoot.SetActive(selling);
        Refresh();
    }

    public bool IsSellTab => sellTab;

    public void Refresh()
    {
        if (vendor == null) return;

        // Populate buy grid
        if (buySlots != null)
        {
            for (int i = 0; i < buySlots.Length; i++)
            {
                var s = (i < vendor.stock.Count) ? vendor.stock[i] : null;

                if (s != null && !s.IsEmpty)
                {
                    int price = vendor.BuyPrice(s.item);
                    buySlots[i].Set(vendor, i, s.item, s.count, price);
                }
                else
                {
                    buySlots[i].Clear();
                }
            }
        }
    }

    // called by DropZone
    public bool TrySellFromBagIndex(int bagIndex)
    {
        if (vendor == null) return false;
        if (!sellTab) return false;
        return vendor.TrySellFromBagIndex(bagIndex);
    }
}