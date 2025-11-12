using UnityEngine;
using System.Collections;

public class NPC804R : MonoBehaviour
{
    [System.Serializable]
    public class Leg
    {
        public Transform upper;   // hip pivot (rotates around X)
        public Transform lower;   // ankle pivot (child of upper, rotates around X)
        [HideInInspector] public Quaternion upperRest;
        [HideInInspector] public Quaternion lowerRest;
        [HideInInspector] public float phase;
    }

    [Header("Legs (upper -> lower)")]
    public Leg leftFront;
    public Leg rightFront;
    public Leg leftRear;
    public Leg rightRear;

    [Header("Gait")]
    [Tooltip("Degrees from center for the upper joint.")]
    public float hipSwingDeg = 18f;
    [Tooltip("Degrees from center for the lower joint (knee/ankle).")]
    public float ankleSwingDeg = 24f;
    [Tooltip("Lower joint fires slightly later for a clacky robot feel.")]
    public float anklePhaseDelay = 0.12f; // seconds
    [Tooltip("Base step rate when nearly idle (Hz).")]
    public float baseHz = 0.8f;
    [Tooltip("Extra Hz per 1 m/s world speed.")]
    public float hzPerMps = 6.5f;
    [Tooltip("Speed must exceed this before full stride replaces idle shuffle.")]
    public float minActiveSpeed = 0.05f;
    [Tooltip("Small shuffle when almost stopped.")]
    public bool idleShuffle = true;
    public float idleHipDeg = 4f;
    public float idleAnkleDeg = 6f;
    public float perLegPhaseJitter = 0.07f;

    [Header("Body / Extras")]
    public Transform body;             // optional; defaults to self
    public bool bodyBob = true;
    public float bobHeight = 0.02f;
    public bool bodyTilt = true;
    public float maxTiltDeg = 8f;
    public Transform head;             // pitch a little while moving
    public float headNodDeg = 8f;

    [Header("Tail (smoothed)")]
    public Transform tail;
    public float tailWagMaxDeg = 18f;   // max left/right swing
    public float tailWagHz = 2.0f;      // wag frequency
    public float tailDamping = 12f;     // higher = snappier
    public float tailMaxDegPerSec = 360f;

    [Header("Height Lock (since parent NPC moves)")]
    public bool lockWorldY = true;
    public float worldYOffset = 0f;

    [Header("Idle Sniff")]
    public bool enableSniff = true;
    public float sniffIdleSpeed = 0.05f;       // must be below this to consider sniffing
    public Vector2 sniffIntervalSeconds = new Vector2(6f, 14f);
    public float sniffDownDeg = 35f;
    public float sniffTimeDown = 0.25f;
    public int sniffPumps = 2;
    public float sniffPumpDeg = 8f;
    public float sniffPumpHz = 6f;
    public AudioSource sniffAudio; // optional lil “pffft” sound

    [Header("Death (cartoony spread + fall)")]
    public AudioSource bangAudio;       // optional; plays on impact
    public AudioClip bangClip;
    public ParticleSystem deathBurst;   // optional, spawned at body
    public float spreadDegFront = 75f;  // forward kick on front legs (+X)
    public float spreadDegRear  = -75f; // backward kick on rear legs (-X)
    public float spreadTime = 0.18f;    // how quickly legs snap to spread
    public float tipOverTime = 0.22f;   // lean/tip duration before drop
    public float fakeDropHeight = 0.6f; // used if there’s no Rigidbody
    public float fakeDropTime = 0.25f;

    [Header("Audio (optional servo)")]
    public AudioSource servoLoop;
    public float servoSpeedMin = 0.05f;
    public float servoMaxVol = 0.45f;
    public float servoFollow = 10f;

    // --- internals ---
    Leg[] legs;
    Vector3 prevPos;
    float smoothedSpeed;
    float lockedY;
    Vector3 bodyBaseLocalPos;
    Quaternion bodyRestRot;

    // tail smoothing
    float tailPhase = 0f;
    float tailYaw = 0f;
    float tailYawVel = 0f;

