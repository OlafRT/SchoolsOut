using UnityEngine;

[DisallowMultipleComponent]
public class PlayerHealth : MonoBehaviour, IDamageable
{
    public PlayerStats stats;
    public int currentHP;

    void Awake()
    {
        if (!stats) stats = GetComponent<PlayerStats>();
        currentHP = stats ? stats.MaxHP : 100;
        if (stats) stats.OnStatsChanged += RecomputeMaxHP;
    }

    void OnDestroy()
    {
        if (stats) stats.OnStatsChanged -= RecomputeMaxHP;
    }

    void RecomputeMaxHP()
    {
        int newMax = stats.MaxHP;
        currentHP = Mathf.Min(currentHP, newMax);
    }

    public void ApplyDamage(int amount)
    {
        currentHP -= Mathf.Max(0, amount);
        if (currentHP <= 0)
        {
            currentHP = 0;
            // TODO: death / respawn
        }
    }
}