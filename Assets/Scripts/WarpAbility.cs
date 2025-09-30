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

    [Header("VFX / SFX")]
    [Tooltip("Effect spawned where you START warping from.")]
    public GameObject warpFromVfxPrefab;
    [Tooltip("Effect spawned where you ARRIVE.")]
    public GameObject warpToVfxPrefab;
    public float warpVfxDuration = 1.5f;

    [SerializeField] private AudioClip warpSfx;
    [SerializeField, Range(0f, 2f)] private float warpSfxVolume = 1f;
    [SerializeField, Range(0.25f, 2f)] private float warpSfxPitch = 1f;

    [Header("Animation")]
    [SerializeField] private Animator animator;        // auto-found in Awake if left null
    [SerializeField] private string warpTrigger = "Warp";

    [Header("Charges")]
    public int maxCharges = 2;
    public float rechargeSeconds = 10f;

    [Header("Collision / Blocking")]
    [Tooltip("Layers that block warp path (usually your 'Wall' layer).")]
    public LayerMask wallBlockerLayers;
    [Tooltip("Also block destructible walls even if they are on Target layer (recommended ON).")]
    public bool blockDestructibleWallsOnTargetLayer = true;

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

        // If not set in inspector, fall back to the project's wall layer
        if (wallBlockerLayers.value == 0 && ctx != null)
            wallBlockerLayers = ctx.wallLayer;

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

        // --- spawn ORIGIN VFX/SFX before moving ---
        Vector3 fromFxPos = startTile;
        if (ctx.TryGetGroundHeight(fromFxPos, out float gyFrom)) fromFxPos.y = gyFrom;
        SpawnVfx(fromFxPos, warpFromVfxPrefab, warpVfxDuration);
        PlayOneShotAt(fromFxPos, warpSfx, warpSfxVolume, warpSfxPitch);

        // Step forward up to N tiles; stop BEFORE first blocked tile
        Vector3 lastFree = startTile;
        for (int step = 0; step < warpDistanceTiles; step++)
        {
            Vector3 candidate = lastFree + stepDir * tileSize;

            // Box that approximates our capsule footprint
            Vector3 half = new Vector3(tileSize * 0.45f, 0.6f, tileSize * 0.45f);

            // 1) Regular wall blockers (e.g. "Wall" layer)
            bool blocked = Physics.CheckBox(
                candidate + Vector3.up * half.y, half,
                Quaternion.identity, wallBlockerLayers, QueryTriggerInteraction.Ignore);

            // 2) Destructible wall objects that live on Target layer: block these too
            if (!blocked && blockDestructibleWallsOnTargetLayer)
            {
                // Only query the Target layer to keep this cheap
                int targetMask = ctx ? ctx.targetLayer.value : 0;
                if (targetMask != 0)
                {
                    var cols = Physics.OverlapBox(
                        candidate + Vector3.up * half.y, half,
                        Quaternion.identity, targetMask, QueryTriggerInteraction.Ignore);

                    for (int i = 0; i < cols.Length; i++)
                    {
                        // We block ONLY if this collider belongs to a destructible wall
                        if (cols[i].GetComponentInParent<DestructibleWallV2>() != null)
                        {
                            blocked = true;
                            break;
                        }
                    }
                }
            }

            if (blocked) break;
            lastFree = candidate;
        }

        // If we didn’t actually move, refund the charge and bail
        if (lastFree == startTile)
        {
            currentCharges = Mathf.Min(maxCharges, currentCharges + 1);
            return;
        }

        // Teleport
        transform.position = lastFree;

        // Rebase movement WITHOUT cooldown so input continues smoothly
        var mover = GetComponent<PlayerMovement>();
        if (mover != null) mover.RebaseTo(lastFree, withCooldown: false);

        // --- spawn ARRIVAL VFX/SFX after moving ---
        Vector3 toFxPos = lastFree;
        if (ctx.TryGetGroundHeight(lastFree, out float gyTo)) toFxPos.y = gyTo;
        SpawnVfx(toFxPos, warpToVfxPrefab ? warpToVfxPrefab : warpFromVfxPrefab, warpVfxDuration);
        // (Optional) play arrival sound too; comment out if you only want it at the start
        PlayOneShotAt(toFxPos, warpSfx, warpSfxVolume, warpSfxPitch);

        // Play "Warp" animation trigger
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

    // ---- helpers ----
    void SpawnVfx(Vector3 pos, GameObject prefab, float life)
    {
        if (!prefab) return;
        var fx = Instantiate(prefab, pos, Quaternion.identity);
        if (life > 0f) Destroy(fx, life);
    }

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
