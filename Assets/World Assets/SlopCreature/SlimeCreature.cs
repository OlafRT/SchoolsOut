using UnityEngine;

/// <summary>
/// Companion script for the cafeteria lady's slime minions.
/// Responsibilities:
///   • Auto-configures SlimeTrailEffect (trail is left disabled; SlimeVentEmerge enables it after landing).
///   • Spawns a lingering AoE puddle at the death position.
///
/// The emerge / vent-crawl sequence is handled entirely by SlimeVentEmerge.
/// If SlimeVentEmerge is NOT on the prefab, NPC components are left at whatever
/// state they're in — this script will not interfere.
///
/// REQUIRED COMPANIONS ON SAME GAMEOBJECT:
///   SlimeVentEmerge  (vent crawl + landing + NPC wake-up)
///   SlimeJiggle      (body squish animation)
///   SlimeDeathRelay  (bridges NPCHealth → SlimeJiggle.Die)
///   NPCAI, NPCMovement, NPCHealth, NPCAutoAttack
/// </summary>
[DisallowMultipleComponent]
public class SlimeCreature : MonoBehaviour
{
    [Header("Slime Trail")]
    [Tooltip("Prefab for slime puddles left behind while moving. Needs a flat trigger Collider + visual.")]
    public GameObject slimePuddlePrefab;
    public float puddleDropInterval  = 0.45f;
    public float puddleMinDistance   = 0.60f;
    public float puddleDuration      = 6f;
    public int   puddleTickDamage    = 5;
    public float puddleTickInterval  = 0.5f;
    [Range(0f, 0.95f)]
    public float puddleSlowPercent   = 0.30f;

    [Header("Death Puddle")]
    [Tooltip("Spawn a larger AoE puddle at the death location.")]
    public bool  deathPuddle             = true;
    public int   deathPuddleRadius       = 1;
    public float deathPuddleDuration     = 5f;
    public int   deathPuddleTickDamage   = 8;
    [Range(0f, 0.95f)]
    public float deathPuddleSlowPercent  = 0.40f;
    public GameObject deathPuddleTileMarkerPrefab;
    public float      deathPuddleTileSize      = 1f;
    public float      deathPuddleMarkerYOffset = 0.02f;
    public LayerMask  deathPuddleGroundMask    = ~0;
    public LayerMask  deathPuddleVictimLayer;

    // ───────────────────────────────────────────────
    NPCHealth _health;
    bool      _dead;

    void Awake()
    {
        _health = GetComponent<NPCHealth>();

        SetupTrail();
    }

    void SetupTrail()
    {
        var trail = GetComponent<SlimeTrailEffect>();
        if (!trail) trail = gameObject.AddComponent<SlimeTrailEffect>();

        trail.slimePuddlePrefab         = slimePuddlePrefab;
        trail.dropInterval              = puddleDropInterval;
        trail.minDistanceBetweenPuddles = puddleMinDistance;
        trail.puddleDuration            = puddleDuration;
        trail.puddleTickDamage          = puddleTickDamage;
        trail.puddleTickInterval        = puddleTickInterval;
        trail.puddleSlowPercent         = puddleSlowPercent;
        trail.dropOnStart               = false;

        // Leave disabled — SlimeVentEmerge enables it after the slime lands.
        // If there is no SlimeVentEmerge, enable it manually or leave it enabled in the Inspector.
        if (GetComponent<SlimeVentEmerge>() != null)
            trail.enabled = false;
    }

    // ───────────────────────────────────────────────
    void Update()
    {
        if (_dead) return;
        if (!_health || !_health.IsDead) return;

        _dead = true;
        SpawnDeathPuddle();
    }

    void SpawnDeathPuddle()
    {
        if (!deathPuddle || !deathPuddleTileMarkerPrefab) return;

        var go    = new GameObject("SlimeDeathAoE");
        var field = go.AddComponent<BombAoEFieldHostile>();
        field.Init(
            center:        transform.position,
            tileSize:      deathPuddleTileSize,
            markerPrefab:  deathPuddleTileMarkerPrefab,
            markerYOffset: deathPuddleMarkerYOffset,
            groundMask:    deathPuddleGroundMask,
            victimsLayer:  deathPuddleVictimLayer,
            radiusTiles:   deathPuddleRadius,
            duration:      deathPuddleDuration,
            tickInterval:  0.5f,
            tickDamage:    deathPuddleTickDamage,
            slowPercent:   deathPuddleSlowPercent,
            slowTag:       "Slow",
            slowIcon:      null
        );
    }
}
