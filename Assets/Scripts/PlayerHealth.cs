using UnityEngine;
using System;

[DisallowMultipleComponent]
public class PlayerHealth : MonoBehaviour, IDamageable
{
    public PlayerStats stats;
    public int currentHP;

    public bool IsDead { get; private set; }

    [Header("Regeneration")]
    public float baseRegenPerSecond = 0.2f;   // 1 HP / 5s
    public float regenMultiplier = 1f;        // talents can raise this (e.g., 1.5 = +50%)
    private float regenAccumulator = 0f;

    // Events
    public event Action<int,int> OnDamaged;  // (newHP, delta)
    public event Action<int> OnHealed;       // amount
    public event Action OnDied;

    private BlockAbility block;
    private AudioSource _audioSource;
    private Animator _animator;

    [Header("Hurt Sounds")]
    [Tooltip("One clip is picked at random each time the player takes damage. " +
             "Add as many variations as you like — more clips = less repetition.")]
    public AudioClip[] hurtSfx;
    [Range(0f, 1f)] public float hurtVolume = 0.8f;
    [Tooltip("Random pitch variation range. 0 = no variation, 0.15 = subtle.")]
    public float hurtPitchVariation = 0.12f;

    void Update()
    {
        if (IsDead || stats == null) return;

        float rate = baseRegenPerSecond * regenMultiplier;
        if (rate <= 0f) return;

        regenAccumulator += rate * Time.deltaTime;
        // Convert accumulated fractional regen into whole HP points
        int whole = Mathf.FloorToInt(regenAccumulator);
        if (whole > 0)
        {
            regenAccumulator -= whole;
            Heal(whole);
        }
    }

    void Awake()
    {
        if (!stats) stats = GetComponent<PlayerStats>();
        block = GetComponent<BlockAbility>();

        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.spatialBlend = 0f;  // 2D — heard at full volume regardless of position
        _audioSource.playOnAwake  = false;

        // Find animator on children (the model is a child of the player root)
        _animator = GetComponentInChildren<Animator>(true);

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

        // ---- BLOCK INTERCEPT ----
        if (block && block.IsLearned)
        {
            if (block.TryBlockIncomingHit(out bool consumed))
            {
                // Blocked! Maybe show a small "BLOCK" text:
                if (CombatTextManager.Instance)
                {
                    Vector3 pos = transform.position; pos.y += 1.6f;
                    CombatTextManager.Instance.ShowText(pos, "BLOCK", Color.cyan, transform, 0.6f, 32f);
                }
                // No damage, no shake/flash
                return;
            }
        }
        // -------------------------

        int dmg = Mathf.Max(0, amount);
        if (dmg == 0) return;

        currentHP = Mathf.Max(0, currentHP - dmg);
        OnDamaged?.Invoke(currentHP, dmg);

        // UI + FX
        PlayerHUD.TryFlashDamage();                 
        CameraShaker.Instance?.Shake(0.12f, 0.25f);
        PlayHurtSfx();
        TriggerHitAnimation();
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

    public void FullHeal()
    {
        if (stats == null) return;
        currentHP = stats.MaxHP;
        OnHealed?.Invoke(currentHP);  // notify listeners (amount semantics aren't strict here)
        NotifyHUD();
    }

    void Die()
    {
        if (IsDead) return;
        IsDead = true;

        var move = GetComponent<PlayerMovement>();
        if (move)
        {
            move.StopMovement(); // <-- kill the coroutine first
            move.enabled = false;
        }

        var abilities = GetComponent<PlayerAbilities>();
        if (abilities) abilities.enabled = false;

        OnDied?.Invoke();

        currentHP = 0;
        NotifyHUD();
    }

    void TriggerHitAnimation()
    {
        if (!_animator) return;
        // Alternate randomly between Hit1 and Hit2 so it doesn't look repetitive
        string trigger = UnityEngine.Random.value < 0.5f ? "Hit1" : "Hit2";
        _animator.SetTrigger(trigger);
    }

    void PlayHurtSfx()
    {
        if (hurtSfx == null || hurtSfx.Length == 0 || !_audioSource) return;

        // Pick a random clip, skipping any null slots the designer may have left empty.
        // Try up to the array length times so we don't loop forever on an all-null array.
        AudioClip clip = null;
        for (int i = 0; i < hurtSfx.Length; i++)
        {
            clip = hurtSfx[UnityEngine.Random.Range(0, hurtSfx.Length)];
            if (clip != null) break;
        }
        if (!clip) return;

        _audioSource.pitch = 1f + UnityEngine.Random.Range(-hurtPitchVariation, hurtPitchVariation);
        _audioSource.PlayOneShot(clip, hurtVolume);
    }

    void NotifyHUD() => PlayerHUD.TryUpdateHealth(currentHP, stats ? stats.MaxHP : 100);
}
