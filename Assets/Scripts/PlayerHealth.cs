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
    private AutoAttackAbility _autoAttack;

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

    // True on a fresh start — heals the player to full on the first RecomputeMaxHP()
    // call (which fires when the equipment bridge applies bonuses in OnEnable).
    // This ensures fresh-game HP accounts for any toughness equipment the player has.
    private bool _healToFullOnNextStatsChange;

    void Awake()
    {
        if (!stats) stats = GetComponent<PlayerStats>();
        block = GetComponent<BlockAbility>();
        _autoAttack = GetComponent<AutoAttackAbility>();

        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.spatialBlend = 0f;  // 2D — heard at full volume regardless of position
        _audioSource.playOnAwake  = false;

        // Find animator on children (the model is a child of the player root)
        _animator = GetComponentInChildren<Animator>(true);

        int savedHP = (stats?.progressAsset != null && stats.progressAsset.hasData)
            ? stats.progressAsset.currentHP : 0;

        if (savedHP > 0)
        {
            // Use the saved value directly — do NOT clamp against stats.MaxHP here
            // because the equipment bridge hasn't run yet (it fires in OnEnable, which
            // comes after Awake). stats.MaxHP is base-only at this point, so clamping
            // would silently reduce HP (e.g. savedHP=130, base MaxHP=120 → clamps to 120).
            // RecomputeMaxHP() will clamp correctly once the bridge applies equipment.
            currentHP = savedHP;
        }
        else
        {
            // No saved data — fresh game. Use base MaxHP as a visible placeholder
            // and set the flag so that the first RecomputeMaxHP() (fired by the bridge
            // in OnEnable) heals to the correct full MaxHP including equipment bonuses.
            currentHP = stats ? stats.MaxHP : 100;
            _healToFullOnNextStatsChange = true;
        }

        if (stats) stats.OnStatsChanged += RecomputeMaxHP;
        NotifyHUD(); // initial
    }

    void OnDestroy()
    {
        if (stats) stats.OnStatsChanged -= RecomputeMaxHP;
    }

    void RecomputeMaxHP()
    {
        if (stats == null) return;
        int newMax = stats.MaxHP;

        if (_healToFullOnNextStatsChange)
        {
            // First call after bridge applied equipment bonuses — heal to true full MaxHP.
            _healToFullOnNextStatsChange = false;
            currentHP = newMax;
        }
        else
        {
            // Normal case: clamp HP if MaxHP decreased (e.g. unequipped toughness gear).
            // Does not auto-heal if MaxHP increased — HP increase from equipping gear
            // shouldn't count as a free heal.
            currentHP = Mathf.Min(currentHP, newMax);
        }
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
        SyncHPToAsset();
        OnDamaged?.Invoke(currentHP, dmg);

        // Push back the auto-attack swing timer if a swing animation is in progress
        _autoAttack?.ApplyAttackPushback();

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
        SyncHPToAsset();
        NotifyHUD();
    }

    public void FullHeal()
    {
        if (stats == null) return;
        currentHP = stats.MaxHP;
        OnHealed?.Invoke(currentHP);  // notify listeners (amount semantics aren't strict here)
        SyncHPToAsset();
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

    // Keeps the progress asset's currentHP in sync so scene transitions preserve HP.
    // Called after every HP change — cheap enough to not need batching.
    void SyncHPToAsset()
    {
        if (stats?.progressAsset != null)
            stats.progressAsset.currentHP = currentHP;
    }

    void NotifyHUD() => PlayerHUD.TryUpdateHealth(currentHP, stats ? stats.MaxHP : 100);
}
