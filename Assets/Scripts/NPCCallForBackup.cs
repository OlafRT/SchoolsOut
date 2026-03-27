using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Attach to a Girl-faction NPC alongside NPCAI, NPCMovement, and NPCHealth.
///
/// Behaviour sequence:
///   1. While hostile, periodically checks if she is "losing":
///      • she is below selfLowHPPercent, OR
///      • there are fewer than healthyAllyMinimum healthy same-faction allies nearby.
///   2. On trigger: pauses NPCAI, flees ~fleeDistanceTiles tiles directly away from
///      the player (fans out 45° at a time until a path is found).
///   3. Once arrived: sets the IsOnPhone animator bool and waits for the OnPhoneCallComplete animation event.
///   4. Spawns backupNPCPrefab on a free tile nearby.
///   5. Optionally re-enables NPCAI so she can resume the fight.
/// </summary>
[DisallowMultipleComponent]
public class NPCCallForBackup : MonoBehaviour
{
    // ── Faction Gate ──────────────────────────────────────────────────────────
    [Header("Faction Gate")]
    [Tooltip("Only NPCs matching this faction will use the ability.")]
    public NPCFaction requiredFaction = NPCFaction.Girl;

    // ── Trigger Conditions ────────────────────────────────────────────────────
    [Header("Trigger Conditions")]
    [Tooltip("World-unit sphere radius used to scan for same-faction allies.")]
    public float allyCheckRadius = 8f;

    [Tooltip("HP % at or below which this NPC considers herself low HP.")]
    [Range(0f, 1f)] public float selfLowHPPercent = 0.45f;

    [Tooltip("HP % at or below which a nearby ally is counted as 'low HP' (and NOT as a healthy ally).")]
    [Range(0f, 1f)] public float allyLowHPPercent = 0.40f;

    [Tooltip("If fewer than this many healthy allies are nearby, she considers herself alone/losing.")]
    public int healthyAllyMinimum = 1;

    [Tooltip("Seconds between condition re-checks. Keeps it from firing the instant combat starts.")]
    public float conditionCheckInterval = 1.5f;

    // ── Flee ──────────────────────────────────────────────────────────────────
    [Header("Flee")]
    [Tooltip("How many tiles away from the player to run before calling backup.")]
    public int fleeDistanceTiles = 20;

    [Tooltip("How many direction attempts (fanning 45° each time) to try when looking for a flee path.")]
    public int fleeDirectionAttempts = 8;

    [Tooltip("Speed multiplier applied to the mover while fleeing (uses the same system as NPCAI chase speed).")]
    public float fleeSpeedMultiplier = 2.5f;

    [Tooltip("Seconds to wait for movement to finish before giving up and aborting.")]
    public float fleeTimeoutSeconds = 20f;

    // ── Phone Animation ───────────────────────────────────────────────────────
    [Header("Phone Animation")]
    [Tooltip("Animator BOOL parameter that drives the phone-call state. "
           + "Set to true to enter, false to cancel. Add an Animation Event on the "
           + "last frame of the clip that calls OnPhoneCallComplete().")]
    public string phoneBoolName = "IsOnPhone";

    [Tooltip("If she takes any damage during the phone call, cancel it and resume fighting.")]
    public bool cancelOnHit = true;

    // ── Sounds ────────────────────────────────────────────────────────────────
    [Header("Sounds")]
    [Tooltip("Played the moment she starts running away (e.g. a scared scream or 'Help!').")]
    public AudioClip fleeSound;
    [Range(0f, 1f)] public float fleeSoundVolume = 1f;

    [Tooltip("Played when she raises the phone to call for backup.")]
    public AudioClip phoneSound;
    [Range(0f, 1f)] public float phoneSoundVolume = 1f;

    // ── Backup Spawn ──────────────────────────────────────────────────────────
    [Header("Backup Spawn")]
    [Tooltip("The NPC prefab to instantiate after the phone call. Must have NPCAI etc. set up.")]
    public GameObject backupNPCPrefab;

    [Tooltip("Cardinal/diagonal tile distance from this NPC to try when placing the backup.")]
    public int spawnOffsetTiles = 3;

