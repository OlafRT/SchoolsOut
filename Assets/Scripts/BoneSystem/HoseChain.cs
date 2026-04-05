using UnityEngine;

[DefaultExecutionOrder(10000)]
public class HoseChain : MonoBehaviour
{
    [Header("Anchors")]
    public Transform hoseStart;     // HoseSocket on the Backpack
    public Transform hoseEnd;       // HoseSocket on the Handle

    [Header("Bones")]
    [Tooltip("Drag joint1 through joint11 in order, root to tip.")]
    public Transform[] bones;

    [Header("Tip lock")]
    [Tooltip("Bones at the handle end placed along the backpack-to-handle direction " +
             "rather than the bezier, keeping the attachment stable during arm swing.")]
    [Range(0, 6)]
    public int tipBoneCount = 3;

    [Header("Bezier shape")]
    public float startTangentLength = 0.3f;
    public float endTangentLength   = 0.2f;
    public Vector3 sagOffset = new Vector3(0f, -0.25f, 0f);

    Quaternion[] _restLocalRots;
    Vector3[] _dLocal;
    float[] _segLengths;

    bool _initialized;

    void OnEnable() => _initialized = false;
    void OnValidate() { if (Application.isPlaying) _initialized = false; }

    void LateUpdate()
    {
        if (hoseStart == null || hoseEnd == null || bones == null || bones.Length < 2)
            return;

        if (!_initialized) Init();

        int last = bones.Length - 1;
        int clampedTip = Mathf.Min(tipBoneCount, last);

        // Pass 1: place all bones along a bezier
        Vector3 p0 = hoseStart.position;
        Vector3 p3 = hoseEnd.position;

        // Stable approach direction from handle back toward the backpack/body.
        Vector3 approachDir = (hoseStart.position - hoseEnd.position).normalized;

        Vector3 p1 = p0 + hoseStart.forward * startTangentLength;
        Vector3 p2 = p3 + approachDir * endTangentLength;

        for (int i = 0; i <= last; i++)
        {
            if (bones[i] == null) continue;

            float t = (float)i / last;
            float env = Mathf.Sin(t * Mathf.PI);
            bones[i].position = CubicBezier(p0, p1, p2, p3, t) + sagOffset * env;
        }

        // Pass 2: hard-lock the tip bones near the gun socket using the same stable direction
        for (int k = 0; k < clampedTip; k++)
        {
            int i = last - k;
            float dist = 0f;

            for (int j = 0; j < k; j++)
            {
                int seg = last - j - 1;
                if (seg >= 0) dist += _segLengths[seg];
            }

            if (bones[i] != null)
                bones[i].position = hoseEnd.position + approachDir * dist;
        }

        // Pass 3: rotate each bone to point toward the next one
        if (bones[0] == null) return;

        Transform parent0 = bones[0].parent;
        Quaternion runningRot = (parent0 != null ? parent0.rotation : Quaternion.identity) * _restLocalRots[0];

        for (int i = 0; i < last; i++)
        {
            if (bones[i] == null || bones[i + 1] == null)
            {
                if (i + 1 < bones.Length)
                    runningRot = runningRot * _restLocalRots[i + 1];
                continue;
            }

            Vector3 restDir = runningRot * _dLocal[i];
            Vector3 newDir = bones[i + 1].position - bones[i].position;

            Quaternion correction;

            if (restDir.sqrMagnitude < 0.00001f || newDir.sqrMagnitude < 0.00001f)
            {
                correction = Quaternion.identity;
            }
            else
            {
                Vector3 rn = restDir.normalized;
                Vector3 nd = newDir.normalized;
                float dot = Vector3.Dot(rn, nd);

                if (dot > 0.9999f)
                {
                    correction = Quaternion.identity;
                }
                else if (dot < -0.9999f)
                {
                    Vector3 axis = Vector3.Cross(rn, runningRot * Vector3.up);
                    if (axis.sqrMagnitude < 0.001f)
                        axis = Vector3.Cross(rn, runningRot * Vector3.right);

                    correction = Quaternion.AngleAxis(180f, axis.normalized);
                }
                else
                {
                    correction = Quaternion.FromToRotation(rn, nd);
                }
            }

            bones[i].rotation = correction * runningRot;

            if (i + 1 < bones.Length)
                runningRot = bones[i].rotation * _restLocalRots[i + 1];
        }

        // Hard-stabilize the last few tip bone rotations so the socket area
        // behaves like a firm connector instead of a floppy continuation
        // of the whole hose solve.
        Quaternion tipRot = BuildTipRotation(approachDir);

        // Keep a few bones before the socket on the same stable rotation.
        for (int k = 0; k < clampedTip; k++)
        {
            int i = last - k;
            if (i >= 0 && bones[i] != null)
                bones[i].rotation = tipRot;
        }
    }

