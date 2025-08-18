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
    public float lingerDuration = 3f;
    public float tickInterval = 0.5f;
    public int tickDamage = 5;
    [Range(0f, 0.95f)] public float slowPercent = 0.4f;
    public float slowDuration = 1.0f; // kept for compatibility (not used by aura)

    [Header("Status UI")]
    public string slowStatusTag = "Slow";
    public Sprite slowStatusIcon; // <-- assign your snail/snowflake/etc icon here

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

        if (initialImpactDamage > 0)
        {
            foreach (var c in ctx.GetDiamondTiles(targetCenter, bombRadiusTiles))
                ctx.DamageTile(c, ctx.tileSize * 0.45f, initialImpactDamage);
        }

        // Dynamically create the field and pass the icon/tag
        var go = new GameObject("BombAoEField");
        var field = go.AddComponent<BombAoEField>();
        field.slowStatusTag = slowStatusTag;      // <-- pass tag
        field.slowStatusIcon = slowStatusIcon;    // <-- pass icon
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

        // Sample ONE ground Y at the center, then use it for the whole preview
        float gy;
        if (!ctx.TryGetGroundHeight(center, out gy, strict: true)) gy = center.y;

        foreach (var c in ctx.GetDiamondTiles(center, bombRadiusTiles))
        {
            Vector3 pos = c;
            pos.y = gy + ctx.markerYOffset;

            var m = Instantiate(ctx.tileMarkerPrefab, pos, Quaternion.identity);
            if (!m.TryGetComponent<TileMarker>(out var tm)) tm = m.AddComponent<TileMarker>();
            tm.Init(999f, ctx.tileSize); // aiming preview; destroyed on EndAim
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
}