    // ── Internal refs ─────────────────────────────────────────────────────────
    NPCAI          ai;
    NPCMovement    mover;
    NPCHealth      myHealth;
    GridPathfinder pathfinder;
    Transform      player;

    bool  abilityUsed    = false; // one-shot per NPC lifetime
    bool  isExecuting    = false;
    float nextCheckTime  = 0f;

    // Animation event / cancellation state
    bool phoneCallComplete = false;
    int  hpAtPhoneStart    = 0;

    // Bug 3 fix: shared across all instances so only ONE girl flees at a time.
    // When any girl registers herself here, all others see a non-zero count and skip.
    static readonly HashSet<NPCCallForBackup> s_currentlyFleeing = new();

    // ─────────────────────────────────────────────────────────────────────────
    void Awake()
    {
        ai         = GetComponent<NPCAI>();
        mover      = GetComponent<NPCMovement>();
        myHealth   = GetComponent<NPCHealth>();

        // Prefer a pathfinder assigned on NPCAI; fall back to scene search.
        pathfinder = ai ? ai.pathfinder : null;
        if (!pathfinder) pathfinder = FindAnyObjectByType<GridPathfinder>();

        var p = GameObject.FindGameObjectWithTag("Player");
        if (p) player = p.transform;
    }

    // ─────────────────────────────────────────────────────────────────────────
    void Update()
    {
        if (abilityUsed || isExecuting)          return;
        if (!ai || !mover || !player)            return;
        if (!backupNPCPrefab)                    return;
        if (ai.faction != requiredFaction)       return;
        if (myHealth && myHealth.IsDead)         return;

        // Only fire when already engaged in combat
        if (ai.CurrentHostility != NPCAI.Hostility.Hostile) return;

        if (Time.time < nextCheckTime) return;
        nextCheckTime = Time.time + conditionCheckInterval;

        // Bug 3 fix: if another girl is already mid-flee, don't pile on.
        if (s_currentlyFleeing.Count > 0) return;

        if (ShouldFlee())
            StartCoroutine(BackupSequence());
    }

    // ── Condition ─────────────────────────────────────────────────────────────
    bool ShouldFlee()
    {
        // Self low HP?
        bool selfLow = myHealth &&
                       (float)myHealth.currentHP / Mathf.Max(1, myHealth.maxHP) <= selfLowHPPercent;

        // Count living same-faction allies with enough HP to matter
        int healthyAllies = 0;
        var hits = Physics.OverlapSphere(transform.position, allyCheckRadius);
        foreach (var col in hits)
        {
            if (col.gameObject == gameObject) continue;

            var allyAI = col.GetComponent<NPCAI>();
            if (!allyAI || allyAI.faction != requiredFaction) continue;

            var allyHP = col.GetComponent<NPCHealth>();
            if (allyHP && allyHP.IsDead) continue;

            float hpPct = allyHP
                ? (float)allyHP.currentHP / Mathf.Max(1, allyHP.maxHP)
                : 1f;

            if (hpPct > allyLowHPPercent)
                healthyAllies++;
        }

        bool aloneOrOutnumbered = healthyAllies < healthyAllyMinimum;

        // Flee if personally low OR no healthy backup is around
        return selfLow || aloneOrOutnumbered;
    }

