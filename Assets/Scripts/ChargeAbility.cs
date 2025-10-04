using UnityEngine;
using System.Collections;

[DisallowMultipleComponent]
public class ChargeAbility : MonoBehaviour, IAbilityUI
{
    [Header("Learned Gate")]
    public string chargeAbilityName = "Charge";

    [Header("Input")]
    public KeyCode chargeKey = KeyCode.Q;

    [Header("Charge")]
    public int   chargeDistanceTiles       = 10;
    public float chargeSpeedTilesPerSecond = 16f;
    public float chargeStunSeconds         = 2f;
    public float stopYOffsetClamp          = 0f;

    [Header("Hit Detection")]
    public float nextTileHitRadius = 0.45f;

    [Header("Impact FX")]
    public GameObject impactVfxPrefab;
    public float      impactVfxLifetime = 1.2f;
    public AudioClip  impactSfx;
    [Range(0f, 1f)] public float impactSfxVolume = 0.9f;
    public float cameraShakeAmplitude = 0.25f;
    public float cameraShakeDuration  = 0.12f;

    [Header("Animation (via Relay)")]
    [SerializeField] private JockAnimRelay animRelay;
    [Tooltip("If no relay assigned, we’ll try to set these directly on a found Animator:")]
    [SerializeField] private Animator fallbackAnimator;
    [SerializeField] private string chargingBool = "Charging";
    [SerializeField] private string chargeSpeedParam = "ChargeSpeed";
    [SerializeField] private float  chargeSpeedAnimMult = 1f;

    [Header("Status UI")]
    public string  stunStatusTag = "Stunned";
    public Sprite  stunStatusIcon;

    [Header("UI")]
    public Sprite icon;
    public float  cooldownSeconds = 0f;

    private PlayerAbilities   ctx;
    private AutoAttackAbility autoAttack;
    private float             nextReadyTime = 0f;

    public bool IsCharging { get; private set; }
    public AbilityClassRestriction AllowedFor => AbilityClassRestriction.Jock;

    void Awake()
    {
        ctx = GetComponent<PlayerAbilities>();
        autoAttack = GetComponent<AutoAttackAbility>();

        if (!animRelay) animRelay = GetComponentInChildren<JockAnimRelay>(true);
        if (!fallbackAnimator) fallbackAnimator = GetComponentInChildren<Animator>(true);
    }

    void Update()
    {
        if (!IsLearned) return;

        if (IsCharging)
        {
            if (autoAttack) autoAttack.IsSuppressedByOtherAbilities = true;
            PushAnimSpeedParam();
            return;
        }

        if (CooldownRemaining > 0f) return;

        if (Input.GetKeyDown(chargeKey))
            StartCoroutine(DoChargeTilePerfect());
    }

