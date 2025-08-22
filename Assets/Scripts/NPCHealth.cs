using UnityEngine;
using System.Collections;

[DisallowMultipleComponent]
public class NPCHealth : MonoBehaviour, IDamageable, IStunnable
{
    [Header("Vitals")]
    public int maxHP = 100;
    public bool invulnerableIfFriendly = true;

    [Header("Rewards")]
    public int xpReward = 10; // XP given to the player when this NPC dies

    [Header("Debug")]
    public int currentHP;

    private NPCAI ai;
    private bool isStunned;
    private float stunEnd;

    void Awake()
    {
        ai = GetComponent<NPCAI>();
        currentHP = maxHP;
    }

    public void ApplyDamage(int amount)
    {
        if (amount <= 0) return;

        // If we are flagged invulnerable while friendly, ignore damage
        if (invulnerableIfFriendly && ai && ai.CurrentHostility == NPCAI.Hostility.Friendly)
            return;

        currentHP = Mathf.Max(0, currentHP - amount);

        // If we were neutral, being hit should aggro (manual override)
        if (ai && ai.CurrentHostility == NPCAI.Hostility.Neutral)
            ai.BecomeHostile();

        if (currentHP == 0)
        {
            // Award XP to player
            AwardXP();

            // Simple death: disable AI & collider
            if (ai) ai.enabled = false;
            var col = GetComponent<Collider>(); 
            if (col) col.enabled = false;

            // Optional: play death VFX/anim here
            Destroy(gameObject, 2f);
        }
    }

    void AwardXP()
    {
        // Find the player (tag is the easiest way, but you could cache a reference instead)
        var player = GameObject.FindWithTag("Player");
        if (!player) return;

        var stats = player.GetComponent<PlayerStats>();
        if (stats) stats.AddXP(xpReward);
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