    // ── Main Sequence ─────────────────────────────────────────────────────────
    IEnumerator BackupSequence()
    {
        isExecuting = true;
        abilityUsed = true;

        // Bug 3 fix: claim the "fleeing" slot so no other girl starts her own flee.
        s_currentlyFleeing.Add(this);

        // ── Step 1: Find a tile ~fleeDistanceTiles away from the player ──
        Vector3 fleeGoal = FindFleeGoal();
        if (fleeGoal == transform.position)
        {
            AbortAndRestoreAI();
            yield break;
        }

        // ── Step 2: Hand the flee goal to NPCAI.
        // NPCAI stays ENABLED — DoHostile() now paths toward fleeGoal instead of
        // the player. UpdateAnimatorLocomotion() keeps running, so IsMoving/IsRunning
        // update every frame and the animator plays the run animation correctly.
        // The NPC looks intentionally scared, not glitched.
        mover.SetExternalSpeedMultiplier(fleeSpeedMultiplier);
        ai.StartFlee(fleeGoal);

        // Play the flee sound at her position the moment she bolts.
        if (fleeSound)
            AudioSource.PlayClipAtPoint(fleeSound, transform.position, fleeSoundVolume);

        // ── Step 3: Wait until she reaches the goal (with safety timeout) ──
        float elapsed = 0f;
        while (elapsed < fleeTimeoutSeconds)
        {
            if (myHealth && myHealth.IsDead)
            {
                ai.StopFlee();
                s_currentlyFleeing.Remove(this);
                yield break;
            }

            // Close enough? Chebyshev within 1.5 tiles counts as arrived.
            float dx = Mathf.Abs(transform.position.x - fleeGoal.x);
            float dz = Mathf.Abs(transform.position.z - fleeGoal.z);
            float t  = mover ? mover.tileSize : 1f;
            if (Mathf.Max(dx, dz) <= t * 1.5f) break;

            elapsed += Time.deltaTime;
            yield return null;
        }

        // ── Step 4: Stop movement and suspend AI decisions.
        // SuspendDecisions() calls mover.HardStop() internally, so IsMoving
        // goes false. UpdateAnimatorLocomotion() is still running each frame and
        // will now set IsMoving=false + IsRunning=false naturally.
        ai.StopFlee();
        ai.SuspendDecisions(true);

        // Give the animator 2 frames to process the locomotion bool change
        // and fully exit the run/walk state before we start the phone call.
        yield return null;
        yield return null;

        if (myHealth && myHealth.IsDead)
        {
            ai.SuspendDecisions(false);
            s_currentlyFleeing.Remove(this);
            yield break;
        }

        // ── Step 5: Play phone-call animation ──
        var anim = (ai && ai.animator) ? ai.animator : GetComponentInChildren<Animator>();

        // Disable root motion so the phone animation can't slide her around.
        bool prevRootMotion = false;
        if (anim) { prevRootMotion = anim.applyRootMotion; anim.applyRootMotion = false; }

        // Use a BOOL (not a trigger) so we can cancel cleanly by setting it false.
        // The animation state machine should transition: Idle -> IsOnPhone=true -> Phone clip.
        // An Animation Event on the last frame of the clip calls OnPhoneCallComplete().
        phoneCallComplete = false;
        hpAtPhoneStart    = myHealth ? myHealth.currentHP : 0;

        if (anim && !string.IsNullOrEmpty(phoneBoolName))
            anim.SetBool(phoneBoolName, true);

        // Play the phone sound in sync with the animation.
        if (phoneSound)
            AudioSource.PlayClipAtPoint(phoneSound, transform.position, phoneSoundVolume);

        // ── Step 6: Wait for the animation event (or cancel on hit / death) ──
        while (!phoneCallComplete)
        {
            if (myHealth && myHealth.IsDead)
            {
                EndPhoneCall(anim, prevRootMotion, spawnBackup: false);
                yield break;
            }

            if (cancelOnHit && myHealth && myHealth.currentHP < hpAtPhoneStart)
            {
                // She was hit — cancel the call, resume fighting immediately.
                EndPhoneCall(anim, prevRootMotion, spawnBackup: false);
                yield break;
            }

            yield return null;
        }

        // Animation event fired — phone call completed successfully.
        EndPhoneCall(anim, prevRootMotion, spawnBackup: true);
    }

    // Called by the animation event on the last frame of the phone clip,
    // AND internally when cancelling. Keeps all the cleanup in one place.
    void EndPhoneCall(Animator anim, bool prevRootMotion, bool spawnBackup)
    {
        // Exit the phone animation state.
        if (anim && !string.IsNullOrEmpty(phoneBoolName))
            anim.SetBool(phoneBoolName, false);

        if (anim) anim.applyRootMotion = prevRootMotion;

        if (spawnBackup)
        {
            // ── Step 7: Spawn the backup NPC ──
            SpawnBackup();
        }

        // ── Step 8: Restore AI ──
        ai.SuspendDecisions(false);
        mover.SetExternalSpeedMultiplier(ai ? ai.chaseSpeedMultiplier : 1f);

        s_currentlyFleeing.Remove(this);
        isExecuting = false;
    }

