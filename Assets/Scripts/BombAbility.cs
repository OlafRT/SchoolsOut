using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class BombAbility : MonoBehaviour, IAbilityUI, IClassRestrictedAbility, IChargeableAbility
{
    [Header("Learned Gate")]
    public string bombAbilityName = "Bomb";

    [Header("Input")]
    public KeyCode bombKey = KeyCode.E;

    [Header("Bomb Throw")]
    public int initialImpactDamage = 0;          // optional burst on landing
    public float bombThrowTimePerTile = 0.08f;
    public float bombArcHeight = 1.5f;
    public GameObject bombPrefab;
    public GameObject explosionVfxPrefab;

    [Header("Aim Constraints")]
    [Tooltip("Max throw distance in tiles from the player.")]
    public int maxRangeTiles = 12;

    [Tooltip("Layers considered solid for line-of-sight (e.g., 'Wall').")]
    public LayerMask wallMask;

    [Tooltip("Small backoff distance (meters) from a blocking wall when snapping the telegraph.")]
    public float wallBackoff = 0.2f;

    [Header("Impact SFX")]                       // <<< NEW
    [SerializeField] private AudioClip impactSfx;
    [SerializeField, Range(0f, 2f)] private float impactVolume = 1f;
    [SerializeField, Range(0.25f, 2f)] private float impactPitch = 1f;

    [Header("Impact Shake")]
    [SerializeField] private bool  shakeOnImpact   = true;
    [SerializeField] private float shakeDuration   = 0.12f;
    [SerializeField] private float shakeAmplitude  = 0.35f;

    [Header("Lingering AoE")]
    public int bombRadiusTiles = 1;
    public float lingerDuration = 3f;
    public float tickInterval = 0.5f;
    public int tickDamage = 5;
    [Range(0f, 0.95f)] public float slowPercent = 0.4f;
    public float slowDuration = 1.0f; // kept for compatibility (not used by aura)

    [Header("Status UI")]
    public string slowStatusTag = "Slow";
    public Sprite slowStatusIcon;

    [Header("Charges")]
    public int maxCharges = 2;
    public float rechargeSeconds = 8f;

    [Header("UI")]
    public Sprite icon;

    [Header("Animation")]
    [SerializeField] private Animator animator;          // auto-found in Awake if left null
    [SerializeField] private string bombTrigger = "Bomb";
    [SerializeField] private float eventFailSafeSeconds = 1.0f;

    private PlayerAbilities ctx;
    private AutoAttackAbility autoAttack;

    // aiming/markers
    private bool isAiming;
    private Vector3 aimCenter;
    private readonly List<GameObject> markers = new();

    // charge state
    private int currentCharges;
    private float nextChargeReadyTime = 0f;

    // pending throw (armed at click; executed by animation event)
    private struct PendingThrow { public bool armed; public Vector3 target; public float timeoutAt; }
    private PendingThrow pending;

    public AbilityClassRestriction AllowedFor => AbilityClassRestriction.Nerd;

    void Awake()
    {
        ctx = GetComponent<PlayerAbilities>();
        autoAttack = GetComponent<AutoAttackAbility>();
        if (!animator) animator = GetComponentInChildren<Animator>(true);

        currentCharges = Mathf.Max(1, maxCharges);
        if (currentCharges < maxCharges)
            nextChargeReadyTime = Time.time + Mathf.Max(0.01f, rechargeSeconds);

        // Default to "Wall" if left empty
        if (wallMask.value == 0)
            wallMask = LayerMask.GetMask("Wall");
    }

    void Update()
    {
        if (!IsLearned) return;

        TickRecharge();
        FailSafeIfEventMissed();

        if (!isAiming)
        {
            if (currentCharges > 0 && Input.GetKeyDown(bombKey))
            {
                isAiming = true;
                if (autoAttack) autoAttack.IsSuppressedByOtherAbilities = true;
                var seed = ConstrainTarget(GetMouseSnapTileCenter());
                UpdateMarkers(seed);
            }
            return;
        }

        // Aiming
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1)) { EndAim(true); return; }

        Vector3 cur = ConstrainTarget(GetMouseSnapTileCenter());
        if ((cur - aimCenter).sqrMagnitude > 0.0001f) UpdateMarkers(cur);

        if (Input.GetMouseButtonDown(0))
        {
            if (currentCharges <= 0) return;

            // Spend immediately to feel responsive
            SpendCharge();

            // Arm pending throw for the animation event
            pending.armed     = true;
            pending.target    = aimCenter;
            pending.timeoutAt = Time.time + eventFailSafeSeconds;

            // Leave auto-attack suppressed until release; clear markers and exit aim
            foreach (var m in markers) if (m) Destroy(m); markers.Clear();
            isAiming = false;

            // Trigger the Bomb animation
            if (animator && !string.IsNullOrEmpty(bombTrigger))
                animator.SetTrigger(bombTrigger);
        }
    }

    // ---- Animation Event (called via NerdAnimRelay on the Animator object) ----
    public void AnimEvent_ReleaseBomb()
    {
        if (!pending.armed) return;

        // Allow auto-attack again at the moment we actually throw
        if (autoAttack) autoAttack.IsSuppressedByOtherAbilities = false;

        StartCoroutine(ThrowBombAndSpawnField(pending.target));
        pending.armed = false;
    }

    void FailSafeIfEventMissed()
    {
        if (pending.armed && Time.time >= pending.timeoutAt)
        {
            Debug.LogWarning("BombAbility: Bomb animation event missed—firing fail-safe.");
            AnimEvent_ReleaseBomb();
        }
    }

    // ---- Charges ----
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

    void EndAim(bool clear)
    {
        isAiming = false;
        if (autoAttack) autoAttack.IsSuppressedByOtherAbilities = false;
        if (clear) { foreach (var m in markers) if (m) Destroy(m); markers.Clear(); }
    }

    // ---- Throw + Field ----
    IEnumerator ThrowBombAndSpawnField(Vector3 targetCenter)
    {
        if (!bombPrefab) yield break;

        Vector3 start = ctx.Snap(transform.position) + Vector3.up * 0.5f;
        Vector3 end = targetCenter + Vector3.up * 0.5f;
        float distTiles = Mathf.Max(1f, Vector3.Distance(start, end) / Mathf.Max(0.0001f, ctx.tileSize));
        float duration = bombThrowTimePerTile * distTiles;

        var bomb = Instantiate(bombPrefab, start, Quaternion.identity);

        float t = 0f;
        Vector3 flatStart = new Vector3(start.x, 0f, start.z);
        Vector3 flatEnd = new Vector3(end.x, 0f, end.z);
        float flatDist = Vector3.Distance(flatStart, flatEnd);
        float arc = bombArcHeight + 0.15f * flatDist;

        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.0001f, duration);
            float u = Mathf.Clamp01(t);
            Vector3 pos = Vector3.Lerp(start, end, u);
            pos.y = Mathf.Lerp(start.y, end.y, u) + (-4f * arc * (u * u - u));
            bomb.transform.position = pos;
            yield return null;
        }

        // Impact: VFX + SFX
        if (explosionVfxPrefab)
            Instantiate(explosionVfxPrefab, targetCenter + Vector3.up * 0.02f, Quaternion.identity);

        // sound
        PlayOneShotAt(targetCenter, impactSfx, impactVolume, impactPitch);

        // screen shake
        if (shakeOnImpact)
            CameraShaker.Instance?.Shake(shakeDuration, shakeAmplitude);

        Destroy(bomb);

        if (initialImpactDamage > 0)
        {
            int impact = ctx.stats ? ctx.stats.ComputeDamage(initialImpactDamage, PlayerStats.AbilitySchool.Nerd, true, out _) : initialImpactDamage;
            foreach (var c in ctx.GetDiamondTiles(targetCenter, bombRadiusTiles))
                ctx.DamageTileScaled(c, ctx.tileSize * 0.45f, impact, PlayerStats.AbilitySchool.Nerd, false);
        }

        // Dynamically create the field and pass the icon/tag
        var go = new GameObject("BombAoEField");
        var field = go.AddComponent<BombAoEField>();
        field.slowStatusTag = slowStatusTag;
        field.slowStatusIcon = slowStatusIcon;
        field.Init(
            ctx: ctx,
            center: targetCenter,
            radiusTiles: bombRadiusTiles,
            duration: lingerDuration,
            tickInterval: tickInterval,
            tickDamage: tickDamage,
            slowPercent: slowPercent,
            slowDuration: slowDuration
        );
    }

    void UpdateMarkers(Vector3 center)
    {
        aimCenter = center;
        if (!ctx.tileMarkerPrefab) return;

        foreach (var m in markers) if (m) Destroy(m);
        markers.Clear();

        float gy;
        if (!ctx.TryGetGroundHeight(center, out gy, strict: true)) gy = center.y;

        foreach (var c in ctx.GetDiamondTiles(center, bombRadiusTiles))
        {
            Vector3 pos = c;
            pos.y = gy + ctx.markerYOffset;

            var m = Instantiate(ctx.tileMarkerPrefab, pos, Quaternion.identity);
            if (!m.TryGetComponent<TileMarker>(out var tm)) tm = m.AddComponent<TileMarker>();
            tm.Init(999f, ctx.tileSize); // aiming preview; destroyed when we exit aim
            markers.Add(m);
        }
    }

    // Ray straight to ground from camera
    Vector3 GetMouseSnapTileCenter()
    {
        Camera cam = ctx.aimCamera ? ctx.aimCamera : Camera.main;
        if (!cam) return ctx.Snap(transform.position);

        int mask = (ctx.groundLayer.value == 0) ? ~0 : ctx.groundLayer.value;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
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

    Vector3 ConstrainTarget(Vector3 desiredCenter)
    {
        // Player start (snap to grid) and raise a bit to cast through clear space
        Vector3 start = ctx.Snap(transform.position);
        if (ctx.TryGetGroundHeight(start, out float sgy)) start.y = sgy;
        Vector3 startCast = start + Vector3.up * 0.6f;

        // 1) Clamp by max range (tiles)
        float maxMeters = Mathf.Max(1, maxRangeTiles) * Mathf.Max(0.0001f, ctx.tileSize);
        Vector3 flatTo = desiredCenter; flatTo.y = start.y;
        Vector3 delta = flatTo - start;
        float dist = delta.magnitude;
        if (dist > maxMeters)
        {
            flatTo = start + delta.normalized * maxMeters;
            desiredCenter = new Vector3(flatTo.x, desiredCenter.y, flatTo.z);
        }

        // 2) Stop at walls (linecast from player to candidate)
        // Cast slightly above the ground to avoid floor hits
        Vector3 endCast = new Vector3(desiredCenter.x, startCast.y, desiredCenter.z);
        Vector3 dir = (endCast - startCast);
        float len = dir.magnitude;
        if (len > 0.0001f)
        {
            dir /= len;
            if (Physics.Raycast(startCast, dir, out var hit, len, wallMask, QueryTriggerInteraction.Ignore))
            {
                // Back off a little from the wall and snap to grid
                Vector3 backed = hit.point - dir * Mathf.Max(0.01f, wallBackoff);
                desiredCenter = ctx.Snap(backed);
            }
        }

        // Snap to ground height for the final center
        if (ctx.TryGetGroundHeight(desiredCenter, out float gy)) desiredCenter.y = gy;
        return desiredCenter;
    }

    // ---- IAbilityUI ----
    public string AbilityName => bombAbilityName;
    public Sprite Icon => icon;
    public KeyCode Key => bombKey;

    public float CooldownRemaining => (currentCharges > 0) ? 0f : Mathf.Max(0f, nextChargeReadyTime - Time.time);
    public float CooldownDuration => Mathf.Max(0.01f, rechargeSeconds);
    public bool IsLearned => ctx && ctx.HasAbility(bombAbilityName);

    // ---- IChargeableAbility ----
    public int CurrentCharges => currentCharges;
    public int MaxCharges => Mathf.Max(1, maxCharges);

    // ---- simple 3D one-shot helper (like Warp) ----
    void PlayOneShotAt(Vector3 pos, AudioClip clip, float volume, float pitch)
    {
        if (!clip) return;
        var go = new GameObject("OneShotAudio_BombImpact");
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
