using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class LootSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public Image icon;
    public TMP_Text label;

    LootUI _lootUI;
    bool _isMoney;
    int _corpseItemIndex = -1;

    ItemInstance _item;

    public void BindMoney(LootUI lootUI, int slotIndexZero) { _lootUI = lootUI; _isMoney = true; }
    public void BindItem(LootUI lootUI, int corpseItemIndex) { _lootUI = lootUI; _corpseItemIndex = corpseItemIndex; _isMoney = false; }

    public void SetMoney(int dollars)
    {
        _item = null;
        icon.enabled = true;
        icon.sprite = null; // use a $ sprite if you have one
        label.text = dollars > 0 ? $"$ {dollars:N0}" : "";
        gameObject.SetActive(dollars > 0);
    }

    public void SetItem(ItemInstance item)
    {
        _item = item;
        if (item == null)
        {
            icon.enabled = false; label.text = ""; gameObject.SetActive(false);
        }
        else
        {
            icon.enabled = item.Icon != null;
            icon.sprite = item.Icon;
            label.text = item.DisplayName;
            gameObject.SetActive(true);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_isMoney || _item == null || _lootUI.tooltip == null) return;
        _lootUI.tooltip.Show(_item);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _lootUI.tooltip?.Hide();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_isMoney) _lootUI.TakeMoney();
        else if (_item != null) _lootUI.TakeItem(_corpseItemIndex);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_isMoney || _item == null) return;
        if (_item.Icon == null) return;
        _lootUI.BeginDragFromLoot(_corpseItemIndex, _item.Icon);
        _lootUI.tooltip?.Hide();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_lootUI.dragController) _lootUI.dragController.UpdateGhost(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (_lootUI.dragController) _lootUI.dragController.EndDragFromLoot();
    }
}
