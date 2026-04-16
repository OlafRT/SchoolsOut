using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A small slime puddle that damages and slows anything standing on it.
/// Spawned automatically by SlimeTrailEffect on slime minions.
///
/// PREFAB SETUP:
///   - Add a flat trigger Collider (BoxCollider or CapsuleCollider) — roughly 1 tile wide, very thin.
///   - Optionally add a particle system / sprite for the visual.
///   - SlimePuddle is added/Init'd programmatically, no scene setup needed.
/// </summary>
[DisallowMultipleComponent]
public class SlimePuddle : MonoBehaviour
{
    [Header("Defaults (overridden by Init)")]
    public float duration          = 6f;
    public int   tickDamage        = 5;
    public float tickInterval      = 0.5f;
    [Range(0f, 0.95f)]
    public float slowPercent       = 0.30f;

    // Track slow sources separately for correct aura ref-counts
    static int s_playerSlowCount = 0;
    bool weAffectingPlayer = false;

    readonly HashSet<Collider>      inside      = new();
    readonly HashSet<NPCMovement>   npcInside   = new();
    readonly HashSet<PlayerMovement> playerInside = new();

    bool initialised = false;

    // ───────────────────────────────────────────
    /// <summary>Call this immediately after spawning the prefab.</summary>
    public void Init(float dur, int dmg, float interval, float slow)
    {
        duration     = dur;
        tickDamage   = dmg;
        tickInterval = interval;
        slowPercent  = Mathf.Clamp01(slow);

        initialised = true;
        StartCoroutine(Lifetime());
        StartCoroutine(TickLoop());
    }

    void Start()
    {
        // If Init wasn't called (e.g., placed in scene directly), boot with defaults.
        if (!initialised)
        {
            initialised = true;
            StartCoroutine(Lifetime());
            StartCoroutine(TickLoop());
        }
    }

    // ───────────────────────────────────────────
    IEnumerator Lifetime()
    {
        yield return new WaitForSeconds(duration);
        Cleanup();
    }

    IEnumerator TickLoop()
    {
        float end = Time.time + duration;
        while (Time.time < end)
        {
            yield return new WaitForSeconds(tickInterval);

            // Snapshot so damage callbacks can't mutate the set mid-iteration
            Collider[] snap = new Collider[inside.Count];
            inside.CopyTo(snap);

            foreach (var col in snap)
            {
                if (!col) { inside.Remove(col); continue; }

                IDamageable dmg = null;
                col.TryGetComponent(out dmg);
                if (dmg == null) dmg = col.GetComponentInParent<IDamageable>();
                dmg?.ApplyDamage(tickDamage);

                if (dmg != null && CombatTextManager.Instance)
                {
                    Vector3 pos = col.bounds.center;
                    pos.y = col.bounds.max.y;
                    CombatTextManager.Instance.ShowDamage(pos, tickDamage, false, col.transform, lifetimeOverride: 0.5f);
                }
            }
        }
    }

    // ───────────────────────────────────────────
    void OnTriggerEnter(Collider other)
    {
        inside.Add(other);

        // Apply slow aura to NPCs
        NPCMovement npc = null;
        other.TryGetComponent(out npc);
        if (npc == null) npc = other.GetComponentInParent<NPCMovement>();
        if (npc != null && !npcInside.Contains(npc))
        {
            npcInside.Add(npc);
            npc.SetAuraMultiplier(this, 1f - slowPercent);
            var host = npc.GetComponent<NPCStatusHost>();
            if (host) host.AddOrRefreshAura("Slow", this, null);
        }

        // Apply slow aura to Player
        PlayerMovement pm = null;
        other.TryGetComponent(out pm);
        if (pm == null) pm = other.GetComponentInParent<PlayerMovement>();
        if (pm != null && !playerInside.Contains(pm))
        {
            playerInside.Add(pm);
            pm.SetAuraMultiplier(this, 1f - slowPercent);

            if (!weAffectingPlayer)
            {
                weAffectingPlayer = true;
                s_playerSlowCount++;
                PlayerHUD.SetSlowed(true);
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        inside.Remove(other);

        // Remove NPC slow
        NPCMovement npc = null;
        other.TryGetComponent(out npc);
        if (npc == null) npc = other.GetComponentInParent<NPCMovement>();
        if (npc != null && npcInside.Remove(npc))
        {
            npc.ClearAura(this);
            var host = npc.GetComponent<NPCStatusHost>();
            if (host) host.RemoveAura("Slow", this);
        }

        // Remove Player slow
        PlayerMovement pm = null;
        other.TryGetComponent(out pm);
        if (pm == null) pm = other.GetComponentInParent<PlayerMovement>();
        if (pm != null && playerInside.Remove(pm))
        {
            pm.ClearAura(this);
            if (weAffectingPlayer)
            {
                weAffectingPlayer = false;
                s_playerSlowCount = Mathf.Max(0, s_playerSlowCount - 1);
                PlayerHUD.SetSlowed(s_playerSlowCount > 0);
            }
        }
    }

    // ───────────────────────────────────────────
    void Cleanup()
    {
        foreach (var npc in npcInside)
        {
            if (npc) { npc.ClearAura(this); }
            var host = npc ? npc.GetComponent<NPCStatusHost>() : null;
            if (host) host.RemoveAura("Slow", this);
        }
        foreach (var pm in playerInside)
            if (pm) pm.ClearAura(this);

        if (weAffectingPlayer)
        {
            weAffectingPlayer = false;
            s_playerSlowCount = Mathf.Max(0, s_playerSlowCount - 1);
            PlayerHUD.SetSlowed(s_playerSlowCount > 0);
        }

        Destroy(gameObject);
    }
}
