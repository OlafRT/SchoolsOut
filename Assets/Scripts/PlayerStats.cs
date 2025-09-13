using UnityEngine;
using System;
using System.Collections;

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
    public int muscles = 1;
    public int iq = 1;
    public int toughness = 5;
    [Range(0f, 1f)] public float critChance = 0.05f;

    [Header("HP From Toughness")]
    public int baseHP = 50;
    public int hpPerToughness = 10;

    [Header("Level Up FX")]
    public GameObject levelUpEffectPrefab;
    public AudioClip levelUpSfx;
    [Range(0f, 1f)] public float levelUpSfxVolume = 0.8f;
    public Vector3 levelUpEffectOffset = new Vector3(0f, 1.2f, 0f);
    public bool parentEffectToPlayer = true;
    public bool destroyOnParticlesEnd = true;
    public float levelUpEffectLifetime = 2.0f;
    public float effectTimeoutCap = 8f;

    [Header("Testing / Debug")]
    [Tooltip("Automatically trigger LevelUp() on scene start for testing.")]
    public bool simulateLevelUpOnStart = false;
    [Tooltip("Key to press at runtime to instantly level up.")]
    public KeyCode testLevelUpKey = KeyCode.F10;

    public int MaxHP => Mathf.Max(1, baseHP + toughness * hpPerToughness);

    public event Action OnStatsChanged;
    public event Action<int> OnLeveledUp;

    void Awake()
    {
        xpToNext = ComputeXpToNext(level);
    }

    void Start()
    {
        if (simulateLevelUpOnStart)
            LevelUp();
    }

    void Update()
    {
        if (Input.GetKeyDown(testLevelUpKey))
            LevelUp();
    }

    [ContextMenu("Test ▶ Level Up")]
    private void ContextTestLevelUp()
    {
        LevelUp();
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
        critChance = Mathf.Clamp01(critChance + 0.01f);

        xpToNext = ComputeXpToNext(level);

        // NEW: refill HP (and power, if you have a resource script—see note below)
        var health = GetComponent<PlayerHealth>();
        if (health) health.FullHeal();

        // OPTIONAL: if you have a power/mana component, mirror this:
        // GetComponent<PlayerPower>()?.FullRefill();

        // NEW: trigger the Level Up banner UI
        var levelUI = FindObjectOfType<QuestCompleteUI>();
        if (levelUI) levelUI.PlayLevelUp(level);

        // existing FX/SFX
        SpawnLevelUpEffect();

        OnLeveledUp?.Invoke(level);
        OnStatsChanged?.Invoke();
    }

    void SpawnLevelUpEffect()
    {
        if (levelUpEffectPrefab)
        {
            Vector3 pos = transform.position + levelUpEffectOffset;
            var fx = Instantiate(levelUpEffectPrefab, pos, Quaternion.identity);

            if (fx)
            {
                if (parentEffectToPlayer)
                    fx.transform.SetParent(transform, true);

                var pss = fx.GetComponentsInChildren<ParticleSystem>(true);
                foreach (var ps in pss)
                    if (ps && !ps.isPlaying) ps.Play();

                if (destroyOnParticlesEnd)
                    StartCoroutine(DestroyWhenParticlesDone(fx, pss, GetEffectiveTimeout()));
                else if (levelUpEffectLifetime > 0f)
                    Destroy(fx, levelUpEffectLifetime);
                else
                    StartCoroutine(DestroyWhenParticlesDone(fx, pss, GetEffectiveTimeout()));
            }
        }

        if (levelUpSfx)
            AudioSource.PlayClipAtPoint(levelUpSfx, transform.position, levelUpSfxVolume);
    }

    float GetEffectiveTimeout()
    {
        float t = Mathf.Max(levelUpEffectLifetime, 0f);
        if (t <= 0f) t = 4f;
        return Mathf.Min(t + 2f, Mathf.Max(2f, effectTimeoutCap));
    }

    IEnumerator DestroyWhenParticlesDone(GameObject fx, ParticleSystem[] systems, float timeout)
    {
        float elapsed = 0f;
        while (fx)
        {
            bool anyAlive = false;
            foreach (var ps in systems)
            {
                if (ps && ps.IsAlive(true)) { anyAlive = true; break; }
            }
            if (!anyAlive) break;

            elapsed += Time.unscaledDeltaTime;
            if (elapsed >= timeout) break;
            yield return null;
        }
        if (fx) Destroy(fx);
    }

    int ComputeXpToNext(int lvl)
    {
        float baseVal = 60f * lvl + 25f * lvl * lvl;
        return Mathf.Max(50, Mathf.RoundToInt(baseVal * Mathf.Max(0.1f, xpCurveMultiplier)));
    }

    public int ComputeDamage(int baseDamage, AbilitySchool school, bool allowCrit, out bool didCrit)
    {
        didCrit = false;
        if (baseDamage <= 0) return 0;

        float scaled = baseDamage;
        switch (school)
        {
            case AbilitySchool.Jock: scaled *= (1f + muscles * 0.05f); break;
            case AbilitySchool.Nerd: scaled *= (1f + iq * 0.05f); break;
        }

        if (allowCrit && UnityEngine.Random.value < critChance)
        {
            didCrit = true;
            scaled *= 2f;
        }

        return Mathf.Max(0, Mathf.RoundToInt(scaled));
    }

    public int ComputeDotDamage(int baseDamage, AbilitySchool school)
    {
        bool _;
        return ComputeDamage(baseDamage, school, false, out _);
    }
}

