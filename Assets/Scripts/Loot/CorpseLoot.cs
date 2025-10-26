using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class CorpseLoot : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Generated Loot")]
    public List<ItemInstance> items = new(); // rolled items
    public int dollars;                      // money on corpse

    [Header("Lifetime")]
    public float lootableSeconds = 20f;
    float _dieTime;

    [Header("Refs (assigned by NPCHealth on death)")]
    public NPCLootProfile profile;
    public LootUI lootUIPrefab;
    public EquipmentManager cursorOwner;
    public Texture2D lootCursor;
    public Vector2 lootHotspot = new Vector2(8, 2);

    [Header("UI target")]
    public Canvas preferredCanvas; // main UI canvas to spawn LootWindow under

    LootUI _openUI;
    bool _hovering;

    // -------------------------------------------------
    // Called by NPCHealth on death to roll loot
    // -------------------------------------------------
    public void Generate(int npcLevel)
    {
        _dieTime = Time.time + lootableSeconds;

        // Roll dollars
        dollars = profile ? profile.GetRandomMoney(npcLevel) : 0;

        // Roll items (cap to 3-ish)
        int rolls = profile ? Random.Range(profile.itemRolls.x, profile.itemRolls.y + 1) : 2;
        items = profile ? profile.RollItems(npcLevel, rolls) : new List<ItemInstance>();
        if (items.Count > 3)
            items.RemoveRange(3, items.Count - 3);
    }

    void Update()
    {
        // Auto-despawn if timer expired OR empty
        if (Time.time >= _dieTime || IsEmpty)
        {
            // make sure loot window closes cleanly (also hides tooltip)
            if (_openUI) _openUI.Close();

            Destroy(gameObject);
        }
    }

    // Is corpse fully looted?
    public bool IsEmpty => (dollars <= 0) && (items == null || items.Count == 0);

    // -------------------------------------------------
    // Public helpers LootUI will call
    // -------------------------------------------------

    // Player took an item at corpseIndex
    public ItemInstance LootItem(int corpseIndex)
    {
        if (items == null) return null;
        if (corpseIndex < 0 || corpseIndex >= items.Count) return null;

        var inst = items[corpseIndex];
        if (inst != null)
        {
            // remove it from corpse
            items.RemoveAt(corpseIndex);
        }
        return inst;
    }

    // Player looted all the dollars
    public int LootMoney()
    {
        int amt = dollars;
        dollars = 0;
        return amt;
    }

    // -------------------------------------------------
    // Pointer callbacks (needs PhysicsRaycaster on camera)
    // -------------------------------------------------

    public void OnPointerEnter(PointerEventData eventData)
    {
        _hovering = true;

        if (lootCursor)
            Cursor.SetCursor(lootCursor, lootHotspot, CursorMode.Auto);
        else
            cursorOwner?.SetHoverCursor();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hovering = false;

        cursorOwner?.RestoreCursor();
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        OpenLootUI();
    }

    // -------------------------------------------------
    // Loot window spawning
    // -------------------------------------------------
    void OpenLootUI()
    {
        if (!lootUIPrefab)
        {
            Debug.LogWarning("CorpseLoot.OpenLootUI: lootUIPrefab is null, cannot open loot.", this);
            return;
        }

        // pick which canvas we want to parent LootWindow under
        Canvas canvas = preferredCanvas;
        if (!canvas)
        {
            // try to find the "UI" canvas
            var allCanvases = FindObjectsOfType<Canvas>();
            foreach (var c in allCanvases)
            {
                if (c.name == "UI") { canvas = c; break; }
            }

            // last resort, literally any canvas
            if (!canvas)
                canvas = FindAnyObjectByType<Canvas>();
        }

        if (!canvas)
        {
            Debug.LogWarning("CorpseLoot.OpenLootUI: No Canvas found, cannot show loot window.", this);
            return;
        }

        // instantiate once and keep reusing
        if (_openUI == null)
        {
            _openUI = Instantiate(lootUIPrefab, canvas.transform);
            _openUI.gameObject.name = "LootWindow(Clone)";

            // auto-wire refs if missing
            if (_openUI.playerInventory == null)
                _openUI.playerInventory = FindAnyObjectByType<Inventory>();
            if (_openUI.wallet == null)
                _openUI.wallet = FindAnyObjectByType<PlayerWallet>();
            if (_openUI.dragController == null)
                _openUI.dragController = FindAnyObjectByType<DragController>();
            if (_openUI.tooltip == null)
                _openUI.tooltip = FindAnyObjectByType<ItemTooltipUI>();

            // initial position
            RectTransform rt = _openUI.transform as RectTransform;
            if (rt)
            {
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
            }
        }

        // bind (tells LootUI: "this corpse is your data source")
        _openUI.Bind(this);

        // show, move on top, and build the slots
        _openUI.gameObject.SetActive(true);
        _openUI.transform.SetAsLastSibling();
        _openUI.Refresh(); // calls RebuildSlots() inside LootUI
    }

    // called if some external code wants to instantly nuke corpse (not strictly required anymore)
    public void OnLootAllTaken()
    {
        if (_openUI) _openUI.Close();
        Destroy(gameObject);
    }
}