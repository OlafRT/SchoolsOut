using System.Collections.Generic;
using UnityEngine;

public class PlayerTimedBuffs : MonoBehaviour
{
    /// <summary>Read-only snapshot of a single active buff for UI consumption.</summary>
    public struct BuffInfo
    {
        public BuffStat stat;
        public float    remaining;   // seconds left
        public float    duration;    // original total duration
    }

    class Buff
    {
        public BuffStat stat;
        public int amount;
        public float endTime;
        public float duration;   // stored so UI can compute fill %
    }

    PlayerStats stats;
    readonly List<Buff> active = new();

    /// <summary>Current active buffs — read by BuffBarUI every frame.</summary>

    // Public accessor so BuffBarUI can read what it needs without reflection
    public BuffInfo GetInfo(int i)
    {
        var b = active[i];
        return new BuffInfo
        {
            stat      = b.stat,
            remaining = Mathf.Max(0f, b.endTime - Time.time),
            duration  = b.duration
        };
    }
    public int ActiveCount => active.Count;

    void Awake()
    {
        stats = GetComponent<PlayerStats>();
    }

    public void ApplyBuff(BuffStat stat, int amount, float durationSeconds)
    {
        if (!stats) return;

        amount = Mathf.Max(0, amount);
        float dur = Mathf.Max(0.1f, durationSeconds);

        // apply immediately
        Add(stat, amount);

        active.Add(new Buff
        {
            stat     = stat,
            amount   = amount,
            endTime  = Time.time + dur,
            duration = dur
        });

        stats.RaiseStatsChanged();
    }

    void Update()
    {
        if (active.Count == 0 || !stats) return;

        float now = Time.time;
        for (int i = active.Count - 1; i >= 0; i--)
        {
            if (now >= active[i].endTime)
            {
                Remove(active[i].stat, active[i].amount);
                active.RemoveAt(i);
                stats.RaiseStatsChanged();
            }
        }
    }

    void Add(BuffStat stat, int amt)
    {
        switch (stat)
        {
            case BuffStat.Muscles: stats.muscles += amt; break;
            case BuffStat.IQ: stats.iq += amt; break;
            case BuffStat.Toughness: stats.toughness += amt; break;
            case BuffStat.CritChance: stats.critChance = Mathf.Clamp01(stats.critChance + (amt / 100f)); break;
        }
    }

    void Remove(BuffStat stat, int amt)
    {
        switch (stat)
        {
            case BuffStat.Muscles: stats.muscles -= amt; break;
            case BuffStat.IQ: stats.iq -= amt; break;
            case BuffStat.Toughness: stats.toughness -= amt; break;
            case BuffStat.CritChance: stats.critChance = Mathf.Clamp01(stats.critChance - (amt / 100f)); break;
        }
    }
}