    IEnumerator DoChargeTilePerfect()
    {
        if (cooldownSeconds > 0f) nextReadyTime = Time.time + cooldownSeconds;

        IsCharging = true;
        if (autoAttack) autoAttack.IsSuppressedByOtherAbilities = true;

        SetChargingAnim(true);
        PushAnimSpeedParam();

        if (ctx.movement)
        {
            ctx.movement.StopAllCoroutines();
            ctx.movement.ResetMovementState(transform.position);
            ctx.movement.canMove = false;
        }

        Vector3 dir8 = ctx.SnapDirTo8(transform.forward);
        var (sx, sz) = ctx.StepFromDir8(dir8);
        if (sx == 0 && sz == 0) { EndCharge(); yield break; }

        Vector3 stepDir = new Vector3(sx, 0f, sz).normalized;
        transform.rotation = Quaternion.LookRotation(dir8, Vector3.up);

        float startY = transform.position.y;
        Vector3 FixY(Vector3 p) { p.y = startY + stopYOffsetClamp; return p; }

        Vector3 currentTile = ctx.Snap(transform.position);
        float tileSize = Mathf.Max(0.01f, ctx.tileSize);
        float moveSpeed = Mathf.Max(0.01f, chargeSpeedTilesPerSecond) * tileSize;
        float radius    = Mathf.Max(0.05f, nextTileHitRadius);

        int wallMask   = ctx.wallLayer.value;
        int targetMask = ctx.targetLayer.value;

        bool IsWallBlocked(Vector3 tileCenter)
        {
            Vector3 half = new Vector3(tileSize * 0.45f, 0.6f, tileSize * 0.45f);
            return Physics.CheckBox(tileCenter + Vector3.up * half.y, half, Quaternion.identity, wallMask, QueryTriggerInteraction.Ignore);
        }

        bool didImpact = false;
        Vector3 impactPoint = currentTile;

        int maxSteps = Mathf.Max(0, chargeDistanceTiles);
        for (int step = 0; step < maxSteps; step++)
        {
            transform.rotation = Quaternion.LookRotation(dir8, Vector3.up);

            Vector3 nextTile = currentTile + stepDir * tileSize;

            if (IsWallBlocked(nextTile))
            {
                didImpact = true;
                impactPoint = FixY(nextTile);
                break;
            }

            var hits = Physics.OverlapSphere(nextTile + Vector3.up * 0.4f, radius, targetMask, QueryTriggerInteraction.Ignore);
            if (hits != null && hits.Length > 0)
            {
                didImpact = true;
                impactPoint = FixY(nextTile);
                break;
            }

            Vector3 start = transform.position;
            Vector3 end   = FixY(nextTile);
            float dist    = Vector3.Distance(start, end);
            float dur     = Mathf.Max(0.01f, dist / moveSpeed);
            float t       = 0f;

            while (t < 1f)
            {
                transform.rotation = Quaternion.LookRotation(dir8, Vector3.up);
                t += Time.deltaTime / dur;
                transform.position = Vector3.Lerp(start, end, t);
                yield return null;
            }
            transform.position = end;

            currentTile = nextTile;
        }

        // Stun targets one tile ahead
        {
            Vector3 tileAhead = currentTile + stepDir * tileSize;
            var hits = Physics.OverlapSphere(tileAhead + Vector3.up * 0.4f, radius, targetMask, QueryTriggerInteraction.Ignore);
            if (hits != null && hits.Length > 0)
            {
                foreach (var col in hits)
                {
                    if (!col) continue;
                    var host = col.GetComponentInParent<NPCStatusHost>();
                    if (host && stunStatusIcon) host.AddOrRefreshAura(stunStatusTag, this, stunStatusIcon);

                    var ai = col.GetComponentInParent<NPCAI>();
                    if (ai)
                    {
                        ai.ApplyStun(chargeStunSeconds);
                        ai.CancelAttack();
                        StartCoroutine(RemoveStatusAfter(host, stunStatusTag, this, chargeStunSeconds));
                    }
                    else if (col.TryGetComponent<IStunnable>(out var stun))
                    {
                        stun.ApplyStun(chargeStunSeconds);
                        StartCoroutine(RemoveStatusAfter(host, stunStatusTag, this, chargeStunSeconds));
                    }
                    else
                    {
                        col.SendMessage("ApplyStun", chargeStunSeconds, SendMessageOptions.DontRequireReceiver);
                        StartCoroutine(RemoveStatusAfter(host, stunStatusTag, this, chargeStunSeconds));
                    }
                }
            }
        }

        transform.position = FixY(currentTile);

        if (didImpact)
        {
            if (impactVfxPrefab)
            {
                var vfx = Instantiate(impactVfxPrefab, impactPoint, Quaternion.identity);
                if (impactVfxLifetime > 0f) Destroy(vfx, impactVfxLifetime);
            }
            if (impactSfx) AudioSource.PlayClipAtPoint(impactSfx, impactPoint, impactSfxVolume);
            CameraShaker.Instance?.Shake(cameraShakeDuration, cameraShakeAmplitude);
        }

        EndCharge();
    }

    IEnumerator RemoveStatusAfter(NPCStatusHost host, string tag, Object source, float delay)
    {
        if (!host) yield break;
        yield return new WaitForSeconds(Mathf.Max(0.01f, delay));
        if (host) host.RemoveAura(tag, source);
    }

    void EndCharge()
    {
        SetChargingAnim(false);

        if (ctx.movement)
        {
            ctx.movement.ResetMovementState(transform.position);
            ctx.movement.canMove = true;
        }
        IsCharging = false;
        if (autoAttack) autoAttack.IsSuppressedByOtherAbilities = false;
    }

    // ---- Animator/Relay helpers ----
    void SetChargingAnim(bool on)
    {
        if (animRelay) { animRelay.SetCharging(on); return; }

        if (fallbackAnimator && !string.IsNullOrEmpty(chargingBool))
        {
            fallbackAnimator.SetBool(chargingBool, on);
            if (!on && !string.IsNullOrEmpty(chargeSpeedParam))
                fallbackAnimator.SetFloat(chargeSpeedParam, 0f);
        }
    }

    void PushAnimSpeedParam()
    {
        float norm = chargeSpeedTilesPerSecond / 16f * chargeSpeedAnimMult;
        if (animRelay) { animRelay.SetChargeSpeed(norm); return; }
        if (fallbackAnimator && !string.IsNullOrEmpty(chargeSpeedParam))
            fallbackAnimator.SetFloat(chargeSpeedParam, norm);
    }

    // ---- IAbilityUI ----
    public string AbilityName => chargeAbilityName;
    public Sprite Icon => icon;
    public KeyCode Key => chargeKey;
    public float CooldownRemaining => Mathf.Max(0f, nextReadyTime - Time.time);
    public float CooldownDuration => Mathf.Max(0f, cooldownSeconds);
    public bool IsLearned => ctx && ctx.HasAbility(chargeAbilityName);
}
