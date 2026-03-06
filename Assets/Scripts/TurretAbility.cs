using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Nerd ability — deploys an automated turret on a player-chosen tile.
///
/// Usage flow (mirrors BombAbility):
///   1. Player presses turretKey  → enters aim mode, telegraph marker appears.
///   2. Player moves mouse        → marker snaps to nearest valid tile.
///   3. Player left-clicks        → spends a charge, spawns the turret prefab,
///                                  exits aim mode.
///   4. RMB / Escape              → cancels aim.
///
/// The turret prefab must have TurretController attached (or it will be added
/// at runtime). Structure expected:
///   TurretRoot
///     ├─ Base
///     ├─ Swivel        ← rotates Y
///     │    └─ Head     ← rotates X (boot-up tilt)
///     │         └─ Muzzle   (optional spawn point)
///
/// Implements the same IAbilityUI / IClassRestrictedAbility / IChargeableAbility
/// interfaces used by BombAbility so the HUD picks it up automatically.
/// </summary>
[DisallowMultipleComponent]
public class TurretAbility : MonoBehaviour, IAbilityUI, IClassRestrictedAbility, IChargeableAbility
{
    // ─── Learned Gate ──────────────────────────────────────────────────────────
    [Header("Learned Gate")]
    public string turretAbilityName = "Turret";

    // ─── Input ─────────────────────────────────────────────────────────────────
    [Header("Input")]
    public KeyCode turretKey = KeyCode.Q;

    // ─── Prefab ────────────────────────────────────────────────────────────────
    [Header("Turret Prefab")]
    [Tooltip("The turret prefab. Must contain (or will receive) a TurretController component.")]
    public GameObject turretPrefab;

    // ─── Aim Constraints ───────────────────────────────────────────────────────
    [Header("Aim Constraints")]
    [Tooltip("Max placement distance from the player in tiles.")]
    public int maxRangeTiles = 8;
    [Tooltip("Layers considered solid walls for LOS blocking.")]
    public LayerMask wallMask;
    [Tooltip("Small backoff from a blocking wall when snapping the marker.")]
    public float wallBackoff = 0.2f;

    // ─── Turret Settings (forwarded to TurretController) ──────────────────────
    [Header("Turret Behaviour")]
    [Tooltip("Seconds the turret stays alive before self-destructing.")]
    public float turretLifetime   = 18f;
    [Tooltip("Base damage per turret shot (scaled by PlayerStats).")]
    public int   turretDamage     = 8;
    [Tooltip("Scan radius for hostile NPCs (Unity units).")]
    public float turretScanRadius = 12f;
    [Tooltip("Seconds between turret shots.")]
    public float turretFireInterval = 0.7f;

    // ─── Charges ───────────────────────────────────────────────────────────────
    [Header("Charges")]
    public int   maxCharges       = 1;
    public float rechargeSeconds  = 20f;

    // ─── UI ────────────────────────────────────────────────────────────────────
    [Header("UI")]
    public Sprite icon;

    // ─── VFX / SFX ─────────────────────────────────────────────────────────────
    [Header("Placement SFX")]
    [SerializeField] private AudioClip placeSfx;
    [SerializeField, Range(0f, 2f)]    private float placeSfxVolume = 1f;
    [SerializeField, Range(0.25f, 2f)] private float placeSfxPitch  = 1f;

    // ─── Private runtime ───────────────────────────────────────────────────────
    private PlayerAbilities   ctx;
    private AutoAttackAbility autoAttack;

    private bool       isAiming;
    private Vector3    aimCenter;
    private GameObject aimMarker;   // single tile marker shown during aim

    private int   currentCharges;
    private float nextChargeReadyTime;

    // ══════════════════════════════════════════════════════════════════════════
    //  Interface implementations
    // ══════════════════════════════════════════════════════════════════════════

    public AbilityClassRestriction AllowedFor => AbilityClassRestriction.Nerd;

