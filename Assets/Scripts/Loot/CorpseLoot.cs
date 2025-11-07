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

    // ---------------- Loot FX ----------------
    [Header("Loot FX")]
    [Tooltip("Particle prefab (e.g., sparkles) to show while corpse has loot.")]
    public GameObject lootFXPrefab;
    [Tooltip("Local offset where the FX should sit (above the body).")]
    public Vector3 lootFXOffset = new Vector3(0f, 0.25f, 0f);
    GameObject _lootFXInstance;
    // ----------------------------------------------

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

        UpdateLootFX(); // NEW: turn on FX if we actually have loot
    }

    void Update()
    {
        // Auto-despawn if timer expired OR empty
        if (Time.time >= _dieTime || IsEmpty)
        {
            // close loot UI (which also hides tooltip & restores cursor from slot hover)
            if (_openUI) _openUI.Close();

            // also restore cursor in case player was still hovering the corpse itself
            ForceRestoreCursor();

            // NEW: clean up FX
            if (_lootFXInstance) Destroy(_lootFXInstance);

            Destroy(gameObject);
            return;
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
            items.RemoveAt(corpseIndex);
        }

        UpdateLootFX();
        return inst;
    }

    // Player looted all the dollars
    public int LootMoney()
    {
        int amt = dollars;
        dollars = 0;
        UpdateLootFX();
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
            var allCanvases = FindObjectsOfType<Canvas>();
            foreach (var c in allCanvases)
            {
                if (c.name == "UI") { canvas = c; break; }
            }
            if (!canvas) canvas = FindAnyObjectByType<Canvas>();
        }

        if (!canvas)
        {
            Debug.LogWarning("CorpseLoot.OpenLootUI: No Canvas found, cannot show loot window.", this);
            return;
        }

        if (_openUI == null)
        {
            _openUI = Instantiate(lootUIPrefab, canvas.transform);
            _openUI.gameObject.name = "LootWindow(Clone)";

            if (_openUI.playerInventory == null)
                _openUI.playerInventory = FindAnyObjectByType<Inventory>();
            if (_openUI.wallet == null)
                _openUI.wallet = FindAnyObjectByType<PlayerWallet>();
            if (_openUI.dragController == null)
                _openUI.dragController = FindAnyObjectByType<DragController>();
            if (_openUI.tooltip == null)
                _openUI.tooltip = FindAnyObjectByType<ItemTooltipUI>();

            RectTransform rt = _openUI.transform as RectTransform;
            if (rt)
            {
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
            }
        }

        _openUI.Bind(this);
        _openUI.gameObject.SetActive(true);
        _openUI.transform.SetAsLastSibling();
        _openUI.Refresh();
    }

    // called if some external code wants to instantly nuke corpse (not strictly required anymore)
    public void OnLootAllTaken()
    {
        if (_openUI) _openUI.Close();

        ForceRestoreCursor();

        UpdateLootFX(); // hide sparkles if we became empty
        //Destroy(gameObject);
    }

    void ForceRestoreCursor()
    {
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        if (cursorOwner != null) cursorOwner.RestoreCursor();
    }

    public void PutBackItem(int corpseIndex, ItemInstance inst)
    {
        if (inst == null) return;
        if (items == null) items = new List<ItemInstance>();

        if (corpseIndex < 0 || corpseIndex > items.Count)
            corpseIndex = items.Count;

        items.Insert(corpseIndex, inst);
        UpdateLootFX();
    }

    // ---------------- FX spawn/cleanup ---------------
    void UpdateLootFX()
    {
        bool shouldHaveFX = !IsEmpty && lootFXPrefab != null;

        if (shouldHaveFX)
        {
            if (_lootFXInstance == null)
            {
                _lootFXInstance = Instantiate(lootFXPrefab, transform, false);
                _lootFXInstance.transform.localPosition = lootFXOffset;
            }
        }
        else
        {
            if (_lootFXInstance != null)
            {
                Destroy(_lootFXInstance);
                _lootFXInstance = null;
            }
        }
    }
    // ------------------------------------------------------
}
