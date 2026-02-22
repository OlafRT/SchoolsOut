using UnityEngine;

public class BirdSwimmer : MonoBehaviour
{
    public enum Mode { Leader, Follower }
    [Header("Mode")]
    public Mode mode = Mode.Leader;

    [Header("Leader Path (A -> B -> C -> ... -> A)")]
    public Transform[] waypoints;
    public float waypointReachDistance = 0.25f;

    [Header("Movement")]
    public float moveSpeed = 1.25f;
    public float turnSpeed = 6f;                // higher = snappier rotation
    public bool lockYToStartHeight = true;

    [Header("Follower")]
    public BirdSwimmer followTarget;
    public float followDistance = 1.2f;
    public float followSideOffset = 0f;         // +/- to spread a flock
    public float followTightness = 6f;          // higher = follows more tightly

    [Header("Follower Steering (natural turning)")]
    public float followAccel = 6f;          // how fast it can change velocity
    public float maxFollowSpeed = 1.6f;     // follower top speed (can be a bit > leader)
    public float arriveRadius = 1.5f;       // start slowing down when close
    public float stopRadius = 0.2f;         // considered "at" the follow spot

    [Header("Rig (assign these)")]
    public Transform body;                      // optional (can be same as root)
    public Transform head;                      // yaw only (local Y)

    [Header("Head Look")]
    public float headTurnSpeed = 8f;
    public float headMaxYaw = 55f;              // degrees left/right
    [Range(0f, 1f)] public float lookWhereGoingWeight = 0.75f;
    [Range(0f, 1f)] public float randomLookWeight = 0.35f;

    [Header("Random Look Settings")]
    public float randomYawRange = 35f;
    public Vector2 randomHoldTimeRange = new Vector2(0.6f, 2.0f);

    int _wpIndex;
    float _lockedY;
    Vector3 _vel; // SmoothDamp velocity
    Vector3 _followVelocity;

    float _randomYawTarget;
    float _randomYawCurrent;
    float _randomYawVel;
    float _randomTimer;

    float _headYawCurrent;
    float _headYawVel;

    void Awake()
    {
        _lockedY = transform.position.y;
        PickNewRandomLook();
    }

    void Update()
    {
        Vector3 desiredMoveDir = Vector3.zero;

        if (mode == Mode.Leader)
        {
            desiredMoveDir = LeaderMoveDir();
        }
        else
        {
            desiredMoveDir = FollowerMoveDir();
        }

        // Move (flat)
        if (desiredMoveDir.sqrMagnitude > 0.0001f)
        {
            Vector3 pos = transform.position + desiredMoveDir * (moveSpeed * Time.deltaTime);

            if (lockYToStartHeight)
                pos.y = _lockedY;

            transform.position = pos;

            // Body rotate toward move direction (mesh forward is +Z)
            Quaternion targetRot = Quaternion.LookRotation(desiredMoveDir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 1f - Mathf.Exp(-turnSpeed * Time.deltaTime));
        }

        // Head look (yaw only)
        UpdateHeadLook(desiredMoveDir);
    }

    Vector3 LeaderMoveDir()
    {
        if (waypoints == null || waypoints.Length == 0)
            return Vector3.zero;

        Transform wp = waypoints[_wpIndex];
        if (!wp) return Vector3.zero;

        Vector3 to = wp.position - transform.position;
        if (lockYToStartHeight) to.y = 0f;

        // Arrived? advance
        if (to.magnitude <= waypointReachDistance)
        {
            _wpIndex = (_wpIndex + 1) % waypoints.Length;
            wp = waypoints[_wpIndex];
            if (!wp) return Vector3.zero;

            to = wp.position - transform.position;
            if (lockYToStartHeight) to.y = 0f;
        }

        return to.normalized;
    }

    Vector3 FollowerMoveDir()
    {
        if (!followTarget) return Vector3.zero;

        // Desired "slot" behind the target (this is just a goal, not a hard constraint)
        Vector3 targetForward = followTarget.transform.forward;
        Vector3 targetRight = followTarget.transform.right;

        Vector3 desiredPos =
            followTarget.transform.position
            - targetForward * followDistance
            + targetRight * followSideOffset;

        if (lockYToStartHeight)
            desiredPos.y = _lockedY;

        Vector3 toGoal = desiredPos - transform.position;
        if (lockYToStartHeight) toGoal.y = 0f;

        float dist = toGoal.magnitude;

        // If we're basically at the goal, bleed off velocity smoothly
        if (dist <= stopRadius)
        {
            _followVelocity = Vector3.Lerp(_followVelocity, Vector3.zero, 1f - Mathf.Exp(-followAccel * Time.deltaTime));
            return Vector3.zero;
        }

        // Arrive behavior: full speed far away, slow down near the goal
        float desiredSpeed = maxFollowSpeed;
        if (dist < arriveRadius)
            desiredSpeed = Mathf.Lerp(0f, maxFollowSpeed, dist / Mathf.Max(0.0001f, arriveRadius));

        Vector3 desiredVel = (toGoal / dist) * desiredSpeed;

        // Accelerate our own velocity toward desired velocity
        _followVelocity = Vector3.MoveTowards(
            _followVelocity,
            desiredVel,
            followAccel * Time.deltaTime
        );

        // Move using our own velocity (flat)
        Vector3 pos = transform.position + _followVelocity * Time.deltaTime;
        if (lockYToStartHeight) pos.y = _lockedY;
        transform.position = pos;

        // Return movement direction for rotation + head look
        Vector3 move = _followVelocity;
        if (lockYToStartHeight) move.y = 0f;

        return move.sqrMagnitude > 0.0001f ? move.normalized : Vector3.zero;
    }

    void UpdateHeadLook(Vector3 moveDir)
    {
        if (!head) return;

        // Random look target refresh
        _randomTimer -= Time.deltaTime;
        if (_randomTimer <= 0f) PickNewRandomLook();

        // Smooth random yaw
        _randomYawCurrent = Mathf.SmoothDampAngle(_randomYawCurrent, _randomYawTarget, ref _randomYawVel, 0.25f);

        // "Look where going" yaw (relative to body forward)
        float goingYaw = 0f;
        if (moveDir.sqrMagnitude > 0.0001f)
        {
            // Signed yaw angle between forward and move direction
            goingYaw = Vector3.SignedAngle(transform.forward, moveDir, Vector3.up);
            goingYaw = Mathf.Clamp(goingYaw, -headMaxYaw, headMaxYaw);
        }

        float desiredYaw =
            goingYaw * lookWhereGoingWeight
            + _randomYawCurrent * randomLookWeight;

        desiredYaw = Mathf.Clamp(desiredYaw, -headMaxYaw, headMaxYaw);

        _headYawCurrent = Mathf.SmoothDampAngle(_headYawCurrent, desiredYaw, ref _headYawVel, 1f / Mathf.Max(0.01f, headTurnSpeed));

        // Apply local yaw only (keep head’s local X/Z as-is)
        Vector3 e = head.localEulerAngles;
        e.y = _headYawCurrent;
        head.localEulerAngles = e;
    }

    void PickNewRandomLook()
    {
        _randomYawTarget = Random.Range(-randomYawRange, randomYawRange);
        _randomTimer = Random.Range(randomHoldTimeRange.x, randomHoldTimeRange.y);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (mode == Mode.Leader && waypoints != null && waypoints.Length > 1)
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (!waypoints[i]) continue;
                int next = (i + 1) % waypoints.Length;
                if (!waypoints[next]) continue;
                Gizmos.DrawLine(waypoints[i].position, waypoints[next].position);
            }
        }
    }
#endif
}