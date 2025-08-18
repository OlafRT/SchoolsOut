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

    [Header("Lingering AoE")]
    public int bombRadiusTiles = 1;
    public float lingerDuration = 3f;            // how long the field stays (talent upgradable)
    public float tickInterval = 0.5f;            // how often it ticks
    public int tickDamage = 5;                   // damage per tick
    [Range(0f, 0.95f)]
    public float slowPercent = 0.4f;             // 40% slow
    public float slowDuration = 1.0f;            // applied each tick (refreshes)

    [Header("Charges")]
    public int maxCharges = 2;
    public float rechargeSeconds = 8f;

    [Header("UI")]
    public Sprite icon;

    private PlayerAbilities ctx;
    private AutoAttackAbility autoAttack;

    // aiming/markers
    private bool isAiming;
    private Vector3 aimCenter;
    private readonly List<GameObject> markers = new();

    // charge state
    private int currentCharges;
    private float nextChargeReadyTime = 0f;

    public AbilityClassRestriction AllowedFor => AbilityClassRestriction.Nerd;

    void Awake()
    {
        ctx = GetComponent<PlayerAbilities>();
        autoAttack = GetComponent<AutoAttackAbility>();
        currentCharges = Mathf.Max(1, maxCharges);
        if (currentCharges < maxCharges)
            nextChargeReadyTime = Time.time + Mathf.Max(0.01f, rechargeSeconds);
    }

    void Update()
    {
        if (!IsLearned) return;

        TickRecharge();

        if (!isAiming)
        {
            if (currentCharges > 0 && Input.GetKeyDown(bombKey))
            {
                isAiming = true;
                if (autoAttack) autoAttack.IsSuppressedByOtherAbilities = true;
                UpdateMarkers(GetMouseSnapTileCenter());
            }
            return;
        }

        // Aiming
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1)) { EndAim(true); return; }

        Vector3 cur = GetMouseSnapTileCenter();
        if ((cur - aimCenter).sqrMagnitude > 0.0001f) UpdateMarkers(cur);

        if (Input.GetMouseButtonDown(0))
        {
            Vector3 target = aimCenter;
            EndAim(true);

            if (currentCharges > 0)
            {
                SpendCharge();
                StartCoroutine(ThrowBombAndSpawnField(target));
            }
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

        if (explosionVfxPrefab) Instantiate(explosionVfxPrefab, targetCenter + Vector3.up * 0.02f, Quaternion.identity);
        Destroy(bomb);

        // Optional burst damage on land
        if (initialImpactDamage > 0)
        {
            foreach (var c in ctx.GetDiamondTiles(targetCenter, bombRadiusTiles))
                ctx.DamageTile(c, ctx.tileSize * 0.45f, initialImpactDamage);
        }

        // Spawn the lingering field (handles damage ticks, slow, & telegraph lifespan)
        var go = new GameObject("BombAoEField");
        var field = go.AddComponent<BombAoEField>();
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
        foreach (var m in markers) if (m) Destroy(m); markers.Clear();

        foreach (var c in ctx.GetDiamondTiles(center, bombRadiusTiles))
        {
            Vector3 pos = c;
            if (ctx.TryGetGroundHeight(c, out float gy)) pos.y = gy + ctx.markerYOffset;
            else pos.y = c.y + ctx.markerYOffset;

            var m = Instantiate(ctx.tileMarkerPrefab, pos, Quaternion.identity);
            if (!m.TryGetComponent<TileMarker>(out var tm)) tm = m.AddComponent<TileMarker>();
            tm.Init(999f, ctx.tileSize); // aiming preview; destroyed on EndAim
            markers.Add(m);
        }
    }

    Vector3 GetMouseSnapTileCenter()
    {
        Camera cam = ctx.aimCamera ? ctx.aimCamera : Camera.main;
        if (!cam) return ctx.Snap(transform.position);
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        Plane plane = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));
        if (!plane.Raycast(ray, out float dist)) return ctx.Snap(transform.position);
        Vector3 hit = ray.GetPoint(dist);
        Vector3 snapped = ctx.Snap(hit);
        if (ctx.TryGetGroundHeight(snapped, out float gy)) snapped.y = gy;
        return snapped;
    }

    // ---- IAbilityUI ----
    public string AbilityName => bombAbilityName;
    public Sprite Icon => icon;
    public KeyCode Key => bombKey;

    // Charges-aware cooldown readout for the slot overlay
    public float CooldownRemaining => (currentCharges > 0) ? 0f : Mathf.Max(0f, nextChargeReadyTime - Time.time);
    public float CooldownDuration => Mathf.Max(0.01f, rechargeSeconds);
    public bool IsLearned => ctx && ctx.HasAbility(bombAbilityName);

    // ---- IChargeableAbility ----
    public int CurrentCharges => currentCharges;
    public int MaxCharges => Mathf.Max(1, maxCharges);
}
