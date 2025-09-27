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

    [Header("Animation")]
    [SerializeField] private Animator animator;        // auto-found in Awake if left null
    [SerializeField] private string warpTrigger = "Warp";

    [Header("Audio")]
    [SerializeField] private AudioClip warpSfx;        // <- add your warp sound here
    [SerializeField, Range(0f, 2f)] private float warpSfxVolume = 1f;
    [SerializeField, Range(0.25f, 2f)] private float warpSfxPitch = 1f;

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

        if (!animator) animator = GetComponentInChildren<Animator>(true);
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

        // If we didn’t actually move, don’t waste a charge
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

        // Determine arrival FX position (try to align to ground height)
        Vector3 fxPos = lastFree;
        if (ctx.TryGetGroundHeight(lastFree, out float gy)) fxPos.y = gy;

        // 3) Arrival VFX
        if (warpVfxPrefab)
        {
            var fx = Instantiate(warpVfxPrefab, fxPos, Quaternion.identity);
            if (warpVfxDuration > 0f) Destroy(fx, warpVfxDuration);
        }

        // 4) Arrival SFX
        PlayOneShotAt(fxPos, warpSfx, warpSfxVolume, warpSfxPitch);

        // 5) Play "Warp" animation trigger
        if (animator && !string.IsNullOrEmpty(warpTrigger))
            animator.SetTrigger(warpTrigger);
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

    // ---- simple 3D one-shot helper ----
    void PlayOneShotAt(Vector3 pos, AudioClip clip, float volume, float pitch)
    {
        if (!clip) return;
        var go = new GameObject("OneShotAudio_Warp");
        go.transform.position = pos;
        var a = go.AddComponent<AudioSource>();
        a.clip = clip;
        a.volume = Mathf.Clamp01(volume);
        a.pitch = Mathf.Clamp(pitch, 0.25f, 2f);
        a.spatialBlend = 1f; // 3D
        a.rolloffMode = AudioRolloffMode.Linear;
        a.maxDistance = 30f;
        a.Play();
        Destroy(go, clip.length / Mathf.Max(0.01f, a.pitch) + 0.1f);
    }
}
