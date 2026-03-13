using System.Collections.Generic;
using UnityEngine;

public class Vendor : MonoBehaviour
{
    [Header("Config")]
    public string vendorId = "Vendor";
    public VendorDefinition definition;

    [Header("Refs")]
    public VendorUI vendorUI;
    public Inventory playerInventory;
    public PlayerWallet wallet;

    [Header("Runtime stock (sold items are added here too)")]
    public List<ItemStack> stock = new();

    [Header("Auto Close")]
    public float autoCloseDistance = 4f;

    [Header("SFX")]
    public AudioClip buySfx;
    public AudioClip sellSfx;
    [Range(0f, 1f)] public float sfxVolume = 0.8f;

    public int vendorItemLevel = 1;

    Transform player;
    bool isOpen = false;

    void Awake()
    {
        if (definition != null && stock.Count == 0)
        {
            int ilvl = Mathf.Max(1, vendorItemLevel); // add this field, or hardcode 1 for now

            foreach (var e in definition.startingStock)
            {
                if (!e.template) continue;

                int amt = Mathf.Max(1, e.amount);

                bool stackable = e.template.isStackable;

                if (stackable)
                {
                    // stackables can be rolled once (usually fine)
                    var inst = BuildInstanceFromTemplate(e.template, ilvl);
                    AddToStock(inst, amt);
                }
                else
                {
                    // equipment/non-stackables: roll EACH copy so stats differ
                    for (int n = 0; n < amt; n++)
                    {
                        var inst = BuildInstanceFromTemplate(e.template, ilvl);
                        AddToStock(inst, 1);
                    }
                }
            }
        }
    }
    void Update()
    {
        if (!isOpen || player == null) return;

        float dist = Vector3.Distance(player.position, transform.position);

        if (dist > autoCloseDistance)
        {
            Close();
        }
    }

    public void Open()
    {
        if (!vendorUI)
            vendorUI = FindObjectOfType<VendorUI>(true);

        if (!vendorUI)
        {
            Debug.LogError("[Vendor] No VendorUI found.");
            return;
        }

        vendorUI.Show(this);
        DialogueController.I?.SetExternalLock(true);

        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        isOpen = true;
    }

    public void Close()
    {
        if (!isOpen) return;

        isOpen = false;

        if (vendorUI)
            vendorUI.Hide();

        DialogueController.I?.SetExternalLock(false);
    }

    // ---------- Pricing ----------
    public int EvaluatePrice(ItemInstance inst)
    {
        if (inst == null) return 1;

        // If instance has a non-zero explicit value, use it
        if (inst.value > 0) return Mathf.Max(1, inst.value);

        // Otherwise use your PriceCalculator (rarity + ilvl)
        return Mathf.Max(1, PriceCalculator.Evaluate(inst));
    }

    public int BuyPrice(ItemInstance inst)
    {
        float mult = (definition != null) ? definition.buyMultiplier : 1f;
        return Mathf.Max(1, Mathf.RoundToInt(EvaluatePrice(inst) * mult));
    }

    public int SellPrice(ItemInstance inst)
    {
        float mult = (definition != null) ? definition.sellMultiplier : 1f;
        return Mathf.Max(1, Mathf.RoundToInt(EvaluatePrice(inst) * mult));
    }

    // ---------- Buy ----------
    public bool TryBuy(int vendorIndex)
    {
        if (playerInventory == null || wallet == null) return false;
        if (vendorIndex < 0 || vendorIndex >= stock.Count) return false;

        var s = stock[vendorIndex];
        if (s.IsEmpty) return false;

        int price = BuyPrice(s.item);
        if (!wallet.Spend(price)) return false; // not enough money

        // Add to player bag (if fails, refund)
        bool added = playerInventory.Add(s.item, 1);
        if (!added)
        {
            wallet.Add(price); // refund
            return false;
        }

        //Quest Tracking
        QuestEvents.ItemLooted?.Invoke(s.item.template.id, 1);

        // Remove 1 from vendor stack
        s.count -= 1;
        if (s.count <= 0) s = new ItemStack(null, 0);
        stock[vendorIndex] = s;

        vendorUI?.Refresh();
        PlaySfx(buySfx);
        return true;
    }

    // ---------- Sell (by bag index) ----------
    public bool TrySellFromBagIndex(int bagIndex)
    {
        if (playerInventory == null || wallet == null) return false;
        if (bagIndex < 0 || bagIndex >= playerInventory.Slots.Count) return false;

        var s = playerInventory.Slots[bagIndex];
        if (s == null || s.IsEmpty) return false;

        int price = SellPrice(s.item);
        string itemId = s.item?.template?.id; // grab BEFORE removal

        // Remove 1 from bag
        playerInventory.RemoveAt(bagIndex, 1);

        if (itemId != null) QuestEvents.ItemRemoved?.Invoke(itemId, 1);

        // Add to vendor stock so you can buy back
        AddToStock(s.item, 1);

        // Pay player
        wallet.Add(price);

        vendorUI?.Refresh();
        PlaySfx(sellSfx);
        return true;
    }

    // ---------- Stock helper ----------
    void AddToStock(ItemInstance inst, int amount)
    {
        if (inst == null || amount <= 0) return;

        bool stackable = inst.template != null && inst.template.isStackable;
        int maxStack = stackable ? Mathf.Max(1, inst.template.maxStackSize) : 1;

        // top-up existing stacks
        if (stackable)
        {
            for (int i = 0; i < stock.Count && amount > 0; i++)
            {
                var s = stock[i];
                if (s.IsEmpty) continue;
                if (s.item == null || s.item.template != inst.template) continue;

                int space = maxStack - s.count;
                if (space <= 0) continue;

                int add = Mathf.Min(space, amount);
                s.count += add;
                amount -= add;
                stock[i] = s;
            }
        }

        // add new stacks
        while (amount > 0)
        {
            int put = stackable ? Mathf.Min(amount, maxStack) : 1;
            stock.Add(new ItemStack(inst, put));
            amount -= put;
        }
    }

    void PlaySfx(AudioClip clip)
    {
        if (!clip) return;
        AudioSource.PlayClipAtPoint(clip, transform.position, sfxVolume);
    }

    ItemInstance BuildInstanceFromTemplate(ItemTemplate t, int ilvl)
    {
        // This is what corpse loot uses. It handles static items too.
        return AffixRoller.CreateForVendor(t, ilvl);
    }
}