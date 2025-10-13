using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class CorpseLoot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Generated")]
    public List<ItemInstance> items = new(); // up to 3 items
    public int dollars;

    [Header("Lifetime")]
    public float lootableSeconds = 20f; // corpse persists this long (then despawns)

    [Header("Refs")]
    public NPCLootProfile profile;
    public LootUI lootUIPrefab;           // small 4-slot vertical UI prefab
    public EquipmentManager cursorOwner;  // for cursor switching (optional)
    public Texture2D lootCursor;          // optional custom loot cursor
    public Vector2 lootHotspot = new(8, 2);

    LootUI _openUI;
    float _dieTime;
    bool _hovering;

    public static CorpseLoot SpawnFrom(NPCLootProfile profile, Vector3 at, int npcLevel, Transform parent = null)
    {
        var go = new GameObject("CorpseLoot", typeof(BoxCollider), typeof(CorpseLoot));
        go.transform.position = at;
        if (parent) go.transform.SetParent(parent);

        var col = go.GetComponent<BoxCollider>();
        col.isTrigger = true; col.size = new Vector3(0.8f, 1f, 0.8f); // easy to click

        var cl = go.GetComponent<CorpseLoot>();
        cl.profile = profile;
        cl.Generate(npcLevel);
        return cl;
    }

    public void Generate(int npcLevel)
    {
        _dieTime = Time.time + lootableSeconds;

        // Money
        dollars = profile ? profile.GetRandomMoney(npcLevel) : 0;

        // Items (max 3 so we fit under 4 slots with money)
        int rolls = profile ? Random.Range(profile.itemRolls.x, profile.itemRolls.y + 1) : 2;
        items = profile ? profile.RollItems(npcLevel, rolls) : new List<ItemInstance>();
        if (items.Count > 3) items.RemoveRange(3, items.Count - 3);
    }

    void Update()
    {
        if (Time.time >= _dieTime)
        {
            if (_openUI) _openUI.Close();
            Destroy(gameObject);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _hovering = true;
        if (lootCursor) Cursor.SetCursor(lootCursor, lootHotspot, CursorMode.Auto);
        else cursorOwner?.SetHoverCursor();
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

    void OpenLootUI()
    {
        if (!_openUI)
        {
            var canvas = FindAnyObjectByType<Canvas>();
            _openUI = Instantiate(lootUIPrefab, canvas.transform);
            _openUI.Bind(this);
        }
        _openUI.gameObject.SetActive(true);
        _openUI.transform.SetAsLastSibling();
    }

    public void OnLootAllTaken()
    {
        // Close and clean early once empty
        if (_openUI) _openUI.Close();
        Destroy(gameObject);
    }

    public bool IsEmpty => (dollars <= 0) && (items == null || items.Count == 0);
}
