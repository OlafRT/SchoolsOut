using System.Collections;
using UnityEngine;

/// <summary>
/// Manages the diving board spectacle:
///   1. An NPC is spawned and walks to the board steps via waypoints.
///   2. The board bounces (Z-axis rotation: tip bends down then springs up).
///   3. The NPC is "launched" — its NPC scripts are enabled and it drops into the arena.
///
/// SETUP:
///   1. Assign boardTransform (the rotating board mesh/object).
///   2. Assign boardWaypoints — path to the tip of the board, in order.
///   3. Assign launchPoint — empty Transform at the end of the board.
///   4. Assign npcPrefab — the NPC to spawn (same NPC system as cafeteria minions).
///   5. PrincipalBoss calls Trigger() when it wants to summon via the board.
/// </summary>
public class DivingBoardSequence : MonoBehaviour
{
    [Header("Board")]
    [Tooltip("The board mesh Transform — rotated on Z to simulate bending.")]
    public Transform boardTransform;
    [Tooltip("Z rotation when the board is flat/resting.")]
    public float boardRestZ    =  0f;
    [Tooltip("Z rotation at maximum downward bend (weight on tip).")]
    public float boardBentDownZ = 10f;
    [Tooltip("Z rotation at maximum upward spring (after launch).")]
    public float boardSpringUpZ = -8f;
    [Tooltip("Number of bounce cycles before launching.")]
    public int bounceCycles    = 3;
    [Tooltip("Seconds per half-bounce (down or up).")]
    public float bounceHalfSeconds = 0.18f;

    [Header("NPC Path")]
    [Tooltip("Waypoints the NPC walks through to reach the board tip, in order.")]
    public Transform[] boardWaypoints;
    [Tooltip("Walk speed to the board.")]
    public float walkSpeed = 2.0f;
    [Tooltip("Transform at the board tip — NPC stands here before bouncing.")]
    public Transform launchPoint;

    [Header("NPC")]
    [Tooltip("NPC prefab to spawn — NPC scripts should start DISABLED (same as slimes).")]
    public GameObject npcPrefab;
    [Tooltip("Where the NPC spawns (e.g. top of the steps).")]
    public Transform npcSpawnPoint;

    [Header("Launch")]
    [Tooltip("Where the NPC should land in the arena. The arc is computed to hit this exactly.")]
    public Transform landingTarget;
    [Tooltip("Peak height of the arc above the straight line from board to landing.")]
    public float arcHeight = 3.5f;
    [Tooltip("Seconds the NPC is in the air. Longer = slower, more dramatic arc.")]
    public float airTime = 0.9f;

    bool _busy;

    // ──────────────────────────────────────────
    /// <summary>Called by PrincipalBoss to run the full sequence.</summary>
    public void Trigger()
    {
        if (_busy || !npcPrefab) return;
        StartCoroutine(BoardSequence());
    }

    // ──────────────────────────────────────────
    IEnumerator BoardSequence()
    {
        _busy = true;

        // Spawn NPC at the top of the steps
        Transform spawnAt = npcSpawnPoint ? npcSpawnPoint : transform;
        // Instantiate inactive so Awake/OnEnable run after we set the name.
        // This prevents NameplateSpawner reading "PrefabName(Clone)" on OnEnable.
        npcPrefab.SetActive(false);
        var npcGO = Instantiate(npcPrefab, spawnAt.position, spawnAt.rotation);
        npcPrefab.SetActive(true);
        npcGO.name = npcPrefab.name; // clean name before anything reads it
        npcGO.SetActive(true);

        // Disable NPC AI components while we drive it manually
        SetNPCEnabled(npcGO, false);

        // ── Walk to board tip via waypoints ───
        if (boardWaypoints != null)
        {
            foreach (var wp in boardWaypoints)
            {
                if (!wp || !npcGO) break;
                yield return StartCoroutine(WalkTo(npcGO, wp.position));
            }
        }

        if (launchPoint && npcGO)
            yield return StartCoroutine(WalkTo(npcGO, launchPoint.position));

        // ── Board bouncing ────────────────────
        yield return StartCoroutine(BounceBoard(npcGO));

        // ── Launch arc ────────────────────────
        if (npcGO)
            yield return StartCoroutine(LaunchNPC(npcGO));

        _busy = false;
    }

    // ──────────────────────────────────────────
    IEnumerator WalkTo(GameObject npcGO, Vector3 target)
    {
        // Start walk animation — Animator is typically on the mesh child, not the root
        var anim = npcGO ? npcGO.GetComponentInChildren<Animator>() : null;
        if (anim) anim.SetFloat("Speed01", 1f);

        // Walk in full 3D — preserve Y so the NPC climbs steps correctly.
        while (npcGO && Vector3.Distance(npcGO.transform.position, target) > 0.1f)
        {
            Vector3 dir = target - npcGO.transform.position;

            // Face the horizontal direction of travel
            Vector3 flatDir = new Vector3(dir.x, 0f, dir.z);
            if (flatDir.sqrMagnitude > 0.001f)
                npcGO.transform.rotation = Quaternion.Slerp(npcGO.transform.rotation,
                    Quaternion.LookRotation(flatDir.normalized, Vector3.up),
                    1f - Mathf.Exp(-12f * Time.deltaTime));

            npcGO.transform.position = Vector3.MoveTowards(
                npcGO.transform.position, target, walkSpeed * Time.deltaTime);

            yield return null;
        }

        if (npcGO)
        {
            npcGO.transform.position = target;
            // Stop walk animation
            if (anim) anim.SetFloat("Speed01", 0f);
        }
    }

