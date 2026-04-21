using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The Principal boss. Uses NPCAI + NPCAutoAttack for chasing and attacking,
/// then triggers special mechanics every 5% HP lost.
///
/// REQUIRED ON SAME GAMEOBJECT:
///   NPCHealth, NPCAI, NPCMovement, NPCAutoAttack
///
/// SPECIAL MECHANICS (every 5% HP threshold crossed):
///   > 70% HP  → 1 gas emitter
///   > 50% HP  → 2 gas emitters
///   > 25% HP  → 3 gas emitters
///   ≤ 25% HP  → all 5 gas emitters
///
/// GAS EMITTERS: hidden in floor, rise up, apply mind control on prolonged contact.
/// DIVING BOARD: periodically summons NPC minions who bounce off the board.
/// </summary>
[DisallowMultipleComponent]
public class PrincipalBoss : MonoBehaviour
{
    [Header("Core Refs")]
    public NPCHealth health;
    public BossHealthBar bossHealthBar;

    [Header("Gas Emitters (assign all 5 in scene)")]
    [Tooltip("All GasEmitter objects in the room floor — assign up to 5.")]
    public GasEmitter[] gasEmitters;

    [Header("Gas Phase Thresholds")]
    [Tooltip("Above this HP fraction only 1 emitter activates per trigger.")]
    public float phase1Threshold = 0.70f;   // 1 emitter
    [Tooltip("Below this HP fraction 2 emitters activate.")]
    public float phase2Threshold = 0.50f;   // 2 emitters
    [Tooltip("Below this HP fraction 3 emitters activate.")]
    public float phase3Threshold = 0.25f;   // 3 emitters
    // Below phase3Threshold → all 5

    [Header("Mind Control Hit")]
    [Tooltip("Animator trigger fired when the boss hits a mind-controlled player.")]
    public string animMindControlHit = "MeleeHit";
    [Tooltip("Flat damage dealt when the mind-controlled player reaches the boss.")]
    public int mindControlHitDamage = 20;

    [Header("Diving Board Minion Spawner")]
    public DivingBoardSequence divingBoard;
    [Tooltip("Minimum seconds between diving board summons.")]
    public float divingBoardCooldownMin = 15f;
    [Tooltip("Maximum seconds between diving board summons.")]
    public float divingBoardCooldownMax = 30f;
    [Tooltip("Seconds after fight starts before first diving board summon.")]
    public float divingBoardFirstDelay = 20f;

    [Header("Boss Bar")]
    public string bossDisplayName = "The Principal";

    [Header("On Death")]
    [Tooltip("Optional GameObject to enable when the boss dies (e.g. a door, reward chest, or exit trigger).")]
    public GameObject onDeathEnableObject;

    // ── Internal ──────────────────────────────
    Animator _animator;
    int _lastHpPercent = 100;   // last HP percentage we checked (integer)
    bool _fightActive;

    public bool IsDead => health && health.IsDead;

    // ──────────────────────────────────────────
    void Awake()
    {
        if (!health) health = GetComponent<NPCHealth>();
        _animator = GetComponentInChildren<Animator>();
    }

    // ──────────────────────────────────────────
    /// <summary>Called by BossTriggerZone when the player enters the room.</summary>
    public void ActivateFight()
    {
        if (_fightActive) return;
        _fightActive = true;

        if (bossHealthBar)
        {
            bossHealthBar.bossDisplayName = bossDisplayName;
            bossHealthBar.Show(health);
        }

        StartCoroutine(HealthWatchLoop());
        StartCoroutine(DivingBoardLoop());
    }