    Quaternion BuildTipRotation(Vector3 approachDir)
    {
        // Stable up reference so the tip does not roll unpredictably.
        Vector3 upRef = hoseEnd != null ? hoseEnd.up : Vector3.up;

        // If up is nearly parallel to forward, choose another axis.
        if (Mathf.Abs(Vector3.Dot(approachDir.normalized, upRef.normalized)) > 0.98f)
            upRef = hoseEnd != null ? hoseEnd.right : Vector3.right;

        return Quaternion.LookRotation(approachDir, upRef);
    }

    void Init()
    {
        int n = bones.Length;
        _restLocalRots = new Quaternion[n];
        _dLocal = new Vector3[n - 1];
        _segLengths = new float[n - 1];

        for (int i = 0; i < n; i++)
        {
            if (bones[i] != null)
                _restLocalRots[i] = bones[i].localRotation;
        }

        for (int i = 0; i < n - 1; i++)
        {
            if (bones[i] == null || bones[i + 1] == null) continue;

            Vector3 d = bones[i + 1].position - bones[i].position;
            _segLengths[i] = Mathf.Max(d.magnitude, 0.001f);
            _dLocal[i] = d.sqrMagnitude > 0.00001f
                ? bones[i].InverseTransformDirection(d.normalized)
                : Vector3.forward;
        }

        _initialized = true;
    }

    static Vector3 CubicBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float u = 1f - t;
        return u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (hoseStart == null || hoseEnd == null) return;

        Vector3 p0 = hoseStart.position;
        Vector3 p3 = hoseEnd.position;
        Vector3 approachDir = (hoseStart.position - hoseEnd.position).normalized;

        Vector3 p1 = p0 + hoseStart.forward * startTangentLength;
        Vector3 p2 = p3 + approachDir * endTangentLength;

        Gizmos.color = Color.yellow;
        Vector3 prev = p0;

        for (int i = 1; i <= 24; i++)
        {
            float t = i / 24f;
            Vector3 pt = CubicBezier(p0, p1, p2, p3, t) + sagOffset * Mathf.Sin(t * Mathf.PI);
            Gizmos.DrawLine(prev, pt);
            prev = pt;
        }

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(p0, p1);
        Gizmos.DrawWireSphere(p1, 0.015f);
        Gizmos.DrawLine(p3, p2);
        Gizmos.DrawWireSphere(p2, 0.015f);

        if (bones != null && bones.Length > 0)
        {
            Gizmos.color = Color.green;
            float dist = 0f;

            for (int k = 0; k < Mathf.Min(tipBoneCount, bones.Length - 1); k++)
            {
                Vector3 a = hoseEnd.position + approachDir * dist;

                int seg = bones.Length - 1 - k - 1;
                if (seg >= 0 && seg < bones.Length - 1)
                    dist += 0.05f;

                Vector3 b = hoseEnd.position + approachDir * dist;
                Gizmos.DrawLine(a, b);
            }

            Gizmos.DrawWireSphere(hoseEnd.position, 0.025f);
        }
    }
#endif
}