    // ──────────────────────────────────────────
    IEnumerator BounceBoard(GameObject npcGO)
    {
        if (!boardTransform) yield break;

        // Parent the NPC to the board tip so it physically rides the rotation.
        // We store the original parent to restore it after launch.
        Transform originalParent = npcGO ? npcGO.transform.parent : null;
        if (npcGO) npcGO.transform.SetParent(launchPoint ? launchPoint : boardTransform, worldPositionStays: true);

        var bounceAnim = npcGO ? npcGO.GetComponentInChildren<Animator>() : null;

        for (int i = 0; i < bounceCycles; i++)
        {
            yield return StartCoroutine(RotateBoardZ(boardRestZ, boardBentDownZ, bounceHalfSeconds));
            // Fire jump animation once at the first spring-up — animator chain handles the rest
            if (i == 0 && bounceAnim) bounceAnim.SetTrigger("Jump");
            yield return StartCoroutine(RotateBoardZ(boardBentDownZ, boardSpringUpZ, bounceHalfSeconds));
            yield return StartCoroutine(RotateBoardZ(boardSpringUpZ, boardRestZ, bounceHalfSeconds));
        }

        // Final big press for launch
        yield return StartCoroutine(RotateBoardZ(boardRestZ, boardBentDownZ, bounceHalfSeconds));

        // Fire the jump animation exactly as the board springs up — syncs with the launch moment
        if (npcGO)
        {
            var anim = npcGO.GetComponentInChildren<Animator>();
            if (anim) anim.SetTrigger("Jump");
        }

        yield return StartCoroutine(RotateBoardZ(boardBentDownZ, boardSpringUpZ, bounceHalfSeconds * 0.5f));
        yield return StartCoroutine(RotateBoardZ(boardSpringUpZ, boardRestZ, bounceHalfSeconds));

        // Unparent before the arc so the NPC moves freely
        if (npcGO) npcGO.transform.SetParent(originalParent, worldPositionStays: true);
    }

    IEnumerator RotateBoardZ(float fromZ, float toZ, float duration)
    {
        if (!boardTransform) yield break;
        float t = 0f;
        Vector3 euler = boardTransform.localEulerAngles;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.001f, duration);
            float z = Mathf.Lerp(fromZ, toZ, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
            boardTransform.localEulerAngles = new Vector3(euler.x, euler.y, z);
            yield return null;
        }
        boardTransform.localEulerAngles = new Vector3(euler.x, euler.y, toZ);
    }

    // ──────────────────────────────────────────
    IEnumerator LaunchNPC(GameObject npcGO)
    {
        if (!npcGO) yield break;



        // Start arc from the exact launchPoint position (board tip Y included)
        Vector3 from = launchPoint ? launchPoint.position : npcGO.transform.position;
        Vector3 to   = landingTarget ? landingTarget.position : from + transform.forward * 5f;

        // Snap NPC to the launch point before starting
        npcGO.transform.position = from;

        float elapsed = 0f;
        while (elapsed < airTime && npcGO)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / airTime);

            Vector3 pos = Vector3.Lerp(from, to, t);
            pos.y = Mathf.Lerp(from.y, to.y, t) + arcHeight * Mathf.Sin(t * Mathf.PI);

            Vector3 vel = pos - npcGO.transform.position;
            if (vel.sqrMagnitude > 0.0001f)
                npcGO.transform.rotation = Quaternion.Slerp(npcGO.transform.rotation,
                    Quaternion.LookRotation(vel.normalized, Vector3.up),
                    1f - Mathf.Exp(-10f * Time.deltaTime));

            npcGO.transform.position = pos;
            yield return null;
        }

        if (npcGO)
        {
            npcGO.transform.position = to;
            Vector3 e = npcGO.transform.eulerAngles;
            npcGO.transform.eulerAngles = new Vector3(0f, e.y, 0f);

            // Clear any unconsumed Jump trigger so it doesn't fire again on landing
            var anim = npcGO.GetComponentInChildren<Animator>();
            if (anim) anim.ResetTrigger("Jump");
        }

        if (npcGO) SetNPCEnabled(npcGO, true);
    }

    // ──────────────────────────────────────────
    void SetNPCEnabled(GameObject go, bool enabled)
    {
        foreach (var comp in new System.Type[]
        {
            typeof(NPCAI), typeof(NPCMovement), typeof(NPCHealth),
            typeof(NPCAutoAttack), typeof(NPCBombAbility)
        })
        {
            var mb = go.GetComponent(comp) as MonoBehaviour;
            if (mb) mb.enabled = enabled;
        }
    }

    void OnDrawGizmos()
    {
        // Preview the launch arc in the scene view
        if (!launchPoint || !landingTarget) return;

        Vector3 from = launchPoint.position;
        Vector3 to   = landingTarget.position;

        Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.9f);
        Vector3 prev = from;
        int steps = 24;
        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps;
            Vector3 p = Vector3.Lerp(from, to, t);
            p.y = Mathf.Lerp(from.y, to.y, t) + arcHeight * Mathf.Sin(t * Mathf.PI);
            Gizmos.DrawLine(prev, p);
            prev = p;
        }

        Gizmos.color = new Color(0.3f, 1f, 0.3f, 0.9f);
        Gizmos.DrawWireSphere(landingTarget.position, 0.3f);
    }
}
