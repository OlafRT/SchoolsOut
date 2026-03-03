using System.Collections.Generic;
using UnityEngine;

public class PlayerTimedBuffs : MonoBehaviour
{
    class Buff
    {
        public BuffStat stat;
        public int amount;
        public float endTime;
    }

    PlayerStats stats;
    readonly List<Buff> active = new();

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
            stat = stat,
            amount = amount,
            endTime = Time.time + dur
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