    // state
    bool isDead;
    bool isSniffing;

    void Awake()
    {
        legs = new[] { leftFront, rightRear, rightFront, leftRear }; // trot: A pair then B pair
        foreach (var L in legs)
        {
            if (L == null) continue;
            if (L.upper) L.upperRest = L.upper.localRotation;
            if (L.lower) L.lowerRest = L.lower.localRotation;
            L.phase = Random.Range(-perLegPhaseJitter, perLegPhaseJitter);
        }
        // LF + RR together, RF + LR opposite (0.5 cycle)
        AddPhase(rightFront, 0.5f);
        AddPhase(leftRear,  0.5f);

        if (!body) body = transform;
        bodyBaseLocalPos = body.localPosition;
        bodyRestRot = body.localRotation;

        prevPos = transform.position;
        lockedY = transform.position.y + worldYOffset;

        if (servoLoop) { servoLoop.loop = true; servoLoop.playOnAwake = false; }

        // schedule first sniff
        if (enableSniff) StartCoroutine(SniffScheduler());
    }

    void AddPhase(Leg leg, float cycles) { if (leg != null) leg.phase += cycles; }

    void Update()
    {
        if (isDead) return; // dead pose handled by coroutine

        // 1) world speed (works even as a child of NPC mover)
        Vector3 p = transform.position;
        float rawSpeed = (p - prevPos).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
        prevPos = p;
        smoothedSpeed = Mathf.Lerp(smoothedSpeed, rawSpeed, 1f - Mathf.Exp(-12f * Time.deltaTime));

        // 2) cadence & amplitudes
        float hz = baseHz + hzPerMps * Mathf.Max(0f, smoothedSpeed);
        float hipAmp  = Mathf.Lerp(idleShuffle ? idleHipDeg : 0f,   hipSwingDeg,
                                   Mathf.InverseLerp(0f, minActiveSpeed, smoothedSpeed));
        float ankleAmp = Mathf.Lerp(idleShuffle ? idleAnkleDeg : 0f, ankleSwingDeg,
                                    Mathf.InverseLerp(0f, minActiveSpeed, smoothedSpeed));

        // 3) animate each leg (sine on X axis)
        for (int i = 0; i < legs.Length; i++)
        {
            var L = legs[i]; if (L == null || L.upper == null) continue;

            L.phase += Time.deltaTime * hz; // cycles
            float radHip   = (L.phase * 2f * Mathf.PI);
            float radAnkle = ((L.phase + SecToCycles(anklePhaseDelay, hz)) * 2f * Mathf.PI);

            float hipAng   = Mathf.Sin(radHip)   * hipAmp;     // -amp..amp
            float ankleAng = Mathf.Sin(radAnkle) * ankleAmp;

            L.upper.localRotation = L.upperRest * Quaternion.AngleAxis(hipAng, Vector3.right);
            if (L.lower)
                L.lower.localRotation = L.lowerRest * Quaternion.AngleAxis(ankleAng, Vector3.right);
        }

        // 4) head / tail / body flair
        float pairSum = legs[0].phase + legs[2].phase; // LF + RF used for bob rhythm
        if (head && !isSniffing)
        {
            float nod = Mathf.Sin(pairSum * 2f * Mathf.PI + Mathf.PI * 0.5f) * headNodDeg;
            head.localRotation = Quaternion.Euler(nod, 0f, 0f);
        }

        if (tail)
        {
            // desired wag amplitude grows with speed (gentle at idle)
            float amp = Mathf.Lerp(6f, tailWagMaxDeg,
                Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(smoothedSpeed)));

            // independent steady wag clock
            tailPhase += Mathf.Max(Time.deltaTime, 0.0001f) * tailWagHz;
            float desiredYaw = Mathf.Sin(tailPhase * 2f * Mathf.PI) * amp;

            // guard against any non-finite numbers
            if (!float.IsFinite(desiredYaw)) desiredYaw = 0f;
            if (!float.IsFinite(tailYaw))    tailYaw    = 0f;

            // limit slew, then critically damp
            float target = Mathf.MoveTowardsAngle(tailYaw, desiredYaw,
                tailMaxDegPerSec * Mathf.Max(Time.deltaTime, 0.0001f));

            tailYaw = Mathf.SmoothDampAngle(
                tailYaw, target, ref tailYawVel,
                1f / Mathf.Max(0.001f, tailDamping));

            tail.localRotation = Quaternion.Euler(0f, tailYaw, 0f);
        }

