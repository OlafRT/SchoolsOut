using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class LootSlotUI : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerClickHandler
{
    [Header("UI")]
    public Image icon;
    public TMP_Text textLabel; // for money text or can stay empty for items
    public Image glowRing;     // optional rarity glow
    public ParticleSystem legendaryFx; // optional legendary VFX

    [Header("Refs")]
    public ItemTooltipUI tooltip;
    public LootUI owner;

    // what this slot currently represents
    bool _active;
    bool _isMoney;
    int _corpseIndex = -1;
    ItemInstance _itemCached;

    void Awake()
    {
        DisableFx();
        HideMe();
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

    void ApplyFlourish(ItemInstance itm)
    {
        if (glowRing) glowRing.enabled = false;
        if (legendaryFx)
        {
            legendaryFx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            legendaryFx.gameObject.SetActive(false);
        }

        if (itm == null) return;

        var r = itm.rarity;
        if (r == Rarity.Epic)
        {
            if (glowRing)
            {
                glowRing.enabled = true;
                glowRing.color = RarityFX.Glow(r);
            }
        }
        else if (r == Rarity.Legendary)
        {
            if (glowRing)
            {
                glowRing.enabled = true;
                glowRing.color = RarityFX.Glow(r);
            }
            if (legendaryFx)
            {
                var main = legendaryFx.main;
                main.loop = true;
                legendaryFx.gameObject.SetActive(true);
                if (!legendaryFx.isPlaying) legendaryFx.Play();
            }
        }
    }

    // Call this to turn this slot OFF (no loot here)
    public void HideMe()
    {
        _active = false;
        _isMoney = false;
        _corpseIndex = -1;
        _itemCached = null;

        if (icon) icon.enabled = false;
        if (textLabel) textLabel.text = "";

        gameObject.SetActive(false);
    }

    // Call this to make this slot represent MONEY
    public void ShowMoney(LootUI parent, int dollarsAmount, Sprite moneySprite)
    {
        owner = parent;
        tooltip = parent.tooltip;

        _active = true;
        _isMoney = true;
        _corpseIndex = -1;
        _itemCached = null;

        gameObject.SetActive(true);

        if (icon)
        {
            icon.enabled = true;
            icon.sprite = moneySprite != null ? moneySprite : null;
        }

        if (textLabel)
        {
            // show "$ 12" etc.
            textLabel.text = "$ " + dollarsAmount;
        }

        DisableFx(); // money doesn't get rarity glow
    }

    // Call this to make this slot represent an ITEM from corpse.items[index]
    public void ShowItem(LootUI parent, int corpseIndex, ItemInstance inst)
    {
        owner = parent;
        tooltip = parent.tooltip;

        _active = true;
        _isMoney = false;
        _corpseIndex = corpseIndex;
        _itemCached = inst;

        gameObject.SetActive(true);

        if (icon)
        {
            icon.enabled = true;
            icon.sprite = inst != null ? inst.Icon : null;
        }

        if (textLabel)
        {
            // item row usually doesn't need text; clear it
            textLabel.text = "";
        }

        ApplyFlourish(inst);
    }

    // ---------- hover ----------
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!_active) return;
        if (_isMoney) return; // no tooltip for just money

        if (_itemCached != null && tooltip != null)
        {
            tooltip.Show(_itemCached, transform as RectTransform);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        tooltip?.Hide();
    }

    // ---------- click ----------
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!_active) return;
        if (owner == null) return;

        if (_isMoney)
        {
            owner.TakeMoney();
        }
        else
        {
            // loot that specific corpse index
            owner.TakeItem(_corpseIndex);
        }
    }
}
