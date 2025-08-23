using UnityEngine;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class BlockAbility : MonoBehaviour, IAbilityUI, IClassRestrictedAbility, IChargeableAbility
{
    [Header("Learned Gate")]
    public string blockAbilityName = "Block";

    [Header("Input")]
    public KeyCode blockKey = KeyCode.C;     // choose your hotkey

    [Header("Charges")]
    [Tooltip("How many blocks you can store.")]
    public int maxCharges = 3;
    [Tooltip("Seconds to recover one block.")]
    public float rechargeSeconds = 10f;

    [Header("Visuals / Anim")]
    public Sprite icon;
    public GameObject shieldVfxPrefab;   // optional: a looping vfx while active
    public string animRaiseTrigger = "RaiseShield";
    public string animLowerTrigger = "LowerShield";

    [Header("Advanced")]
    [Tooltip("If true, you can keep the stance without stacks (still turns off when you use any ability).")]
    public bool allowActiveWithZeroStacks = false;

    // Context
    private PlayerAbilities ctx;
    private Animator anim;
    private AutoAttackAbility autoAttack;

    // Runtime
    private int currentCharges;
    private float nextChargeReadyTime = 0f;
    private bool isActive;
    private GameObject vfxInstance;

    // cache all other ability keys to auto-cancel on use
    private readonly List<IAbilityUI> otherAbilities = new();

    public AbilityClassRestriction AllowedFor => AbilityClassRestriction.Jock;

    void Awake()
    {
        ctx = GetComponent<PlayerAbilities>();
        anim = GetComponentInChildren<Animator>();
        autoAttack = GetComponent<AutoAttackAbility>();

        // charges
        currentCharges = Mathf.Max(1, maxCharges);
        if (currentCharges < maxCharges)
            nextChargeReadyTime = Time.time + Mathf.Max(0.01f, rechargeSeconds);

        // discover other abilities to watch their keys
        GetComponents(otherAbilities);
        // remove self
        otherAbilities.RemoveAll(a => a == (IAbilityUI)this);
    }

    void Update()
    {
        if (!IsLearned) { if (isActive) Deactivate(); return; }

        TickRecharge();

        // Toggle on/off
        if (Input.GetKeyDown(blockKey))
        {
            if (isActive) Deactivate();
            else ActivateIfAllowed();
        }

        // If active, cancel the moment you press any other learned ability key
        if (isActive)
        {
            for (int i = 0; i < otherAbilities.Count; i++)
            {
                var a = otherAbilities[i];
                if (a == null || a.Key == KeyCode.None) continue;
                if (!ctx.HasAbility(a.AbilityName)) continue; // not learned

                if (Input.GetKeyDown(a.Key))
                {
                    Deactivate();
                    break;
                }
            }
        }
    }

    void ActivateIfAllowed()
    {
        if (currentCharges <= 0 && !allowActiveWithZeroStacks) return;

        isActive = true;
        if (autoAttack) autoAttack.IsSuppressedByOtherAbilities = true; // you can change this if you want auto attack to keep swinging

        if (shieldVfxPrefab && !vfxInstance)
        {
            vfxInstance = Instantiate(shieldVfxPrefab, transform);
        }
        if (anim && !string.IsNullOrEmpty(animRaiseTrigger))
            anim.SetTrigger(animRaiseTrigger);
    }

    void Deactivate()
    {
        isActive = false;
        if (autoAttack) autoAttack.IsSuppressedByOtherAbilities = false;

        if (vfxInstance) { Destroy(vfxInstance); vfxInstance = null; }
        if (anim && !string.IsNullOrEmpty(animLowerTrigger))
            anim.SetTrigger(animLowerTrigger);
    }

    void TickRecharge()
    {
        if (currentCharges >= Mathf.Max(1, maxCharges)) return;
        if (Time.time >= nextChargeReadyTime)
        {
            currentCharges = Mathf.Min(maxCharges, currentCharges + 1);
            if (currentCharges < maxCharges)
                nextChargeReadyTime = Time.time + Mathf.Max(0.01f, rechargeSeconds);
        }
    }

    void SpendCharge()
    {
        currentCharges = Mathf.Max(0, currentCharges - 1);
        if (currentCharges < maxCharges && nextChargeReadyTime < Time.time + 0.001f)
            nextChargeReadyTime = Time.time + Mathf.Max(0.01f, rechargeSeconds);

        // If you don’t want to keep stance with zero stacks, drop it now
        if (currentCharges == 0 && !allowActiveWithZeroStacks)
            Deactivate();
    }

    /// <summary>
    /// Called by PlayerHealth *before* applying damage. If true, damage is fully blocked.
    /// </summary>
    public bool TryBlockIncomingHit(out bool consumedStack)
    {
        consumedStack = false;
        if (!isActive) return false;
        if (currentCharges <= 0 && !allowActiveWithZeroStacks) return false;

        // We block!
        consumedStack = currentCharges > 0;
        if (consumedStack) SpendCharge();

        // (Optional: play a clang sound / quick flash on the shieldVfxInstance)
        return true;
    }

    // ------------- IAbilityUI -------------
    public string AbilityName => blockAbilityName;
    public Sprite Icon => icon;
    public KeyCode Key => blockKey;

    // We present recharging as a cooldown bar when empty
    public float CooldownRemaining => (currentCharges > 0) ? 0f : Mathf.Max(0f, nextChargeReadyTime - Time.time);
    public float CooldownDuration => Mathf.Max(0.01f, rechargeSeconds);
    public bool IsLearned => ctx && ctx.HasAbility(blockAbilityName);

    // ------------- IChargeableAbility -------------
    public int CurrentCharges => currentCharges;
    public int MaxCharges => Mathf.Max(1, maxCharges);
}