        if (body)
        {
            var pos = bodyBaseLocalPos;
            if (bodyBob)
                pos.y += Mathf.Sin(pairSum * 2f * Mathf.PI) * bobHeight;
            body.localPosition = pos;

            body.localRotation = bodyRestRot;
            if (bodyTilt)
            {
                float t = Mathf.Clamp(smoothedSpeed, 0f, 1.2f);
                body.localRotation *= Quaternion.Euler(Mathf.Lerp(0f, maxTiltDeg, t), 0f, 0f);
            }
        }

        // 5) keep authored world Y stable if desired
        if (lockWorldY)
            transform.position = new Vector3(transform.position.x, lockedY, transform.position.z);

        // 6) servo audio
        if (servoLoop)
        {
            float targetVol = Mathf.InverseLerp(servoSpeedMin, servoSpeedMin * 5f, smoothedSpeed) * servoMaxVol;
            servoLoop.volume = Mathf.Lerp(servoLoop.volume, targetVol, 1f - Mathf.Exp(-servoFollow * Time.deltaTime));
            if (servoLoop.volume > 0.01f && !servoLoop.isPlaying) servoLoop.Play();
            if (servoLoop.volume <= 0.01f && servoLoop.isPlaying) servoLoop.Stop();
        }
    }

    float SecToCycles(float seconds, float hz) => seconds * hz;

    // ---------- Idle Sniff ----------
    IEnumerator SniffScheduler()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(sniffIntervalSeconds.x, sniffIntervalSeconds.y));
            if (isDead || !enableSniff) yield break;

            if (smoothedSpeed <= sniffIdleSpeed && !isSniffing && head != null)
                yield return StartCoroutine(DoSniff());
        }
    }

    IEnumerator DoSniff()
    {
        isSniffing = true;

        // ease head down
        Quaternion start = head.localRotation;
        Quaternion down  = Quaternion.Euler(sniffDownDeg, 0f, 0f);
        float t = 0f;
        while (t < sniffTimeDown)
        {
            t += Time.deltaTime;
            head.localRotation = Quaternion.Slerp(start, down, t / sniffTimeDown);
            yield return null;
        }

        if (sniffAudio) sniffAudio.Play();

        // little pumps
        float pumpT = 0f;
        float dur = Mathf.Max(0.01f, (float)sniffPumps / sniffPumpHz);
        while (pumpT < dur)
        {
            pumpT += Time.deltaTime;
            float pump = Mathf.Sin(pumpT * sniffPumpHz * 2f * Mathf.PI) * sniffPumpDeg;
            head.localRotation = down * Quaternion.Euler(pump, 0f, 0f);
            yield return null;
        }

        // return to original
        t = 0f;
        while (t < sniffTimeDown)
        {
            t += Time.deltaTime;
            head.localRotation = Quaternion.Slerp(down, start, t / sniffTimeDown);
            yield return null;
        }

        isSniffing = false;
    }

    // ---------- Death ----------
    public void Die()
    {
        if (isDead) return;
        StartCoroutine(DieRoutine());
    }

    IEnumerator DieRoutine()
    {
        isDead = true;

        // stop servo
        if (servoLoop && servoLoop.isPlaying) servoLoop.Stop();

        // leg spread
        yield return StartCoroutine(SpreadLegs());

        // little tip/anticipation
        if (body)
        {
            Quaternion start = body.localRotation;
            Quaternion tip = start * Quaternion.Euler(12f, 0f, Random.value < 0.5f ? -10f : 10f);
            float t = 0f;
            while (t < tipOverTime)
            {
                t += Time.deltaTime;
                body.localRotation = Quaternion.Slerp(start, tip, t / tipOverTime);
                yield return null;
            }
        }

        // fall: prefer physics if a Rigidbody is present
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            if (lockWorldY) lockWorldY = false; // let it drop
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.AddTorque(Random.onUnitSphere * 2f, ForceMode.VelocityChange);
            // wait a brief moment for impact
            yield return new WaitForSeconds(0.25f);
        }
        else
        {
            // fake drop straight down a bit
            Vector3 start = transform.position;
            Vector3 end = start + Vector3.down * fakeDropHeight;
            if (lockWorldY) lockWorldY = false;
            float t = 0f;
            while (t < fakeDropTime)
            {
                t += Time.deltaTime;
                transform.position = Vector3.Lerp(start, end, t / fakeDropTime);
                yield return null;
            }
        }

        // bang + particles
        if (bangAudio)
        {
            if (bangClip) bangAudio.PlayOneShot(bangClip);
            else bangAudio.Play();
        }
        if (deathBurst)
        {
            var fx = Instantiate(deathBurst, body ? body.position : transform.position, Quaternion.identity);
            fx.Play();
            Destroy(fx.gameObject, fx.main.duration + fx.main.startLifetime.constantMax + 0.5f);
        }

        // leave in dead state; legs already splayed
    }

    IEnumerator SpreadLegs()
    {
        // Cache rest for mix with spread
        Quaternion lfU = leftFront?.upperRest ?? Quaternion.identity;
        Quaternion lfL = leftFront?.lowerRest ?? Quaternion.identity;
        Quaternion rfU = rightFront?.upperRest ?? Quaternion.identity;
        Quaternion rfL = rightFront?.lowerRest ?? Quaternion.identity;
        Quaternion lrU = leftRear?.upperRest ?? Quaternion.identity;
        Quaternion lrL = leftRear?.lowerRest ?? Quaternion.identity;
        Quaternion rrU = rightRear?.upperRest ?? Quaternion.identity;
        Quaternion rrL = rightRear?.lowerRest ?? Quaternion.identity;

        float t = 0f;
        while (t < spreadTime)
        {
            t += Time.deltaTime;
            float a = Mathf.SmoothStep(0f, 1f, t / spreadTime);

            // Front forward, Rear backward
            if (leftFront?.upper) leftFront.upper.localRotation = Quaternion.Slerp(lfU, lfU * Quaternion.Euler(spreadDegFront, 0f, 0f), a);
            if (leftFront?.lower) leftFront.lower.localRotation = Quaternion.Slerp(lfL, lfL * Quaternion.Euler(spreadDegFront * 0.4f, 0f, 0f), a);

            if (rightFront?.upper) rightFront.upper.localRotation = Quaternion.Slerp(rfU, rfU * Quaternion.Euler(spreadDegFront, 0f, 0f), a);
            if (rightFront?.lower) rightFront.lower.localRotation = Quaternion.Slerp(rfL, rfL * Quaternion.Euler(spreadDegFront * 0.4f, 0f, 0f), a);

            if (leftRear?.upper) leftRear.upper.localRotation = Quaternion.Slerp(lrU, lrU * Quaternion.Euler(spreadDegRear, 0f, 0f), a);
            if (leftRear?.lower) leftRear.lower.localRotation = Quaternion.Slerp(lrL, lrL * Quaternion.Euler(spreadDegRear * 0.4f, 0f, 0f), a);

            if (rightRear?.upper) rightRear.upper.localRotation = Quaternion.Slerp(rrU, rrU * Quaternion.Euler(spreadDegRear, 0f, 0f), a);
            if (rightRear?.lower) rightRear.lower.localRotation = Quaternion.Slerp(rrL, rrL * Quaternion.Euler(spreadDegRear * 0.4f, 0f, 0f), a);

            yield return null;
        }
    }
}
