using UnityEngine;

[RequireComponent(typeof(Animator))]
public class BicycleRiderIK : MonoBehaviour
{
    [Header("Bike")]
    public BicycleController bike;
    public Transform seatMount;          // where the HIPs should be
    public Vector3 seatOffset;           // small tweak if needed

    [Header("IK Targets on Bike")]
    public Transform leftPedalPin;
    public Transform rightPedalPin;
    public Transform leftGrip;           // on steeringPivot/handlebar
    public Transform rightGrip;

    [Header("Hints (optional)")]
    public Transform leftKneeHint;
    public Transform rightKneeHint;
    public Transform leftElbowHint;
    public Transform rightElbowHint;

    [Header("Weights")]
    [Range(0,1)] public float feetWeight = 1f;
    [Range(0,1)] public float handsWeightSlow = 1f;
    [Range(0,1)] public float handsWeightFast = 0.85f;
    [Range(0,1)] public float hintWeight = 0.8f;

    [Header("Inhaler Pose")]
    public Transform mouthTarget;
    [Tooltip("Seconds the right hand stays at the mouth when using an inhaler.")]
    public float inhalerPoseTime = 1.0f;
    [Range(0,1)] public float inhalerHandWeight = 1f;

    [Header("Inhaler Prop")]
    [Tooltip("A regular-sized inhaler GameObject parented to the rider's right-hand bone. Leave disabled by default.")]
    public GameObject handInhaler;

    [Header("Body Align")]
    public bool alignHipsToSeatRotation = true;      // rotate pelvis with the bike/seat
    public Vector3 pelvisYawPitchRollDeg;            // tiny local rotation trim if needed

    // ------------------ Foot-down at standstill ------------------
    public enum SupportFoot { Left, Right }

    [Header("Foot Down When Stopped")]
    public SupportFoot supportFoot = SupportFoot.Left;

    [Tooltip("Below this speed (m/s) the rider puts a foot down.")]
    public float footDownSpeed = 0.25f;

    [Tooltip("Above this speed (m/s) the rider puts the foot back on the pedal.")]
    public float pedalUpSpeed = 0.60f;   // should be > footDownSpeed

    [Tooltip("Seconds to blend foot to/from ground or manual target.")]
    public float footBlendTime = 0.18f;

    [Header("Manual Ground Targets (use empties)")]
    public bool useManualGroundTarget = true;
    public Transform leftGroundTarget;
    public Transform rightGroundTarget;
    public bool alignToGroundTargetRotation = true;

    [Header("Fallback Ground Probe (if manual target is missing or disabled)")]
    public Vector3 groundProbeOffset = new Vector3(-0.22f, 0.0f, 0.05f); // left side default
    public LayerMask groundMask = ~0;

    float footBlend;      // 0 = on pedal, 1 = on ground/target
    float footBlendVel;   // for SmoothDamp
    float inhalerTimer;
    // -------------------------------------------------------------

    Animator anim;

    void Awake()
    {
        anim = GetComponent<Animator>();
        if (handInhaler) handInhaler.SetActive(false);   // ensure hidden on load
    }

    void OnAnimatorIK(int layerIndex)
    {
        if (!anim || !bike || !seatMount) return;

        // 1) Put the HUMANOID BODY (pelvis) on the seat (this moves hips, not GameObject)
        Quaternion seatRot = alignHipsToSeatRotation
            ? seatMount.rotation * Quaternion.Euler(pelvisYawPitchRollDeg)
            : anim.bodyRotation;

        anim.bodyPosition = seatMount.position + seatOffset;
        anim.bodyRotation = seatRot;

        // 2) Blend hand stickiness by speed (more glued when slow / steering with bars)
        float tSpeed = Mathf.Clamp01(bike.HandlebarBlend); // 0 slow -> 1 fast
        float handsW = Mathf.Lerp(handsWeightSlow, handsWeightFast, tSpeed);
        bool inhalerActive = inhalerTimer > 0f;
        if (inhalerActive) inhalerTimer -= Time.deltaTime;

        // 3) Update foot-down blend (with hysteresis)
        float spd = bike.CurrentSpeed;
        float target = footBlend;
        if (spd <= footDownSpeed)      target = 1f; // foot down
        else if (spd >= pedalUpSpeed)  target = 0f; // foot back to pedal

        footBlend = Mathf.SmoothDamp(footBlend, target, ref footBlendVel, Mathf.Max(0.01f, footBlendTime));
        footBlend = Mathf.Clamp01(footBlend);

        // 4) Compute ground/target pose
        Vector3 groundPos; Quaternion groundRot;
        GetSupportPose(out groundPos, out groundRot);

        // 5) Feet IK: blend support foot to ground pose, keep the other on the pedal
        if (supportFoot == SupportFoot.Left)
        {
            PoseBlendFoot(AvatarIKGoal.LeftFoot, leftPedalPin, groundPos, groundRot, footBlend);
            SetIK(AvatarIKGoal.RightFoot, rightPedalPin, feetWeight);
        }
        else
        {
            PoseBlendFoot(AvatarIKGoal.RightFoot, rightPedalPin, groundPos, groundRot, footBlend);
            SetIK(AvatarIKGoal.LeftFoot, leftPedalPin, feetWeight);
        }

        // 6) Knee hints (optional but helpful)
        SetHint(AvatarIKHint.LeftKnee,  leftKneeHint,  hintWeight);
        SetHint(AvatarIKHint.RightKnee, rightKneeHint, hintWeight);

        // 7) Hands to grips
        if (inhalerActive && mouthTarget)
        {
            // Left hand stays on bar, right hand to mouth
            SetIK(AvatarIKGoal.LeftHand,  leftGrip,  handsW);
            SetIK(AvatarIKGoal.RightHand, mouthTarget, inhalerHandWeight);
        }
        else
        {
            // Normal both-hands-on-bars behavior
            SetIK(AvatarIKGoal.LeftHand,  leftGrip,  handsW);
            SetIK(AvatarIKGoal.RightHand, rightGrip, handsW);
        }

        // 8) Elbow hints
        SetHint(AvatarIKHint.LeftElbow,  leftElbowHint,  hintWeight);
        SetHint(AvatarIKHint.RightElbow, rightElbowHint, hintWeight);
    }

