using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class StrikeAbility : MonoBehaviour, IAbilityUI
{
    [Header("Learned Gate")]
    public string strikeAbilityName = "Strike";

    [Header("Input")]
    public KeyCode strikeKey = KeyCode.F;

    [Header("Strike Settings")]
    public int strikeRadiusTiles = 1;
    public float cooldownSeconds = 5f;
    public int strikeDamage = 20;

    [Header("Animation")]
    [SerializeField] private Animator animator;          // auto-resolves if null
    [SerializeField] private string strikeTrigger = "Strike";
    [Tooltip("If the anim event never arrives, auto-fire after this many seconds.")]
    [SerializeField] private float eventFailSafeSeconds = 1.0f;

    [Header("Telegraph")]
    [Tooltip("Show a preview during wind-up that tracks your current aim until the impact event.")]
    [SerializeField] private bool telegraphPreImpactPreview = true;
    [Tooltip("How often to refresh the moving preview (seconds).")]
    [SerializeField] private float telegraphRefreshInterval = 0.05f;

    [Header("Audio/VFX")]
    [Tooltip("Swoosh played during the swing (animation event, a few frames before impact).")]
    [SerializeField] private AudioClip strikeWhooshSfx;
    [Range(0f,1f)] [SerializeField] private float strikeWhooshVolume = 1f;

    [Tooltip("Impact SFX played only if we actually hit something.")]
    [SerializeField] private AudioClip strikeImpactSfx;
    [Range(0f,1f)] [SerializeField] private float strikeImpactVolume = 1f;

    [Tooltip("Optional impact VFX spawned at the first hit point (only on hit).")]
    [SerializeField] private GameObject strikeImpactVfx;
    [SerializeField] private float strikeImpactVfxLife = 1.5f;
    [SerializeField] private float strikeImpactVfxScale = 1f;

    [Header("Wall Break Gate")]
    [Tooltip("How long (seconds) the 'Strike gate' stays open so walls know this hit came from Strike.")]
    [SerializeField] private float strikeGateSeconds = 0.05f;

    [Header("UI")]
    public Sprite icon;

    private PlayerAbilities ctx;
    private float nextReadyTime = 0f;

    public AbilityClassRestriction AllowedFor => AbilityClassRestriction.Jock;

    // --- global Strike gate (read by DamageRelay) ---
    private static float s_gateCloseTime = 0f;
    public static bool IsStrikeWindowOpen => Time.time <= s_gateCloseTime;
    public static void OpenStrikeWindow(float seconds) { s_gateCloseTime = Time.time + Mathf.Max(0f, seconds); }

    // pending strike, waiting for the animation event
    private struct Pending
    {
        public bool has;
        public int finalDamage;
        public float timeoutAt;

        // preview helper
        public float nextTelegraphAt;
    }
    private Pending pending;

    void Awake()
    {
        ctx = GetComponent<PlayerAbilities>();
        if (!animator) animator = GetComponentInChildren<Animator>(true);
    }

    void Update()
    {
        if (!IsLearned) return;

        if (CooldownRemaining <= 0f && Input.GetKeyDown(strikeKey))
            ArmStrike();

        // Live preview telegraph while waiting for the anim event
        if (pending.has && telegraphPreImpactPreview && Time.time >= pending.nextTelegraphAt)
        {
            var tilesNow = BuildStrikeTilesFromCurrent();
            ctx.TelegraphOnce(tilesNow);
            pending.nextTelegraphAt = Time.time + Mathf.Max(0.01f, telegraphRefreshInterval);
        }

        // failsafe if the anim event never fires
        if (pending.has && Time.time >= pending.timeoutAt)
        {
            Debug.LogWarning("StrikeAbility: Strike anim event missed — committing via failsafe.");
            AnimEvent_FireStrike();
        }
    }

    private void ArmStrike()
    {
        // compute damage now so numbers & effects stay in sync with this strike
        int final = ctx.stats ? ctx.stats.ComputeDamage(strikeDamage, PlayerStats.AbilitySchool.Jock, true, out _) : strikeDamage;

        pending.has = true;
        pending.finalDamage = final;
        pending.timeoutAt = Time.time + Mathf.Max(0.2f, eventFailSafeSeconds);
        pending.nextTelegraphAt = 0f; // show preview ASAP if enabled

        // kick the animation
        if (animator && !string.IsNullOrEmpty(strikeTrigger))
            animator.SetTrigger(strikeTrigger);
        else
            AnimEvent_FireStrike(); // no animator? just fire immediately
    }

    /// <summary>Animation Event: play the swoosh a bit before impact.</summary>
    public void AnimEvent_StrikeWhoosh()
    {
        if (!strikeWhooshSfx) return;
        Vector3 at = transform.position + Vector3.up * 1.2f;
        AudioSource.PlayClipAtPoint(strikeWhooshSfx, at, strikeWhooshVolume);
    }

    /// <summary>
    /// Animation Event on the impact frame.
    /// Recomputes tiles, opens the Strike gate, applies damage, and plays impact SFX/VFX only on real hit.
    /// </summary>
    public void AnimEvent_FireStrike()
    {
        if (!pending.has) return;

        // Recompute tiles AT IMPACT so the hit matches where you actually are/facing
        var tilesNow = BuildStrikeTilesFromCurrent();

        // If you turned off live preview, flash the correct telegraph at impact
        if (!telegraphPreImpactPreview) ctx.TelegraphOnce(tilesNow);

        // Detect whether we actually hit something (for impact feedback)
        bool hitAny = false;
        Vector3 firstHitPos = Vector3.zero;
        float r = ctx.tileSize * 0.45f;

        foreach (var c in tilesNow)
        {
            var cols = Physics.OverlapSphere(c + Vector3.up * 0.4f, r, ctx.targetLayer, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < cols.Length; i++)
            {
                var col = cols[i];
                if (col.TryGetComponent<IDamageable>(out _))
                {
                    hitAny = true;
                    firstHitPos = col.ClosestPoint(c);
                    goto DoneScan;
                }
            }
        }
    DoneScan:

        // 🔒 Open the "Strike only" wall-break window, apply damage, then let it auto-close
        OpenStrikeWindow(strikeGateSeconds);

        foreach (var c in tilesNow)
            ctx.DamageTileScaled(c, r, pending.finalDamage, PlayerStats.AbilitySchool.Jock, false);

        // Impact feedback only if we hit something
        if (hitAny)
        {
            if (strikeImpactSfx)
                AudioSource.PlayClipAtPoint(strikeImpactSfx, firstHitPos, strikeImpactVolume);

            if (strikeImpactVfx)
            {
                var vfx = Instantiate(strikeImpactVfx, firstHitPos, Quaternion.identity);
                vfx.transform.localScale *= strikeImpactVfxScale;
                if (strikeImpactVfxLife > 0f) Destroy(vfx, strikeImpactVfxLife);
            }
        }

        nextReadyTime = Time.time + Mathf.Max(0.01f, cooldownSeconds);
        pending.has = false;
    }

    // Build the diamond pattern centered one tile ahead of CURRENT facing
    private List<Vector3> BuildStrikeTilesFromCurrent()
    {
        Vector3 dir8 = ctx.SnapDirTo8(transform.forward);
        var (sx, sz) = ctx.StepFromDir8(dir8);
        Vector3 centerAhead = ctx.Snap(transform.position) + new Vector3(sx, 0f, sz) * ctx.tileSize;

        var tilesEnum = ctx.GetDiamondTiles(centerAhead, Mathf.Max(0, strikeRadiusTiles));
        return new List<Vector3>(tilesEnum);
    }

    // IAbilityUI
    public string AbilityName => strikeAbilityName;
    public Sprite Icon => icon;
    public KeyCode Key => strikeKey;
    public float CooldownRemaining => Mathf.Max(0f, nextReadyTime - Time.time);
    public float CooldownDuration => Mathf.Max(0f, cooldownSeconds);
    public bool IsLearned => ctx && ctx.HasAbility(strikeAbilityName);
}
