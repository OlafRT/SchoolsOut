using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class KickAbility : MonoBehaviour, IAbilityUI
{
    [Header("Ability Info")]
    public string kickAbilityName = "Kick";
    public KeyCode kickKey = KeyCode.E;
    public Sprite icon;

    [Header("Kick Settings")]
    public float cooldownSeconds = 5f;
    public int damage = 10;
    public int knockbackTiles = 2;
    public float knockbackDuration = 0.2f;

    [Header("Telegraph")]
    public bool showTelegraphOnCast = true;

    private PlayerAbilities ctx;
    private AutoAttackAbility autoAttack;
    private float nextReadyTime = 0f;

    public AbilityClassRestriction AllowedFor => AbilityClassRestriction.Jock;

    void Awake()
    {
        ctx = GetComponent<PlayerAbilities>();
        autoAttack = GetComponent<AutoAttackAbility>();
    }

    void Update()
    {
        if (!IsLearned) return;
        if (CooldownRemaining > 0f) return;

        if (Input.GetKeyDown(kickKey))
        {
            CastKick();
            nextReadyTime = Time.time + Mathf.Max(0.01f, cooldownSeconds);
        }
    }

    void CastKick()
    {
        Vector3 dir8 = ctx.SnapDirTo8(transform.forward);
        var (sx, sz) = ctx.StepFromDir8(dir8);
        if (sx == 0 && sz == 0) return;

        Vector3 baseTile = ctx.Snap(transform.position);
        float t = ctx.tileSize;

        Vector3 forward = new Vector3(sx, 0, sz);
        Vector3 right   = new Vector3(-sz, 0, sx);

        var tiles = new List<Vector3>();
        Vector3 rowCenter = baseTile + forward * t;
        tiles.Add(rowCenter - right * t);
        tiles.Add(rowCenter);
        tiles.Add(rowCenter + right * t);
        tiles.Add(baseTile + (forward + right) * t);
        tiles.Add(baseTile + (forward - right) * t);
        tiles.Add(baseTile + right * t);
        tiles.Add(baseTile - right * t);

        if (showTelegraphOnCast) ctx.TelegraphOnce(tiles);

        foreach (var center in tiles)
        {
            var hits = Physics.OverlapBox(
                center,
                new Vector3(t * 0.45f, 0.6f, t * 0.45f),
                Quaternion.identity,
                ctx.targetLayer,
                QueryTriggerInteraction.Ignore);

            foreach (var h in hits)
            {
                // Scaled + crit
                ctx.ApplyDamageToCollider(h, damage, PlayerStats.AbilitySchool.Jock, true);

                if (h.TryGetComponent<NPCMovement>(out var mover))
                {
                    var pf = mover.pathfinder;
                    Vector3 cur = mover.Snap(h.transform.position);
                    Vector3 step = forward.normalized * t;

                    int steps = Mathf.Max(0, knockbackTiles);
                    for (int i = 0; i < steps; i++)
                    {
                        Vector3 next = cur + step;
                        if (pf && pf.IsBlocked(next)) break;
                        cur = next;
                    }
                    mover.KnockbackTo(cur, knockbackDuration);
                }
                else
                {
                    Vector3 start = h.transform.position;
                    Vector3 dest  = ctx.Snap(start + forward.normalized * (knockbackTiles * t));
                    StartCoroutine(FallbackKnockback(h.transform, dest, knockbackDuration));
                }
            }
        }
    }

    IEnumerator FallbackKnockback(Transform target, Vector3 destination, float dur)
    {
        float elapsed = 0f;
        Vector3 start = target.position;

        Rigidbody rb = target.GetComponent<Rigidbody>();
        bool hadRB = rb != null;
        bool prevKinematic = false;
        if (hadRB) { prevKinematic = rb.isKinematic; rb.isKinematic = true; rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }

        dur = Mathf.Max(0.01f, dur);
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float u = Mathf.Clamp01(elapsed / dur);
            target.position = Vector3.Lerp(start, destination, u);
            yield return null;
        }
        target.position = destination;

        if (hadRB) rb.isKinematic = prevKinematic;
    }

    public string AbilityName => kickAbilityName;
    public Sprite Icon => icon;
    public KeyCode Key => kickKey;
    public float CooldownRemaining => Mathf.Max(0f, nextReadyTime - Time.time);
    public float CooldownDuration => Mathf.Max(0f, cooldownSeconds);
    public bool IsLearned => ctx && ctx.HasAbility(kickAbilityName);
}
