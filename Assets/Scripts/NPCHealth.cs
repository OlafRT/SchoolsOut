using UnityEngine;
using System.Collections;

[DisallowMultipleComponent]
public class NPCHealth : MonoBehaviour, IDamageable, IStunnable
{
    [Header("Vitals")]
    public int maxHP = 100;
    public bool invulnerableIfFriendly = true;

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
        if (invulnerableIfFriendly && ai && ai.hostility == NPCAI.Hostility.Friendly) return;

        currentHP = Mathf.Max(0, currentHP - amount);

        // If neutral, being hit turns hostile to the attacker’s faction (simple version)
        if (ai && ai.hostility == NPCAI.Hostility.Neutral)
            ai.BecomeHostile();

        if (currentHP == 0)
        {
            // Simple death: disable AI & collider
            if (ai) ai.enabled = false;
            var col = GetComponent<Collider>(); if (col) col.enabled = false;
            // Optional: play death VFX/anim here
            Destroy(gameObject, 2f);
        }
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
