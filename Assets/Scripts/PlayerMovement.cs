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

    [Header("Collision (anti-phasing)")]
    [Tooltip("Horizontal radius used when testing movement. Increase if you can still squeeze through.")]
    [SerializeField] private float collisionRadius = 0.35f;
    [Tooltip("Small extra distance to keep from touching colliders (prevents grazing).")]
    [SerializeField] private float skin = 0.01f;

    [Header("Animation")]
    public Animator animator;

    // Animator parameter names
    [Header("Animation Parameters")]
    [SerializeField] private string p_IsMoving   = "IsMoving";
    [SerializeField] private string p_IsRunning  = "IsRunning";
    [SerializeField] private string p_Speed01    = "Speed01";
    [SerializeField] private string p_Forward    = "Forward";  // -1..1 (back..forward)
    [SerializeField] private string p_Right      = "Right";    // -1..1 (left..right)
    [SerializeField] private string p_TurnLeft45 = "TurnLeft45";
    [SerializeField] private string p_TurnRight45= "TurnRight45";

    [Header("Mouse Aim (8-way)")]
    [Tooltip("Hold Right Mouse to face the cursor snapped to 8 directions.")]
    public bool enableMouseSnapAim = true;
    public KeyCode aimMouseButton = KeyCode.Mouse1;
    public Camera cameraForAim; // if null, falls back to Camera.main

    [Header("Animation Tuning")]
    [Tooltip("Smoothing of Forward/Right animator floats.")]
    [SerializeField] private float animDamp = 0.08f;
    [Tooltip("Minimum magnitude considered 'moving' (prevents jitter).")]
    [SerializeField] private float moveEpsilon = 0.01f;

    private bool isMoving = false;
    private Vector3 targetPosition;
    private Vector3 lastPosition;

    public bool canMove = true;
    private float moveCooldown = 0.2f;

    private Vector3 lastFacing = Vector3.forward;

    // 8-way yaw index for turn-in-place (0..7, each = 45deg)
    private int lastYawIndex = -1;

    // ---------- Aura slow ----------
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
        => auraMultipliers[source] = Mathf.Clamp01(multiplier01);
    public void ClearAura(object source)
    {
        if (auraMultipliers.ContainsKey(source)) auraMultipliers.Remove(source);
    }

    // --- Resolve animator from PlayerBootstrap if not assigned in the inspector ---
    private void ResolveAnimator()
    {
        if (animator) return;

        var bootstrap = GetComponent<PlayerBootstrap>();
        if (bootstrap && bootstrap.ActiveAnimator)
        {
            animator = bootstrap.ActiveAnimator;
            return;
        }

        // Fallback: find any animator under this player hierarchy.
        animator = GetComponentInChildren<Animator>(true);
    }

    // Optional external setter if you hot-swap models at runtime.
    public void SetAnimator(Animator a) => animator = a;

    void Start()
    {
        if (!cameraForAim) cameraForAim = Camera.main;

        // Ensure animator targets the active class model.
        ResolveAnimator();

        transform.position = RoundToNearestTile(transform.position);
        targetPosition = transform.position;

        // init yaw index for turning-in-place detection
        lastYawIndex = YawIndex8(transform.forward);
        lastFacing = transform.forward;
    }

    void Update()
    {
        // --- RMB snap-aim (updates facing even when not moving) ---
        if (enableMouseSnapAim && Input.GetKey(aimMouseButton))
        {
            Vector3 aimDir = GetMouseAimDir8();
            if (aimDir.sqrMagnitude > 0.1f)
            {
                int newIdx = YawIndex8(aimDir);
                bool standingStill = !isMoving;

                // If we are idle and yaw steps changed, fire 45° turn triggers
                if (standingStill && newIdx != lastYawIndex && animator)
                {
                    int delta = DeltaYawSteps(lastYawIndex, newIdx);
                    if (delta > 0) for (int i = 0; i < delta; i++) animator.SetTrigger(p_TurnRight45);
                    if (delta < 0) for (int i = 0; i < -delta; i++) animator.SetTrigger(p_TurnLeft45);
                }

                transform.rotation = Quaternion.LookRotation(aimDir, Vector3.up);
                lastFacing = aimDir;
                lastYawIndex = newIdx;
            }
        }

        if (!canMove)
        {
            UpdateAnimatorLocomotion(Vector3.zero, false);
            return;
        }

        // --- Input (allows diagonals in world axes) ---
        Vector3 wish = Vector3.zero;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))    wish += Vector3.forward;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))  wish += Vector3.back;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))  wish += Vector3.left;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) wish += Vector3.right;

        // If not aiming, snap facing to the 8-way movement direction
        if (wish != Vector3.zero && !(enableMouseSnapAim && Input.GetKey(aimMouseButton)))
        {
            Vector3 snappedMoveDir = SnapDirTo8(wish);
            transform.rotation = Quaternion.LookRotation(snappedMoveDir, Vector3.up);
            lastFacing = snappedMoveDir;
            lastYawIndex = YawIndex8(snappedMoveDir);
        }

        // Sprint + aura slow
        bool isRunning = hasRunningShoes && Input.GetKey(KeyCode.LeftShift);
        float currentSpeed = moveSpeed * (isRunning ? sprintMultiplier : 1f) * EffectiveSpeedMultiplier;

        // Try to start a step if we have intent
        if (!isMoving && wish != Vector3.zero)
        {
            Vector3 snappedDir = SnapDirTo8(wish).normalized;
            TryStartStep(snappedDir, currentSpeed);
        }

        // Update animator using either the active step direction (if moving) or current wish
        UpdateAnimatorLocomotion(wish, /*wishDirValid*/ wish != Vector3.zero, isRunning);
    }

    // ---------- NEW: robust step start with sphere casts and axis-split for diagonals ----------
    private void TryStartStep(Vector3 snappedDir, float speed)
    {
        Vector3 moveDelta = snappedDir * tileSize;

        // 1) try full intended move
        if (IsStepClear(moveDelta))
        {
            StartCoroutine(MoveToPosition(moveDelta, speed));
            return;
        }

        // 2) if diagonal, try sliding along X or Z separately (prevents diagonal phasing)
        bool diagonal = Mathf.Abs(snappedDir.x) > 0f && Mathf.Abs(snappedDir.z) > 0f;
        if (diagonal)
        {
            Vector3 dx = new Vector3(Mathf.Sign(snappedDir.x), 0f, 0f) * tileSize;
            Vector3 dz = new Vector3(0f, 0f, Mathf.Sign(snappedDir.z)) * tileSize;

            bool xClear = IsStepClear(dx);
            bool zClear = IsStepClear(dz);

            if (xClear && !zClear) { StartCoroutine(MoveToPosition(dx, speed)); return; }
            if (!xClear && zClear) { StartCoroutine(MoveToPosition(dz, speed)); return; }

            // If both clear we prefer the intended diag (already failed), else neither clear -> blocked.
        }
        // blocked – do nothing
    }

    // Checks path to target and the target cell itself using a sphere "body".
    private bool IsStepClear(Vector3 delta)
    {
        Vector3 origin = transform.position + Vector3.up * 0.1f; // slight lift in case ground shares mask
        Vector3 dir = delta.normalized;
        float dist = delta.magnitude;

        // sweep for anything along the path
        bool blocked = Physics.SphereCast(origin, collisionRadius, dir, out _, dist - skin, obstacleLayer, QueryTriggerInteraction.Ignore);
        if (blocked) return false;

        // also ensure destination isn't already overlapping thin geometry
        Vector3 dest = RoundToNearestTile(transform.position + delta);
        bool overlapped = Physics.CheckSphere(dest + Vector3.up * 0.1f, collisionRadius - skin, obstacleLayer, QueryTriggerInteraction.Ignore);
        return !overlapped;
    }

    private IEnumerator MoveToPosition(Vector3 direction, float speed)
    {
        isMoving = true;
        targetPosition = RoundToNearestTile(transform.position + direction);

        while (Vector3.Distance(transform.position, targetPosition) > 0.01f)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);
            // while sliding, keep animator hot
            UpdateAnimatorLocomotion(direction, /*wishDirValid*/ true, hasRunningShoes && Input.GetKey(KeyCode.LeftShift));
            yield return null;
        }

        transform.position = targetPosition;
        isMoving = false;
        lastPosition = targetPosition;
    }

    private void UpdateAnimatorLocomotion(Vector3 wishDirWorld, bool wishDirValid, bool isRunning = false)
    {
        if (!animator) return;

        Vector3 stepDir = Vector3.zero;
        if (isMoving)
        {
            Vector3 v = (targetPosition - transform.position);
            if (v.magnitude > moveEpsilon) stepDir = v.normalized;
        }

        Vector3 worldMove = stepDir != Vector3.zero ? stepDir : (wishDirValid ? SnapDirTo8(wishDirWorld).normalized : Vector3.zero);

        float fwd = 0f, right = 0f, speed01 = 0f;
        bool anyMove = worldMove.sqrMagnitude > 0.0001f;

        if (anyMove)
        {
            fwd   = Mathf.Clamp(Vector3.Dot(worldMove, transform.forward), -1f, 1f);
            right = Mathf.Clamp(Vector3.Dot(worldMove, transform.right),   -1f, 1f);
            speed01 = isRunning ? 1f : 0.6f;
        }
        else
        {
            fwd = right = speed01 = 0f;
        }

        animator.SetBool(p_IsMoving, anyMove);
        animator.SetBool(p_IsRunning, isRunning);
        animator.SetFloat(p_Speed01, speed01, animDamp, Time.deltaTime);
        animator.SetFloat(p_Forward, fwd,     animDamp, Time.deltaTime);
        animator.SetFloat(p_Right,   right,   animDamp, Time.deltaTime);
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
        isMoving = false;
        transform.position = RoundToNearestTile(worldPos);
        targetPosition = transform.position;
        lastPosition = targetPosition;

        if (withCooldown)
        {
            StopAllCoroutines();
            StartCoroutine(TeleportCooldown());
        }
        else
        {
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

    // ----------------- Helpers -----------------
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

    private static int YawIndex8(Vector3 dir)
    {
        if (dir == Vector3.zero) return 0;
        float ang = Mathf.Atan2(dir.z, dir.x) * Mathf.Rad2Deg;
        if (ang < 0f) ang += 360f;
        return Mathf.RoundToInt(ang / 45f) & 7; // 0..7
    }

    // returns signed step count in [-4..+4] from a -> b (right positive)
    private static int DeltaYawSteps(int fromIdx, int toIdx)
    {
        int delta = (toIdx - fromIdx) % 8;
        if (delta > 4)  delta -= 8;
        if (delta < -4) delta += 8;
        return delta;
    }
}
