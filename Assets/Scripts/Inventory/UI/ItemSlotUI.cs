using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections; 

public class ItemSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    [Header("UI")] public Image icon; public TMP_Text countText; public Button button;
    [Header("Flourish")] public Image glowRing; public ParticleSystem legendaryFx; // optional
    [Header("Index")] public int inventoryIndex = -1; // -1 means equipment slot
    public EquipSlot equipSlot; // used if inventoryIndex == -1

    [HideInInspector] public Inventory inventory;
    [HideInInspector] public EquipmentState equipment;
    [HideInInspector] public EquipmentManager equipMgr;
    [HideInInspector] public ItemTooltipUI tooltip;
    [HideInInspector] public DragController drag;

    void Awake()
    {
        if (button) button.onClick.AddListener(OnClick);
        DisableFx();
    }

    public void BindInventory(Inventory inv, int index, EquipmentManager mgr, ItemTooltipUI tip)
    {
    inventory = inv; equipment = mgr.equipment; equipMgr = mgr; tooltip = tip; inventoryIndex = index;
    if (!drag) drag = GetComponentInParent<DragController>();
    Refresh();
    }

    public void BindEquipment(EquipmentState eq, EquipSlot slot, EquipmentManager mgr, ItemTooltipUI tip)
    {
        equipment = eq; equipMgr = mgr; tooltip = tip; inventoryIndex = -1; equipSlot = slot;
        if (!drag) drag = GetComponentInParent<DragController>();
        Refresh();
    }

    public void Refresh()
    {
        ItemInstance itm = null;

        if (inventoryIndex >= 0)
        {
            // BAG
            if (inventory == null || inventory.Slots == null || inventoryIndex >= inventory.Slots.Count)
                return;

            var s = inventory.Slots[inventoryIndex];
            if (s.IsEmpty)
            {
                if (icon)
                {
                    icon.sprite = null;
                    icon.enabled = false;
                }
                if (countText) countText.text = "";
            }
            else
            {
                itm = s.item;

                if (icon)
                {
                    icon.sprite = itm?.Icon;
                    icon.enabled = (icon.sprite != null);
                }

                // show stack size if we have more than one
                if (countText)
                    countText.text = s.count > 1 ? s.count.ToString() : "";
            }
        }
        else
        {
            // EQUIPMENT
            if (equipment == null) return;

            itm = equipment.Get(equipSlot);
            if (itm == null)
            {
                if (icon)
                {
                    icon.sprite = null;
                    icon.enabled = false;
                }
                if (countText) countText.text = "";
            }
            else
            {
                if (icon)
                {
                    icon.sprite = itm.Icon;
                    icon.enabled = (icon.sprite != null);
                }
                // no stacking on equipment
                if (countText) countText.text = "";
            }
        }

        ApplyFlourish(itm);
    }

    void ApplyFlourish(ItemInstance itm)
    {
        if (glowRing) glowRing.enabled = false;
        if (legendaryFx) legendaryFx.gameObject.SetActive(false);
        if (itm == null) return;

        var r = itm.rarity;
        if (r == Rarity.Epic)
        {
            if (glowRing) { glowRing.enabled = true; glowRing.color = RarityFX.Glow(r); }
        }
        else if (r == Rarity.Legendary)
        {
            if (glowRing) { glowRing.enabled = true; glowRing.color = RarityFX.Glow(r); }
            if (legendaryFx)
            {
                var main = legendaryFx.main;
                main.loop = true;
                legendaryFx.gameObject.SetActive(true);
                if (!legendaryFx.isPlaying) legendaryFx.Play();
            }
        }
    }

    void DisableFx()
    {
        if (glowRing) glowRing.enabled = false;
        if (legendaryFx)
        {
            legendaryFx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            legendaryFx.gameObject.SetActive(false);
        }
    }

    // ===== Tooltip + Click handling via Unity UI events =====

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!tooltip) return;

        // figure out what item lives in THIS slot
        ItemInstance item = null;
        if (inventoryIndex >= 0)
        {
            var s = inventory?.Slots?[inventoryIndex];
            if (s != null && !s.IsEmpty && s.item?.template != null)
                item = s.item;
        }
        else
        {
            var eqItem = equipment?.Get(equipSlot);
            if (eqItem != null && eqItem.template != null)
                item = eqItem;
        }

        // only do fancy stuff if we actually have an item
        if (item != null)
        {
            // change cursor to "hand"/hover
            equipMgr?.SetHoverCursor();

            // IMPORTANT: pass the RectTransform anchor now
            tooltip.Show(item, transform as RectTransform);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // hide tooltip no matter what
        tooltip?.Hide();

        // restore cursor
        equipMgr?.RestoreCursor();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        OnClick();
    }

    void OnClick()
    {
        bool ok = true;

        if (inventoryIndex >= 0)
        {
            // clicked a BAG slot
            var s = inventory.Slots[inventoryIndex];
            if (!s.IsEmpty && s.item?.template?.isEquippable == true)
            {
                ok = equipMgr.TryEquipFromInventory(inventoryIndex);
            }
        }
        else
        {
            // clicked an EQUIPPED slot
            ok = equipMgr.TryUnequipToInventory(equipSlot);
        }

        if (!ok)
            StartCoroutine(Flash(Color.red));

        Refresh();
    }

    public void OnBeginDrag(PointerEventData e)
    {
        if (drag == null) return;

        if (inventoryIndex >= 0)
        {
            // dragging from BAG
            var s = inventory?.Slots?[inventoryIndex];
            if (s != null && !s.IsEmpty && s.item?.Icon != null)
                drag.BeginDragFromBag(inventoryIndex, s.item.Icon);
        }
        else
        {
            // dragging from EQUIPPED slot
            var itm = equipment?.Get(equipSlot);
            if (itm != null && itm.Icon != null)
                drag.BeginDragFromEquip(equipSlot, itm.Icon);
        }

        if (drag.IsDragging)
            tooltip?.Hide();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (drag == null || !drag.IsDragging) return;
        drag.UpdateGhost(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (drag == null) return;
        drag.EndDragPotentiallyDestroy();
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (drag == null || !drag.IsDragging) return;

        if (inventoryIndex >= 0)
            drag.DropOnBag(inventoryIndex);
        else
            drag.DropOnEquip(equipSlot);
    }

    IEnumerator Flash(Color c)
    {
        var img = icon;
        if (!img) yield break;

        Color start = img.color;
        float t = 0f;

        while (t < 0.25f)
        {
            t += Time.unscaledDeltaTime;
            img.color = Color.Lerp(start, c, t / 0.25f);
            yield return null;
        }

        t = 0f;
        while (t < 0.35f)
        {
            t += Time.unscaledDeltaTime;
            img.color = Color.Lerp(c, start, t / 0.35f);
            yield return null;
        }

        img.color = start;
    }
}