    // ──────────────────────────────────────────
    //   HEALTH MILESTONE WATCH
    // ──────────────────────────────────────────
    IEnumerator HealthWatchLoop()
    {
        _lastHpPercent = 100;

        while (!IsDead)
        {
            yield return new WaitForSeconds(0.2f);

            if (!health) continue;
            int currentPercent = Mathf.FloorToInt((float)health.currentHP / health.maxHP * 100f);

            // Fire every time we cross a 5% boundary downward
            int crossedFrom = _lastHpPercent;
            int crossedTo   = currentPercent;

            if (crossedTo < crossedFrom)
            {
                // Check each 5% step between old and new value
                for (int thresh = crossedFrom - (crossedFrom % 5 == 0 ? 5 : crossedFrom % 5);
                     thresh >= crossedTo; thresh -= 5)
                {
                    if (thresh < crossedFrom && thresh >= crossedTo && thresh > 0)
                        TriggerGasEmitters();
                }
            }

            _lastHpPercent = currentPercent;
        }

        // Boss died
        OnBossDeath();
    }

    // ──────────────────────────────────────────
    //   BOSS DEATH
    // ──────────────────────────────────────────
    void OnBossDeath()
    {
        if (bossHealthBar) bossHealthBar.Hide();

        if (onDeathEnableObject != null)
            onDeathEnableObject.SetActive(true);
    }

    // ──────────────────────────────────────────
    //   GAS EMITTER ACTIVATION
    // ──────────────────────────────────────────
    void TriggerGasEmitters()
    {
        if (gasEmitters == null || gasEmitters.Length == 0) return;

        int count = EmitterCountForCurrentHP();
        if (count <= 0) return;

        // Shuffle to pick randomly
        var available = new List<GasEmitter>(gasEmitters);
        Shuffle(available);

        int activated = 0;
        foreach (var emitter in available)
        {
            if (!emitter || activated >= count) break;
            emitter.Activate(this);
            activated++;
        }
    }

    int EmitterCountForCurrentHP()
    {
        if (!health) return 1;
        float frac = (float)health.currentHP / health.maxHP;

        if (frac > phase1Threshold)   return 1;
        if (frac > phase2Threshold)   return 2;
        if (frac > phase3Threshold)   return 3;
        return gasEmitters != null ? gasEmitters.Length : 5;
    }

    static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // ──────────────────────────────────────────
    //   MIND CONTROL HIT
    // ──────────────────────────────────────────
    /// <summary>Called by MindControlEffect when the gas trick works — boss laughs.</summary>
    public void OnMindControlLand()
    {
        if (_animator && !string.IsNullOrEmpty(animMindControlHit))
            _animator.SetTrigger("Laugh"); // reuse laugh trigger or add a separate one
    }

    /// <summary>Called by MindControlEffect when the player reaches the boss.</summary>
    public void HitMindControlledPlayer(GameObject player)
    {
        if (_animator && !string.IsNullOrEmpty(animMindControlHit))
            _animator.SetTrigger(animMindControlHit);

        var dmg = player.GetComponent<IDamageable>();
        if (dmg == null) dmg = player.GetComponentInParent<IDamageable>();
        if (mindControlHitDamage > 0) dmg?.ApplyDamage(mindControlHitDamage);

        if (CombatTextManager.Instance)
        {
            Vector3 pos = player.transform.position + Vector3.up * 1.5f;
            CombatTextManager.Instance.ShowDamage(pos, mindControlHitDamage, false, player.transform);
        }
    }

    // ──────────────────────────────────────────
    //   DIVING BOARD LOOP
    // ──────────────────────────────────────────
    IEnumerator DivingBoardLoop()
    {
        yield return new WaitForSeconds(divingBoardFirstDelay);

        while (!IsDead)
        {
            if (divingBoard) divingBoard.Trigger();
            float cooldown = Random.Range(divingBoardCooldownMin, divingBoardCooldownMax);
            yield return new WaitForSeconds(cooldown);
        }
    }

    // ──────────────────────────────────────────
    void Update()
    {
        if (_fightActive && IsDead)
        {
            _fightActive = false;
            StopAllCoroutines();
            if (bossHealthBar) bossHealthBar.Hide();
        }
    }
}