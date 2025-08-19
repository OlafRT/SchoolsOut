using UnityEngine;
using System;

[DisallowMultipleComponent]
public class PlayerStats : MonoBehaviour
{
    public enum PlayerClass { Jock, Nerd }
    public enum AbilitySchool { Neutral, Jock, Nerd }

    [Header("Identity")]
    public PlayerClass playerClass = PlayerClass.Jock;
    public int level = 1;

    [Header("XP / Leveling")]
    public int currentXP = 0;
    public int xpToNext = 100;
    [Tooltip("Multiplies the base XP curve. Raise to make leveling slower.")]
    public float xpCurveMultiplier = 1.0f;

    [Header("Core Stats")]
    [Tooltip("Jock scaling stat (affects Jock abilities).")]
    public int muscles = 1;
    [Tooltip("Nerd scaling stat (affects Nerd abilities).")]
    public int iq = 1;
    [Tooltip("Affects HP.")]
    public int toughness = 5;
    [Tooltip("0..1 (e.g., 0.05 = 5%)")]
    [Range(0f, 1f)] public float critChance = 0.05f;

    [Header("HP From Toughness")]
    public int baseHP = 50;
    public int hpPerToughness = 10;

    public int MaxHP => Mathf.Max(1, baseHP + toughness * hpPerToughness);

    public event Action OnStatsChanged;
    public event Action<int> OnLeveledUp;

    void Awake()
    {
        xpToNext = ComputeXpToNext(level);
    }

    public void AddXP(int amount)
    {
        if (amount <= 0) return;
        currentXP += amount;
        while (currentXP >= xpToNext)
        {
            currentXP -= xpToNext;
            LevelUp();
        }
        OnStatsChanged?.Invoke();
    }

    void LevelUp()
    {
        level++;

        if (playerClass == PlayerClass.Jock) muscles += 1;
        else iq += 1;

        toughness += 1;
        critChance = Mathf.Clamp01(critChance + 0.01f); // +1%

        xpToNext = ComputeXpToNext(level);

        OnLeveledUp?.Invoke(level);
        OnStatsChanged?.Invoke();
    }

    int ComputeXpToNext(int lvl)
    {
        // Smooth-ish curve; tweak to taste
        float baseVal = 60f * lvl + 25f * lvl * lvl;
        return Mathf.Max(50, Mathf.RoundToInt(baseVal * Mathf.Max(0.1f, xpCurveMultiplier)));
    }

    // ------ Damage helpers ------

    /// <summary>Compute final damage for a hit. WoW-style crit doubles.</summary>
    public int ComputeDamage(int baseDamage, AbilitySchool school, bool allowCrit, out bool didCrit)
    {
        didCrit = false;
        if (baseDamage <= 0) return 0;

        float scaled = baseDamage;
        switch (school)
        {
            case AbilitySchool.Jock: scaled *= (1f + muscles * 0.05f); break; // +5% per Muscles
            case AbilitySchool.Nerd: scaled *= (1f + iq * 0.05f);      break; // +5% per IQ
            default: break;
        }

        if (allowCrit && UnityEngine.Random.value < critChance)
        {
            didCrit = true;
            scaled *= 2f;
        }

        return Mathf.Max(0, Mathf.RoundToInt(scaled));
    }

    /// <summary>No crits (for DoTs/ticks).</summary>
    public int ComputeDotDamage(int baseDamage, AbilitySchool school)
    {
        bool _;
        return ComputeDamage(baseDamage, school, false, out _);
    }
}