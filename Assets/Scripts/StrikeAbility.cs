using UnityEngine;
using System.Collections;

[DisallowMultipleComponent]
public class StrikeAbility : MonoBehaviour, IAbilityUI
{
    [Header("Learned Gate")]
    public string strikeAbilityName = "Strike";

    [Header("Input")]
    public KeyCode strikeKey = KeyCode.F;

    [Header("Strike Settings")]
    [Tooltip("Diamond radius in tiles (1 = 5 tiles total).")]
    public int strikeRadiusTiles = 1;
    [Tooltip("Seconds between casts.")]
    public float cooldownSeconds = 5f;
    [Tooltip("Damage dealt to each target in the area.")]
    public int strikeDamage = 20;

    [Header("UI")]
    public Sprite icon;

    [Header("FX")]
    [Tooltip("Optional: play a telegraph for the strike area.")]
    public bool telegraphOnCast = true;

    private PlayerAbilities ctx;
    private float nextReadyTime = 0f;

    // ---- IClassRestrictedAbility ----
    public AbilityClassRestriction AllowedFor => AbilityClassRestriction.Jock;

    void Awake() { ctx = GetComponent<PlayerAbilities>(); }

    void Update()
    {
        if (!IsLearned) return;
        if (CooldownRemaining > 0f) return;

        if (Input.GetKeyDown(strikeKey))
            CastStrike();
    }

    void CastStrike()
    {
        Vector3 dir8 = ctx.SnapDirTo8(transform.forward);
        var (sx, sz) = ctx.StepFromDir8(dir8);
        if (sx == 0 && sz == 0) return;

        Vector3 centerAhead = ctx.Snap(transform.position) + new Vector3(sx, 0f, sz) * ctx.tileSize;

        var tiles = ctx.GetDiamondTiles(centerAhead, Mathf.Max(0, strikeRadiusTiles));
        if (telegraphOnCast) ctx.TelegraphOnce(tiles);

        foreach (var c in ctx.GetDiamondTiles(centerAhead, Mathf.Max(0, strikeRadiusTiles)))
            ctx.DamageTile(c, ctx.tileSize * 0.45f, strikeDamage);

        nextReadyTime = Time.time + Mathf.Max(0.01f, cooldownSeconds);
    }

    // ---- IAbilityUI ----
    public string AbilityName => strikeAbilityName;
    public Sprite Icon => icon;
    public KeyCode Key => strikeKey;
    public float CooldownRemaining => Mathf.Max(0f, nextReadyTime - Time.time);
    public float CooldownDuration => Mathf.Max(0f, cooldownSeconds);
    public bool IsLearned => ctx && ctx.HasAbility(strikeAbilityName);
}
