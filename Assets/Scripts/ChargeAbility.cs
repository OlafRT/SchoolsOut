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
    public float chargeSpeedTilesPerSecond = 16f;   // tiles per second (center-to-center)
    public float chargeStunSeconds         = 2f;
    public float stopYOffsetClamp          = 0f;    // keep Y locked; leave at 0

    [Header("Hit Detection")]
    [Tooltip("Radius (world units) to detect a target on the next tile.")]
    public float nextTileHitRadius = 0.45f;

    [Header("Impact FX (plays only if we collide with wall/obstacle/NPC)")]
    public GameObject impactVfxPrefab;
    public float      impactVfxLifetime = 1.2f;
    public AudioClip  impactSfx;
    [Range(0f, 1f)] public float impactSfxVolume = 0.9f;
    [Tooltip("Camera shake amplitude and duration on impact.")]
    public float cameraShakeAmplitude = 0.25f;
    public float cameraShakeDuration  = 0.12f;

    [Header("Status UI")]
    public string  stunStatusTag = "Stunned";
    public Sprite  stunStatusIcon;

    [Header("UI")]
    public Sprite icon;
    public float  cooldownSeconds = 0f;

    // ---- runtime
    private PlayerAbilities   ctx;
    private AutoAttackAbility autoAttack;
    private float             nextReadyTime = 0f;

    public  bool IsCharging { get; private set; }
    public  AbilityClassRestriction AllowedFor => AbilityClassRestriction.Jock; // if you use class restriction

    void Awake()
    {
        ctx = GetComponent<PlayerAbilities>();
        autoAttack = GetComponent<AutoAttackAbility>();
    }

    void Update()
    {
        if (!IsLearned) return;

        // while charging, keep AA suppressed; don't allow steering
        if (IsCharging)
        {
            if (autoAttack) autoAttack.IsSuppressedByOtherAbilities = true;
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

        // Lock player-movement during charge
        if (ctx.movement)
        {
            ctx.movement.StopAllCoroutines();
            ctx.movement.ResetMovementState(transform.position);
            ctx.movement.canMove = false;
        }

        // Determine 8-way direction once and lock it
        Vector3 dir8 = ctx.SnapDirTo8(transform.forward);
        var (sx, sz) = ctx.StepFromDir8(dir8);
        if (sx == 0 && sz == 0) { EndCharge(); yield break; }

        Vector3 stepDir = new Vector3(sx, 0f, sz).normalized;
        transform.rotation = Quaternion.LookRotation(dir8, Vector3.up);

        // Keep Y strictly fixed to the start
        float startY = transform.position.y;
        Vector3 FixY(Vector3 p) { p.y = startY + stopYOffsetClamp; return p; }

        // Tile-by-tile march
        Vector3 currentTile = ctx.Snap(transform.position);
        float tileSize = Mathf.Max(0.01f, ctx.tileSize);
        float moveSpeed = Mathf.Max(0.01f, chargeSpeedTilesPerSecond) * tileSize;
        float radius    = Mathf.Max(0.05f, nextTileHitRadius);

        // Convenience masks (walls + targets)
        int wallMask   = ctx.wallLayer.value;
        int targetMask = ctx.targetLayer.value;

        // Helper: is that tile blocked by WALLS? (targets are handled separately)
        bool IsWallBlocked(Vector3 tileCenter)
        {
            Vector3 half = new Vector3(tileSize * 0.45f, 0.6f, tileSize * 0.45f);
            return Physics.CheckBox(tileCenter + Vector3.up * half.y, half, Quaternion.identity, wallMask, QueryTriggerInteraction.Ignore);
        }

        bool didImpact = false;     // ← we only play FX if true
        Vector3 impactPoint = currentTile;

        // Move up to N tiles
        int maxSteps = Mathf.Max(0, chargeDistanceTiles);
        for (int step = 0; step < maxSteps; step++)
        {
            transform.rotation = Quaternion.LookRotation(dir8, Vector3.up); // defeat RMB steering

            Vector3 nextTile = currentTile + stepDir * tileSize;

            // If the next tile is a WALL → stop here (impact)
            if (IsWallBlocked(nextTile))
            {
                didImpact = true;
                impactPoint = FixY(nextTile); // the blocked tile we slammed into
                break;
            }

            // If the next tile contains a TARGET → stop here (impact) and we'll stun them after the loop
            var hits = Physics.OverlapSphere(nextTile + Vector3.up * 0.4f, radius, targetMask, QueryTriggerInteraction.Ignore);
            if (hits != null && hits.Length > 0)
            {
                didImpact = true;
                impactPoint = FixY(nextTile);
                break;
            }

            // Otherwise, move to the next tile center at charge speed
            Vector3 start = transform.position;
            Vector3 end   = FixY(nextTile);
            float dist    = Vector3.Distance(start, end);
            float t       = 0f;
            float dur     = Mathf.Max(0.01f, dist / moveSpeed);

            while (t < 1f)
            {
                transform.rotation = Quaternion.LookRotation(dir8, Vector3.up); // keep locked
                t += Time.deltaTime / dur;
                transform.position = Vector3.Lerp(start, end, t);
                yield return null;
            }
            transform.position = end;

            // Arrived → advance current tile
            currentTile = nextTile;
        }

        // After we stop, if the *tile in front* has targets, stun them.
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

        // Snap to final tile center (keeps Y)
        transform.position = FixY(currentTile);

        // Play impact FX/SFX/camera shake only if we collided with something
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
