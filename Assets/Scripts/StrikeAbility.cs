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
    public int strikeRadiusTiles = 1;
    public float cooldownSeconds = 5f;
    public int strikeDamage = 20;

    [Header("UI")]
    public Sprite icon;

    [Header("FX")]
    public bool telegraphOnCast = true;

    private PlayerAbilities ctx;
    private float nextReadyTime = 0f;

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

        // Scale + crit once; then apply fixed result to all in the area (feel free to crit per-target if you prefer)
        int final = ctx.stats ? ctx.stats.ComputeDamage(strikeDamage, PlayerStats.AbilitySchool.Jock, true, out _) : strikeDamage;
        foreach (var c in ctx.GetDiamondTiles(centerAhead, Mathf.Max(0, strikeRadiusTiles)))
            ctx.DamageTileScaled(c, ctx.tileSize * 0.45f, final, PlayerStats.AbilitySchool.Jock, false);

        nextReadyTime = Time.time + Mathf.Max(0.01f, cooldownSeconds);
    }

    public string AbilityName => strikeAbilityName;
    public Sprite Icon => icon;
    public KeyCode Key => strikeKey;
    public float CooldownRemaining => Mathf.Max(0f, nextReadyTime - Time.time);
    public float CooldownDuration => Mathf.Max(0f, cooldownSeconds);
    public bool IsLearned => ctx && ctx.HasAbility(strikeAbilityName);
}
