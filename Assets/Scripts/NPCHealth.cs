using UnityEngine;
using System.Collections;

[DisallowMultipleComponent]
public class NPCHealth : MonoBehaviour, IDamageable, IStunnable
{
    [Header("Vitals")]
    public int maxHP = 100;

    [Tooltip("If true, this NPC ignores damage ONLY when it is Friendly TOWARD THE PLAYER (per FactionRelations).")]
    public bool invulnerableIfFriendly = true;

    [Header("Rewards")]
    public int xpReward = 10; // XP given to the player when this NPC dies

    [Header("Loot")]
    public NPCLootProfile lootProfile;
    [Range(1,30)] public int npcLevel = 1;
    public float corpseDespawnSeconds = 20f;

    [Header("Loot UI / Cursor (optional)")]
    public LootUI lootUIPrefab;               // small 4-slot vertical UI prefab
    public EquipmentManager cursorOwner;      // for hand/grab cursor helpers
    public Texture2D lootCursor;              // optional custom loot cursor
    public Vector2 lootCursorHotspot = new Vector2(8, 2);

    [Header("Corpse Click Box")]
    [Tooltip("Approx height of the clickable trigger box (centered above ground).")]
    public float corpseClickableHeight = 1.0f;

    [Header("Debug")]
    public int currentHP;

    // runtime
    NPCAI ai;
    bool isStunned;
    float stunEnd;
    bool isDead;

    // Cache player refs to avoid repeated Find calls
    static GameObject cachedPlayerGO;
    static PlayerStats cachedPlayerStats;

    void Awake()
    {
        ai = GetComponent<NPCAI>();
        currentHP = maxHP;

        if (!cachedPlayerGO)    cachedPlayerGO = GameObject.FindWithTag("Player");
        if (cachedPlayerGO && !cachedPlayerStats)
            cachedPlayerStats = cachedPlayerGO.GetComponent<PlayerStats>();
    }

    public void ApplyDamage(int amount)
    {
        if (amount <= 0 || isDead) return;

        // Ignore damage ONLY if friendly to the PLAYER (per relations)
        if (invulnerableIfFriendly && ai && PlayerIsFriendlyToThisNPC())
            return;

        currentHP = Mathf.Max(0, currentHP - amount);

        // Aggro this NPC + alert same-faction allies to attack the PLAYER
        if (ai) ai.OnDamagedByPlayer();

        if (currentHP == 0)
        {
            HandleDeath();
        }
    }

    void HandleDeath()
    {
        if (isDead) return;
        isDead = true;

        AwardXP();

        // Stop AI & movement, keep the mesh/animator posing as corpse
        if (ai) ai.enabled = false;
        var move = GetComponent<NPCMovement>(); if (move) move.enabled = false;
#if UNITY_AI_PRESENT
        var nav = GetComponent<UnityEngine.AI.NavMeshAgent>(); if (nav) nav.enabled = false;
#endif

        // Ensure a trigger collider exists for pointer interaction
        var col = GetComponent<Collider>();
        if (!col) col = gameObject.AddComponent<BoxCollider>();
        col.isTrigger = true;

        // If it's a BoxCollider, size it for easy clicking
        var box = col as BoxCollider;
        if (box)
        {
            box.center = new Vector3(0f, corpseClickableHeight * 0.5f, 0f);
            box.size   = new Vector3(0.8f, corpseClickableHeight, 0.8f);
        }

        // Add/reuse CorpseLoot ON THIS GAMEOBJECT
        var corpse = GetComponent<CorpseLoot>();
        if (!corpse) corpse = gameObject.AddComponent<CorpseLoot>();

        // Wire references
        corpse.profile         = lootProfile;
        corpse.lootableSeconds = corpseDespawnSeconds;
        corpse.lootUIPrefab    = lootUIPrefab;
        corpse.cursorOwner     = cursorOwner;
        corpse.lootCursor      = lootCursor;
        corpse.lootHotspot     = lootCursorHotspot;

        // Roll loot using NPC level (clamped to 1..30)
        corpse.Generate(Mathf.Clamp(npcLevel, 1, 30));

        // Important: do NOT Destroy(this.gameObject) here; CorpseLoot will despawn after loot is taken or timer ends.
        // If you want to stop further damage processing, you can just return; isDead prevents multiple HandleDeath calls.
    }

    bool PlayerIsFriendlyToThisNPC()
    {
        var player = cachedPlayerGO;
        if (!player || !ai) return false;
        var stats = cachedPlayerStats;
        if (!stats) return false;

        NPCFaction pf = (stats.playerClass == PlayerStats.PlayerClass.Jock)
            ? NPCFaction.Jock
            : NPCFaction.Nerd;

        var relation = FactionRelations.GetRelation(ai.faction, pf);
        return (relation == NPCAI.Hostility.Friendly);
    }

    void AwardXP()
    {
        if (!cachedPlayerStats) return;
        cachedPlayerStats.AddXP(xpReward);
    }

    public void ApplyStun(float seconds)
    {
        if (seconds <= 0f || isDead) return;
        isStunned = true;
        stunEnd = Time.time + seconds;
        if (ai) ai.HardStop();
        StartCoroutine(StunTimer());
    }

    IEnumerator StunTimer()
    {
        while (Time.time < stunEnd) yield return null;
        isStunned = false;
    }

    public bool IsStunned => isStunned;
}
