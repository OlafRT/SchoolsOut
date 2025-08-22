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

        // --- Correct friendliness gate: check relation TOWARD PLAYER, not CurrentHostility ---
        if (invulnerableIfFriendly && ai && cachedPlayerStats)
        {
            NPCFaction playerFaction = (cachedPlayerStats.playerClass == PlayerStats.PlayerClass.Jock)
                ? NPCFaction.Jock
                : NPCFaction.Nerd;

            var relationToPlayer = FactionRelations.GetRelation(ai.faction, playerFaction);

            // Only ignore damage if this NPC is explicitly Friendly to the PLAYER.
            if (relationToPlayer == NPCAI.Hostility.Friendly)
                return;
        }

        // Apply damage
        currentHP = Mathf.Max(0, currentHP - amount);

        // If Neutral toward player before the hit, force a manual hostile override so they fight back.
        if (ai && ai.CurrentHostility == NPCAI.Hostility.Neutral)
            ai.BecomeHostile();

        if (currentHP == 0)
        {
            AwardXP();
            // Simple death: disable AI & collider
            if (ai) ai.enabled = false;
            var col = GetComponent<Collider>(); if (col) col.enabled = false;
            // Optional: VFX/animation here
            Destroy(gameObject, 2f);
        }
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