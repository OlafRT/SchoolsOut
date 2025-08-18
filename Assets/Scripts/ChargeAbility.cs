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
    public int chargeDistanceTiles = 10;
    public float chargeSpeedTilesPerSecond = 16f;
    public float chargeStunSeconds = 2f;
    public float chargeHitRadius = 0.4f;
    public float chargeHeightOffset = 0.5f;

    [Header("UI")]
    public Sprite icon;
    public float cooldownSeconds = 0f;

    private PlayerAbilities ctx;
    private AutoAttackAbility autoAttack;
    private float nextReadyTime = 0f;
    public bool IsCharging { get; private set; }

    // ---- IClassRestrictedAbility ----
    public AbilityClassRestriction AllowedFor => AbilityClassRestriction.Jock;

    void Awake()
    {
        ctx = GetComponent<PlayerAbilities>();
        autoAttack = GetComponent<AutoAttackAbility>();
    }

    void Update()
    {
        if (!IsLearned) return;

        if (IsCharging)
        {
            if (autoAttack) autoAttack.IsSuppressedByOtherAbilities = true;
            return;
        }

        if (CooldownRemaining > 0f) return;

        if (Input.GetKeyDown(chargeKey))
            StartCoroutine(DoCharge());
    }

    IEnumerator DoCharge()
    {
        if (cooldownSeconds > 0f) nextReadyTime = Time.time + cooldownSeconds;

        IsCharging = true;
        if (autoAttack) autoAttack.IsSuppressedByOtherAbilities = true;

        // 8-way direction
        Vector3 dir8 = ctx.SnapDirTo8(transform.forward);
        var (sx, sz) = ctx.StepFromDir8(dir8);
        if (sx == 0 && sz == 0) { EndCharge(); yield break; }
        Vector3 dir = new Vector3(sx, 0f, sz).normalized;
        transform.rotation = Quaternion.LookRotation(dir8, Vector3.up);

        // Cancel any tile move to avoid rubber-banding
        if (ctx.movement)
        {
            ctx.movement.StopAllCoroutines();
            ctx.movement.ResetMovementState(transform.position);
            ctx.movement.canMove = false;
        }

        // Lock Y to the starting value
        float startY = transform.position.y;
        void ForceY(ref Vector3 p) { p.y = startY; }
        var snapNow = transform.position; ForceY(ref snapNow); transform.position = snapNow;

        // Temporarily neutralize Rigidbody so physics can’t add drift
        Rigidbody rb = GetComponent<Rigidbody>();
        bool hadRB = rb != null;
        bool prevKinematic = false, prevUseGravity = false;
        if (hadRB)
        {
            prevKinematic = rb.isKinematic;
            prevUseGravity = rb.useGravity;
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        float maxDistance = Mathf.Max(0, chargeDistanceTiles) * ctx.tileSize;
        float traveled = 0f;
        float speed = Mathf.Max(0.01f, chargeSpeedTilesPerSecond) * ctx.tileSize;
        float epsilon = 0.01f;

        // masks
        int wallMask = ctx.wallLayer.value;
        int targetMask = ctx.targetLayer.value;
        int blockMask = wallMask | targetMask;

        // tile-sized check box extents
        Vector3 tileHalf = new Vector3(ctx.tileSize * 0.45f, 0.6f, ctx.tileSize * 0.45f);

        bool IsTileBlocked(Vector3 tileCenter)
        {
            return Physics.CheckBox(tileCenter + Vector3.up * tileHalf.y, tileHalf, Quaternion.identity, blockMask, QueryTriggerInteraction.Ignore);
        }

        bool havePlannedImpact = false;
        Vector3 plannedStop = Vector3.zero;
        Transform plannedTarget = null;

        while (traveled < maxDistance)
        {
            float remaining = maxDistance - traveled;
            float stepDist = speed * Time.deltaTime;

            if (!havePlannedImpact)
            {
                Vector3 origin = transform.position + Vector3.up * chargeHeightOffset;

                if (Physics.SphereCast(origin, chargeHitRadius, dir, out RaycastHit hit, remaining, blockMask, QueryTriggerInteraction.Ignore))
                {
                    bool hitIsTarget = ((targetMask & (1 << hit.collider.gameObject.layer)) != 0);

                    if (hitIsTarget)
                    {
                        plannedTarget = hit.collider.transform;
                        Vector3 targetTile = ctx.Snap(plannedTarget.position);
                        Vector3 desiredStop = targetTile - new Vector3(sx, 0f, sz) * ctx.tileSize;

                        Vector3 stopTile = desiredStop;
                        int backSteps = 0;
                        while (IsTileBlocked(stopTile) && backSteps < chargeDistanceTiles)
                        {
                            stopTile -= new Vector3(sx, 0f, sz) * ctx.tileSize;
                            backSteps++;
                        }

                        plannedStop = stopTile;
                        ForceY(ref plannedStop);
                        havePlannedImpact = true;
                    }
                    else
                    {
                        float toHit = Mathf.Max(0f, hit.distance - epsilon);
                        float deltaWall = Mathf.Min(stepDist, toHit);

                        if (deltaWall > 0f)
                        {
                            Vector3 p = transform.position + dir * deltaWall;
                            ForceY(ref p);
                            transform.position = p;
                            traveled += deltaWall;
                            yield return null;
                            continue;
                        }

                        Vector3 stop = hit.point - dir * (chargeHitRadius + epsilon);
                        ForceY(ref stop);
                        transform.position = stop;
                        break; // stop charge at wall
                    }
                }
            }

            Vector3 frameTarget = havePlannedImpact
                ? plannedStop
                : transform.position + dir * remaining;

            float distToTarget = Vector3.Distance(transform.position, frameTarget);
            float delta = Mathf.Min(stepDist, distToTarget);

            if (delta > 0f)
            {
                Vector3 p = transform.position + dir * delta;
                ForceY(ref p);
                transform.position = p;
                traveled += delta;
                yield return null;
            }

            // ---- IMPACT ----
            if (havePlannedImpact && Vector3.SqrMagnitude(transform.position - plannedStop) <= 0.0004f)
            {
                if (plannedTarget)
                {
                    // Prefer NPCAI on this object or any parent, then fallback to IStunnable
                    NPCAI ai = plannedTarget.GetComponent<NPCAI>();
                    if (!ai) ai = plannedTarget.GetComponentInParent<NPCAI>();
                    if (ai)
                    {
                        ai.ApplyStun(chargeStunSeconds);
                        ai.CancelAttack();
                    }
                    else
                    {
                        if (plannedTarget.TryGetComponent<IStunnable>(out var stun)) stun.ApplyStun(chargeStunSeconds);
                        else plannedTarget.SendMessage("ApplyStun", chargeStunSeconds, SendMessageOptions.DontRequireReceiver);
                    }
                }
                break; // end charge on impact
            }

            if (!havePlannedImpact && delta < 0.0001f)
                break;
        }

        // Restore Rigidbody & finish
        if (hadRB)
        {
            var p = transform.position; ForceY(ref p); transform.position = p;
            rb.isKinematic = prevKinematic;
            rb.useGravity  = prevUseGravity;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        EndCharge();
    }

    void EndCharge()
    {
        if (ctx.movement)
        {
            ctx.movement.ResetMovementState(transform.position);
            ctx.movement.canMove = true;
        }
        IsCharging = false;
        if (autoAttack) autoAttack.IsSuppressedByOtherAbilities = false;
    }

    // ---- IAbilityUI ----
    public string AbilityName => chargeAbilityName;
    public Sprite Icon => icon;
    public KeyCode Key => chargeKey;
    public float CooldownRemaining => Mathf.Max(0f, nextReadyTime - Time.time);
    public float CooldownDuration => Mathf.Max(0f, cooldownSeconds);
    public bool IsLearned => ctx && ctx.HasAbility(chargeAbilityName);
}
