using UnityEngine;
using System.Collections;

[DisallowMultipleComponent]
public class WarpAbility : MonoBehaviour, IAbilityUI, IClassRestrictedAbility, IChargeableAbility
{
    [Header("Learned Gate")]
    public string warpAbilityName = "Warp";

    [Header("Input")]
    public KeyCode warpKey = KeyCode.F;

    [Header("Warp Settings")]
    public int warpDistanceTiles = 8;
    public float warpVfxDuration = 1.5f;
    public GameObject warpVfxPrefab;

    [Header("Charges")]
    public int maxCharges = 2;
    public float rechargeSeconds = 10f;

    [Header("UI")]
    public Sprite icon;

    private PlayerAbilities ctx;
    private AutoAttackAbility autoAttack;

    private int currentCharges;
    private float nextChargeReadyTime = 0f;

    public AbilityClassRestriction AllowedFor => AbilityClassRestriction.Nerd;

    void Awake()
    {
        ctx = GetComponent<PlayerAbilities>();
        autoAttack = GetComponent<AutoAttackAbility>();
        currentCharges = Mathf.Max(1, maxCharges);
        if (currentCharges < maxCharges)
            nextChargeReadyTime = Time.time + Mathf.Max(0.01f, rechargeSeconds);
    }

    void Update()
    {
        if (!IsLearned) return;
        TickRecharge();

        if (currentCharges > 0 && Input.GetKeyDown(warpKey))
        {
            SpendCharge();
            DoWarp();
        }
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
    }

    void DoWarp()
    {
        Vector3 dir8 = ctx.SnapDirTo8(transform.forward);
        var (sx, sz) = ctx.StepFromDir8(dir8);
        if (sx == 0 && sz == 0) return;

        Vector3 stepDir   = new Vector3(sx, 0f, sz).normalized;
        float   tileSize  = ctx.tileSize;
        Vector3 startTile = ctx.Snap(transform.position);

        int wallMask = ctx.wallLayer.value;

        // step forward up to N tiles; stop BEFORE first blocked tile
        Vector3 lastFree = startTile;
        for (int step = 0; step < warpDistanceTiles; step++)
        {
            Vector3 candidate = lastFree + stepDir * tileSize;

            Vector3 half = new Vector3(tileSize * 0.45f, 0.6f, tileSize * 0.45f);
            bool blocked = Physics.CheckBox(
                candidate + Vector3.up * half.y, half,
                Quaternion.identity, wallMask, QueryTriggerInteraction.Ignore);

            if (blocked) break;
            lastFree = candidate;
        }

        // If we didn’t actually move, don’t waste a charge (nice-to-have)
        if (lastFree == startTile)
        {
            currentCharges = Mathf.Min(maxCharges, currentCharges + 1);
            return;
        }

        // 1) Teleport the transform
        transform.position = lastFree;

        // 2) Rebase movement WITHOUT cooldown so input continues smoothly
        var mover = GetComponent<PlayerMovement>();
        if (mover != null) mover.RebaseTo(lastFree, withCooldown: false);

        // VFX
        if (warpVfxPrefab)
        {
            var fx = Instantiate(warpVfxPrefab, lastFree, Quaternion.identity);
            if (warpVfxDuration > 0f) Destroy(fx, warpVfxDuration);
        }
    }

    // ---- IAbilityUI ----
    public string AbilityName => warpAbilityName;
    public Sprite Icon => icon;
    public KeyCode Key => warpKey;
    public float CooldownRemaining => (currentCharges > 0) ? 0f : Mathf.Max(0f, nextChargeReadyTime - Time.time);
    public float CooldownDuration => Mathf.Max(0.01f, rechargeSeconds);
    public bool IsLearned => ctx && ctx.HasAbility(warpAbilityName);

    // ---- IChargeableAbility ----
    public int CurrentCharges => currentCharges;
    public int MaxCharges => Mathf.Max(1, maxCharges);
}
