using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float sprintMultiplier = 1.5f;
    public bool hasRunningShoes = false;

    [Header("Grid Settings")]
    [SerializeField] private float tileSize = 1f;
    public LayerMask obstacleLayer;

    [Header("Animation")]
    public Animator animator;

    // NEW: mouse snap-aim
    [Header("Mouse Aim (8-way)")]
    [Tooltip("Hold Right Mouse to face the cursor snapped to 8 directions.")]
    public bool enableMouseSnapAim = true;
    public KeyCode aimMouseButton = KeyCode.Mouse1;
    public Camera cameraForAim; // if null, falls back to Camera.main

    private bool isMoving = false;
    private Vector3 targetPosition;
    private Vector3 lastPosition;

    // Optional movement lock (unchanged)
    public bool canMove = true;
    private float moveCooldown = 0.2f;

    // Cache last facing (useful if cursor is on top of player)
    private Vector3 lastFacing = Vector3.forward;

    // ---------- NEW: Aura slow ----------
    // Multipliers from different sources (e.g., stink field). Use the MIN (strongest slow).
    private readonly Dictionary<object, float> auraMultipliers = new Dictionary<object, float>(); // value in [0..1]
    private float EffectiveSpeedMultiplier
    {
        get
        {
            float mult = 1f;
            foreach (var kv in auraMultipliers)
                mult = Mathf.Min(mult, Mathf.Clamp01(kv.Value));
            return mult;
        }
    }
    public void SetAuraMultiplier(object source, float multiplier01)
    {
        auraMultipliers[source] = Mathf.Clamp01(multiplier01);
    }
    public void ClearAura(object source)
    {
        if (auraMultipliers.ContainsKey(source)) auraMultipliers.Remove(source);
    }
    // -----------------------------------

    void Start()
    {
        if (!cameraForAim) cameraForAim = Camera.main;
        transform.position = RoundToNearestTile(transform.position);
        targetPosition = transform.position;
    }

    void Update()
    {
        // --- NEW: Right-click snap-aim (runs even when not moving) ---
        if (enableMouseSnapAim && Input.GetKey(aimMouseButton))
        {
            Vector3 aimDir = GetMouseAimDir8();
            if (aimDir.sqrMagnitude > 0.1f)
            {
                transform.rotation = Quaternion.LookRotation(aimDir, Vector3.up);
                lastFacing = aimDir;
            }
        }

        if (!(canMove && !isMoving)) return;

        Vector3 direction = Vector3.zero;

        // Input (allows diagonals)
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) direction += Vector3.forward;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) direction += Vector3.back;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) direction += Vector3.left;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) direction += Vector3.right;

        // Rotate to movement dir only if not currently aiming
        if (direction != Vector3.zero && !(enableMouseSnapAim && Input.GetKey(aimMouseButton)))
        {
            // Snap movement-facing to 8-way too (prevents tiny jitter)
            Vector3 snappedMoveDir = SnapDirTo8(direction);
            transform.rotation = Quaternion.LookRotation(snappedMoveDir, Vector3.up);
            lastFacing = snappedMoveDir;
        }

        // Apply sprint and aura slow
        float currentSpeed = moveSpeed;
        if (hasRunningShoes && Input.GetKey(KeyCode.LeftShift))
            currentSpeed *= sprintMultiplier;
        currentSpeed *= EffectiveSpeedMultiplier; // ← slow applied here

        if (direction != Vector3.zero)
        {
            direction = direction.normalized * tileSize;
            Vector3 desiredPosition = RoundToNearestTile(transform.position + direction);

            if (!Physics.Raycast(transform.position, direction, tileSize, obstacleLayer))
                StartCoroutine(MoveToPosition(direction, currentSpeed));
        }

        animator.SetFloat("Speed",
            direction.magnitude * (hasRunningShoes && Input.GetKey(KeyCode.LeftShift) ? sprintMultiplier : 1) * EffectiveSpeedMultiplier);
    }

    private IEnumerator MoveToPosition(Vector3 direction, float speed)
    {
        isMoving = true;
        targetPosition = RoundToNearestTile(transform.position + direction);

        while (Vector3.Distance(transform.position, targetPosition) > 0.01f)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);
            yield return null;
        }

        transform.position = targetPosition;
        isMoving = false;
        lastPosition = targetPosition;
    }

    private Vector3 RoundToNearestTile(Vector3 position)
    {
        return new Vector3(
            Mathf.Round(position.x / tileSize) * tileSize,
            position.y,
            Mathf.Round(position.z / tileSize) * tileSize
        );
    }

    public void ResetMovementState(Vector3 newPosition)
    {
        isMoving = false;
        targetPosition = RoundToNearestTile(newPosition);
        lastPosition = targetPosition;
        StartCoroutine(TeleportCooldown());
    }

    public void RebaseTo(Vector3 worldPos, bool withCooldown = false)
    {
        // Cancel any step/coroutine intent
        isMoving = false;

        // Snap & place
        transform.position = RoundToNearestTile(worldPos);
        targetPosition = transform.position;
        lastPosition = targetPosition;

        if (withCooldown)
        {
            StopAllCoroutines(); // ensure no lingering coroutines
            StartCoroutine(TeleportCooldown()); // the old behavior
        }
        else
        {
            // Immediate control (no "stun" pause)
            canMove = true;
        }
    }

    private IEnumerator TeleportCooldown()
    {
        canMove = false;
        yield return new WaitForSeconds(moveCooldown);
        canMove = true;
    }

    public Vector3 GetLastPosition() => lastPosition;

    // ----------------- NEW HELPERS -----------------

    private Vector3 GetMouseAimDir8()
    {
        if (!cameraForAim) return lastFacing;

        Ray ray = cameraForAim.ScreenPointToRay(Input.mousePosition);
        Plane plane = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));
        if (!plane.Raycast(ray, out float dist)) return lastFacing;

        Vector3 hit = ray.GetPoint(dist);
        Vector3 v = hit - transform.position;
        v.y = 0f;

        if (v.sqrMagnitude < 0.0001f) return lastFacing;

        return SnapDirTo8(v);
    }

    private static Vector3 SnapDirTo8(Vector3 v)
    {
        if (v.sqrMagnitude < 0.0001f) return Vector3.zero;
        float ang = Mathf.Atan2(v.z, v.x) * Mathf.Rad2Deg;
        if (ang < 0f) ang += 360f;
        int step = Mathf.RoundToInt(ang / 45f) % 8;

        switch (step)
        {
            case 0:  return new Vector3( 1,0, 0);
            case 1:  return new Vector3( 1,0, 1).normalized;
            case 2:  return new Vector3( 0,0, 1);
            case 3:  return new Vector3(-1,0, 1).normalized;
            case 4:  return new Vector3(-1,0, 0);
            case 5:  return new Vector3(-1,0,-1).normalized;
            case 6:  return new Vector3( 0,0,-1);
            default: return new Vector3( 1,0,-1).normalized;
        }
    }
}