    // IAbilityUI
    public string AbilityName      => turretAbilityName;
    public Sprite Icon             => icon;
    public KeyCode Key             => turretKey;
    public float CooldownRemaining => (currentCharges > 0) ? 0f
                                        : Mathf.Max(0f, nextChargeReadyTime - Time.time);
    public float CooldownDuration  => Mathf.Max(0.01f, rechargeSeconds);
    public bool  IsLearned         => ctx && ctx.HasAbility(turretAbilityName);

    // IChargeableAbility
    public int CurrentCharges => currentCharges;
    public int MaxCharges     => Mathf.Max(1, maxCharges);

    // ══════════════════════════════════════════════════════════════════════════
    //  Unity lifecycle
    // ══════════════════════════════════════════════════════════════════════════

    void Awake()
    {
        ctx        = GetComponent<PlayerAbilities>();
        autoAttack = GetComponent<AutoAttackAbility>();

        currentCharges = Mathf.Max(1, maxCharges);
        if (currentCharges < maxCharges)
            nextChargeReadyTime = Time.time + Mathf.Max(0.01f, rechargeSeconds);

        if (wallMask.value == 0)
            wallMask = LayerMask.GetMask("Wall");
    }

    void Update()
    {
        if (!IsLearned) return;

        TickRecharge();

        // ── Not aiming ────────────────────────────────────────────────────────
        if (!isAiming)
        {
            if (currentCharges > 0 && Input.GetKeyDown(turretKey))
            {
                isAiming = true;
                if (autoAttack) autoAttack.IsSuppressedByOtherAbilities = true;
                UpdateMarker(ConstrainTarget(GetMouseSnapTileCenter()));
            }
            return;
        }

        // ── Cancel ────────────────────────────────────────────────────────────
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
        {
            EndAim(clearMarker: true);
            return;
        }

        // ── Update marker ─────────────────────────────────────────────────────
        Vector3 cur = ConstrainTarget(GetMouseSnapTileCenter());
        if ((cur - aimCenter).sqrMagnitude > 0.0001f) UpdateMarker(cur);

        // ── Confirm placement ─────────────────────────────────────────────────
        if (Input.GetMouseButtonDown(0))
        {
            if (currentCharges <= 0) return;

            Vector3 spawnPos = aimCenter;
            SpendCharge();
            EndAim(clearMarker: true);

            PlaceTurret(spawnPos);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Placement
    // ══════════════════════════════════════════════════════════════════════════

    void PlaceTurret(Vector3 tileCenter)
    {
        if (!turretPrefab) return;

        // Snap to ground height
        if (ctx.TryGetGroundHeight(tileCenter, out float gy)) tileCenter.y = gy;

        var go = Instantiate(turretPrefab, tileCenter, Quaternion.identity);

        // Ensure TurretController exists and initialise it
        var tc = go.GetComponent<TurretController>();
        if (!tc) tc = go.AddComponent<TurretController>();

        tc.Init(
            playerCtx:          ctx,
            overrideLifetime:   turretLifetime,
            overrideBaseDamage: turretDamage,
            overrideScanRadius: turretScanRadius
        );

        // Override fire interval if the prefab didn't set one
        if (turretFireInterval > 0f) tc.fireInterval = turretFireInterval;

        // Re-enable auto-attack immediately after placement
        if (autoAttack) autoAttack.IsSuppressedByOtherAbilities = false;

        // Placement sound
        if (placeSfx) PlayOneShotAt(tileCenter, placeSfx, placeSfxVolume, placeSfxPitch);
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Marker helpers
    // ══════════════════════════════════════════════════════════════════════════

    void UpdateMarker(Vector3 center)
    {
        aimCenter = center;
        if (!ctx.tileMarkerPrefab) return;

        // Reuse the single marker rather than destroy/recreate every frame
        if (!aimMarker)
        {
            aimMarker = Instantiate(ctx.tileMarkerPrefab, center, Quaternion.identity);
            if (!aimMarker.TryGetComponent<TileMarker>(out var tm))
                tm = aimMarker.AddComponent<TileMarker>();
            tm.Init(999f, ctx.tileSize);   // lives until we destroy it
        }

        // Just move it
        Vector3 pos = center;
        if (ctx.TryGetGroundHeight(center, out float gy, strict: true))
            pos.y = gy + ctx.markerYOffset;
        else
            pos.y = center.y + ctx.markerYOffset;

        aimMarker.transform.position = pos;
    }

    void EndAim(bool clearMarker)
    {
        isAiming = false;
        if (autoAttack) autoAttack.IsSuppressedByOtherAbilities = false;
        if (clearMarker && aimMarker)
        {
            Destroy(aimMarker);
            aimMarker = null;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Aim constraint (range + wall stop) — mirrors BombAbility.ConstrainTarget
    // ══════════════════════════════════════════════════════════════════════════

    Vector3 ConstrainTarget(Vector3 desired)
    {
        Vector3 start = ctx.Snap(transform.position);
        if (ctx.TryGetGroundHeight(start, out float sgy)) start.y = sgy;
        Vector3 startCast = start + Vector3.up * 0.6f;

        // 1) Clamp to max range
        float maxMeters = Mathf.Max(1, maxRangeTiles) * Mathf.Max(0.0001f, ctx.tileSize);
        Vector3 flatTo  = desired; flatTo.y = start.y;
        Vector3 delta   = flatTo - start;
        float   dist    = delta.magnitude;
        if (dist > maxMeters)
        {
            flatTo  = start + delta.normalized * maxMeters;
            desired = new Vector3(flatTo.x, desired.y, flatTo.z);
        }

        // 2) Wall stop
        Vector3 endCast = new Vector3(desired.x, startCast.y, desired.z);
        Vector3 dir     = endCast - startCast;
        float   len     = dir.magnitude;
        if (len > 0.0001f)
        {
            dir /= len;
            if (Physics.Raycast(startCast, dir, out var hit, len, wallMask, QueryTriggerInteraction.Ignore))
            {
                Vector3 backed = hit.point - dir * Mathf.Max(0.01f, wallBackoff);
                desired = ctx.Snap(backed);
            }
        }

        if (ctx.TryGetGroundHeight(desired, out float gy)) desired.y = gy;
        return desired;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Mouse → world tile (mirrors BombAbility.GetMouseSnapTileCenter)
    // ══════════════════════════════════════════════════════════════════════════

    Vector3 GetMouseSnapTileCenter()
    {
        Camera cam  = ctx.aimCamera ? ctx.aimCamera : Camera.main;
        if (!cam) return ctx.Snap(transform.position);

        int mask = (ctx.groundLayer.value == 0) ? ~0 : ctx.groundLayer.value;
        Ray ray  = cam.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out var hit, 500f, mask, QueryTriggerInteraction.Ignore))
        {
            Vector3 snapped = ctx.Snap(hit.point);
            if (ctx.TryGetGroundHeight(snapped, out float gy)) snapped.y = gy;
            return snapped;
        }

        Vector3 fallback = ctx.Snap(transform.position);
        if (ctx.TryGetGroundHeight(fallback, out float gy2)) fallback.y = gy2;
        return fallback;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Charge management
    // ══════════════════════════════════════════════════════════════════════════

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

    // ══════════════════════════════════════════════════════════════════════════
    //  Audio
    // ══════════════════════════════════════════════════════════════════════════

    void PlayOneShotAt(Vector3 pos, AudioClip clip, float volume, float pitch)
    {
        if (!clip) return;
        var go = new GameObject("OneShotAudio_TurretPlace");
        go.transform.position = pos;
        var a = go.AddComponent<AudioSource>();
        a.clip         = clip;
        a.volume       = Mathf.Clamp01(volume);
        a.pitch        = Mathf.Clamp(pitch, 0.25f, 2f);
        a.spatialBlend = 1f;
        a.rolloffMode  = AudioRolloffMode.Linear;
        a.maxDistance  = 30f;
        a.Play();
        Destroy(go, clip.length / Mathf.Max(0.01f, a.pitch) + 0.1f);
    }
}