    /// <summary>
    /// Call this from an Animation Event placed on the LAST frame of the phone
    /// call clip. This is what triggers the backup NPC to spawn.
    /// </summary>
    public void OnPhoneCallComplete()
    {
        phoneCallComplete = true;
    }

    // ── Flee Goal ─────────────────────────────────────────────────────────────
    /// <summary>
    /// Finds a grid tile ~fleeDistanceTiles away, starting directly opposite the
    /// player and rotating 45° outward each attempt until a valid path is found.
    /// Returns transform.position on failure (caller checks for that).
    /// </summary>
    Vector3 FindFleeGoal()
    {
        if (!player) return transform.position;

        Vector3 awayDir = transform.position - player.position;
        awayDir.y = 0f;
        // Edge case: same tile as player
        if (awayDir.sqrMagnitude < 0.001f) awayDir = transform.forward;
        awayDir.Normalize();

        float t    = mover ? mover.tileSize : 1f;
        float dist = fleeDistanceTiles * t;

        for (int i = 0; i < fleeDirectionAttempts; i++)
        {
            // Fan: 0°, +45°, −45°, +90°, −90°, +135°, −135°, 180°
            float angleDeg = i == 0
                ? 0f
                : (i % 2 == 1 ? 1 : -1) * Mathf.CeilToInt(i / 2f) * 45f;

            Vector3 dir       = Quaternion.Euler(0f, angleDeg, 0f) * awayDir;
            Vector3 candidate = SnapToGrid(transform.position + dir * dist, t);

            // Validate: can we actually path there?
            if (pathfinder &&
                pathfinder.TryFindPath(transform.position, candidate, 2000, out _, goalPassable: true))
                return candidate;
        }

        return transform.position; // no valid direction found
    }

    // ── Backup Spawn ──────────────────────────────────────────────────────────
    /// <summary>
    /// Tries to place the backup NPC on an unblocked tile close to this NPC.
    /// Tries cardinals first, then diagonals, then falls back to a raw offset.
    /// </summary>
    void SpawnBackup()
    {
        if (!backupNPCPrefab) return;

        float t = mover ? mover.tileSize : 1f;

        // Ranked offsets: cardinals (cleaner spawns) then diagonals
        Vector2Int[] offsets =
        {
            new( spawnOffsetTiles,  0),
            new(-spawnOffsetTiles,  0),
            new( 0,  spawnOffsetTiles),
            new( 0, -spawnOffsetTiles),
            new( spawnOffsetTiles,  spawnOffsetTiles),
            new(-spawnOffsetTiles,  spawnOffsetTiles),
            new( spawnOffsetTiles, -spawnOffsetTiles),
            new(-spawnOffsetTiles, -spawnOffsetTiles),
        };

        foreach (var o in offsets)
        {
            Vector3 candidate = SnapToGrid(
                transform.position + new Vector3(o.x * t, 0f, o.y * t),
                t
            );

            if (pathfinder && pathfinder.IsBlocked(candidate)) continue;

            var spawned = Instantiate(backupNPCPrefab, candidate, Quaternion.identity);
            spawned.name = $"{gameObject.name}'s Boyfriend";
            return;
        }

        // Last resort: spawn one tile to the right (might briefly overlap, but won't crash)
        var fallback = Instantiate(backupNPCPrefab, transform.position + new Vector3(t, 0f, 0f), Quaternion.identity);
        fallback.name = $"{gameObject.name}'s Boyfriend";
    }

    // ── Abort Helper ─────────────────────────────────────────────────────────
    void AbortAndRestoreAI()
    {
        s_currentlyFleeing.Remove(this);
        if (ai)
        {
            ai.StopFlee();
            ai.SuspendDecisions(false);
            mover.SetExternalSpeedMultiplier(ai.chaseSpeedMultiplier);
        }
        abilityUsed = false; // allow a retry next check
        isExecuting = false;
    }

    void OnDestroy() => s_currentlyFleeing.Remove(this);

    // ── Utility ───────────────────────────────────────────────────────────────
    static Vector3 SnapToGrid(Vector3 p, float t) =>
        new(Mathf.Round(p.x / t) * t, p.y, Mathf.Round(p.z / t) * t);
}