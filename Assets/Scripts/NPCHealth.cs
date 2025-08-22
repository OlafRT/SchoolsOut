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

    [Header("Debug")]
    public int currentHP;

    private NPCAI ai;
    private bool isStunned;
    private float stunEnd;

    // Cache player refs to avoid repeated Find calls
    static GameObject cachedPlayerGO;
    static PlayerStats cachedPlayerStats;

    void Awake()
    {
        ai = GetComponent<NPCAI>();
        currentHP = maxHP;

        if (!cachedPlayerGO) cachedPlayerGO = GameObject.FindWithTag("Player");
        if (cachedPlayerGO && !cachedPlayerStats) cachedPlayerStats = cachedPlayerGO.GetComponent<PlayerStats>();
    }

    public void ApplyDamage(int amount)
    {
        if (amount <= 0) return;

        // Ignore damage ONLY if friendly to the PLAYER (per relations)
        if (invulnerableIfFriendly && ai && PlayerIsFriendlyToThisNPC())
            return;

        currentHP = Mathf.Max(0, currentHP - amount);

        // Aggro this NPC + alert same-faction allies to attack the PLAYER
        if (ai)
        {
            ai.OnDamagedByPlayer(); // sets manual hostile + leash targeting player and alerts allies
        }

        if (currentHP == 0)
        {
            AwardXP();
            if (ai) ai.enabled = false;
            var col = GetComponent<Collider>(); if (col) col.enabled = false;
            Destroy(gameObject, 2f);
        }
    }

    bool PlayerIsFriendlyToThisNPC()
    {
        var player = GameObject.FindWithTag("Player");
        if (!player || !ai) return false;
        var stats = player.GetComponent<PlayerStats>();
        if (!stats) return false;

        NPCFaction pf = (stats.playerClass == PlayerStats.PlayerClass.Jock) ? NPCFaction.Jock : NPCFaction.Nerd;
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
        if (seconds <= 0f) return;
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