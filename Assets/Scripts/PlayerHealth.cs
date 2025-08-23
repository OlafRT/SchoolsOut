using UnityEngine;
using System;

[DisallowMultipleComponent]
public class PlayerHealth : MonoBehaviour, IDamageable
{
    public PlayerStats stats;
    public int currentHP;

    public bool IsDead { get; private set; }

    // Events
    public event Action<int,int> OnDamaged;  // (newHP, delta)
    public event Action<int> OnHealed;       // amount
    public event Action OnDied;

    void Awake()
    {
        if (!stats) stats = GetComponent<PlayerStats>();
        currentHP = stats ? stats.MaxHP : 100;
        if (stats) stats.OnStatsChanged += RecomputeMaxHP;
        NotifyHUD(); // initial
    }

    void OnDestroy()
    {
        if (stats) stats.OnStatsChanged -= RecomputeMaxHP;
    }

    void RecomputeMaxHP()
    {
        int newMax = stats.MaxHP;
        currentHP = Mathf.Min(currentHP, newMax);
        NotifyHUD();
    }

    public void ApplyDamage(int amount)
    {
        if (IsDead) return;
        int dmg = Mathf.Max(0, amount);
        if (dmg == 0) return;

        currentHP = Mathf.Max(0, currentHP - dmg);
        OnDamaged?.Invoke(currentHP, dmg);

        // UI + FX
        PlayerHUD.TryFlashDamage();                 // red flash
        CameraShaker.Instance?.Shake(0.12f, 0.25f); // small hit shake
        NotifyHUD();

        if (currentHP <= 0)
        {
            Die();
        }
    }

    public void Heal(int amount)
    {
        if (IsDead) return;
        int heal = Mathf.Max(0, amount);
        if (heal == 0) return;

        int before = currentHP;
        currentHP = Mathf.Min(stats ? stats.MaxHP : 100, currentHP + heal);
        int gained = currentHP - before;
        if (gained > 0) OnHealed?.Invoke(gained);
        NotifyHUD();
    }

    void Die()
    {
        if (IsDead) return;
        IsDead = true;

        // Optional: disable movement/abilities here if you haven’t elsewhere
        var move = GetComponent<PlayerMovement>(); if (move) move.enabled = false;
        var abilities = GetComponent<PlayerAbilities>(); if (abilities) abilities.enabled = false;

        OnDied?.Invoke();
        PlayerHUD.TryShowDeathPanel();

        // keep currentHP at 0
        currentHP = 0;
        NotifyHUD();
    }

    void NotifyHUD()
    {
        PlayerHUD.TryUpdateHealth(currentHP, stats ? stats.MaxHP : 100);
    }
}