    // --- Pose computation ---

    void GetSupportPose(out Vector3 pos, out Quaternion rot)
    {
        // Prefer manual target if enabled and present
        if (useManualGroundTarget)
        {
            Transform t = (supportFoot == SupportFoot.Left) ? leftGroundTarget : rightGroundTarget;
            if (t)
            {
                pos = t.position;
                if (alignToGroundTargetRotation)
                {
                    rot = t.rotation;
                }
                else
                {
                    // Face bike forward, up = world up
                    Vector3 fwd = bike ? bike.transform.forward : transform.forward;
                    rot = Quaternion.LookRotation(Vector3.ProjectOnPlane(fwd, Vector3.up).normalized, Vector3.up);
                }
                return;
            }
        }

        // Fallback: raycast near the side of the bike
        Vector3 localProbe = groundProbeOffset;
        if (supportFoot == SupportFoot.Right) localProbe.x = -localProbe.x;

        Vector3 probeOrigin = seatMount.TransformPoint(localProbe);
        Vector3 up = Vector3.up;
        Vector3 fwdBike = bike ? bike.transform.forward : transform.forward;

        if (Physics.Raycast(probeOrigin + up * 0.6f, Vector3.down, out RaycastHit hit, 2.0f, groundMask, QueryTriggerInteraction.Ignore))
        {
            pos = hit.point;
            Vector3 fwdOnPlane = Vector3.ProjectOnPlane(fwdBike, hit.normal).normalized;
            if (fwdOnPlane.sqrMagnitude < 1e-4f) fwdOnPlane = Vector3.Cross(hit.normal, Vector3.right);
            rot = Quaternion.LookRotation(fwdOnPlane, hit.normal);
        }
        else
        {
            // Flat fallback
            pos = probeOrigin;
            rot = Quaternion.LookRotation(Vector3.ProjectOnPlane(fwdBike, up).normalized, up);
        }
    }

    void PoseBlendFoot(AvatarIKGoal goal, Transform pedal, Vector3 groundPos, Quaternion groundRot, float blend01)
    {
        if (!pedal) return;
        blend01 = Mathf.Clamp01(blend01);

        Vector3 pos = Vector3.Lerp(pedal.position, groundPos, blend01);
        Quaternion rot = Quaternion.Slerp(pedal.rotation, groundRot, blend01);

        anim.SetIKPositionWeight(goal, feetWeight);
        anim.SetIKRotationWeight(goal, feetWeight);
        anim.SetIKPosition(goal, pos);
        anim.SetIKRotation(goal, rot);
    }

    void SetIK(AvatarIKGoal goal, Transform target, float w)
    {
        if (!target) return;
        anim.SetIKPositionWeight(goal, w);
        anim.SetIKRotationWeight(goal, w);
        anim.SetIKPosition(goal, target.position);
        anim.SetIKRotation(goal, target.rotation);
    }

    void SetHint(AvatarIKHint hint, Transform t, float w)
    {
        if (!t) return;
        anim.SetIKHintPositionWeight(hint, w);
        anim.SetIKHintPosition(hint, t.position);
    }

    public void PlayInhalerPose(float duration = -1f)
    {
        inhalerTimer = (duration > 0f) ? duration : inhalerPoseTime;

        // Show the hand prop while we pose
        if (handInhaler) handInhaler.SetActive(true);

        // Restart the hide coroutine so overlapping uses extend correctly
        StopAllCoroutines();
        StartCoroutine(CoHideInhalerAfter(inhalerTimer));
    }

    System.Collections.IEnumerator CoHideInhalerAfter(float t)
    {
        // Keep the hand at the mouth slightly longer if you like:
        float hold = Mathf.Max(0.05f, t);
        yield return new WaitForSeconds(hold);
        if (handInhaler) handInhaler.SetActive(false);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        // Visualize manual targets
        Gizmos.color = Color.cyan;
        if (leftGroundTarget)  Gizmos.DrawWireSphere(leftGroundTarget.position, 0.06f);
        Gizmos.color = Color.magenta;
        if (rightGroundTarget) Gizmos.DrawWireSphere(rightGroundTarget.position, 0.06f);
    }
#endif
}
