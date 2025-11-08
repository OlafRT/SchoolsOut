using UnityEngine;

public class RoachSkitter : MonoBehaviour
{
    public enum Axis { X, Y, Z }

    [Header("Leg Pivots (local axis rotates to step)")]
    public Transform leftFront;
    public Transform leftMid;
    public Transform leftRear;
    public Transform rightFront;
    public Transform rightMid;
    public Transform rightRear;

    [Header("Rotation Axis (per leg)")]
    public Axis legAxis = Axis.Y;          // most rigs: Y twists around vertical

    [Header("Gait")]
    [Tooltip("Degrees each leg swings from center (peak to either side).")]
    public float swingAmplitudeDeg = 16f;
    [Tooltip("Base cadence at standstill (Hz).")]
    public float baseHz = 0.8f;
    [Tooltip("Extra Hz per 1 m/s of movement.")]
    public float hzAt1mps = 8f;
    [Tooltip("How quickly we smooth measured speed.")]
    public float speedSmoothing = 10f;
    [Tooltip("Minimum speed before full cadence kicks in.")]
    public float minActiveSpeed = 0.02f;
    [Tooltip("Keep a tiny idle shuffle when still.")]
    public bool idleShuffle = true;
    public float idleAmplitudeDeg = 3f;
    public float perLegPhaseJitter = 0.12f; // seconds of random offset for desync

    [Header("Height Lock")]
    public bool lockWorldY = true;
    public float worldYOffset = 0f;

    [Header("Body Flair (optional)")]
    public Transform body;
    public bool enableBodyTilt = false;
    public bool enableBodyBob  = false;
    public float tiltDeg = 6f;
    public float bobHeight = 0.005f;
    public float bodyYOffset = 0f;

    [Header("Audio")]
    public bool enableSkitterSFX = true;
    public AudioSource skitterSource;     // optional: auto-created if null and clip provided
    public AudioClip skitterClip;
    public float skitterMinSpeed = 0.03f;
    public float skitterMaxVolume = 0.6f;
    public float skitterPitchBase = 1.0f;
    public float skitterPitchRange = 0.15f;
    public float skitterFade = 10f;       // how fast volume follows speed

    [Header("Death")]
    public bool isDead = false;
    public float deathSpasmSeconds = 0.25f;
    public float deathFlipSeconds  = 0.40f;
    public float deathCurlDegrees  = 25f;
    public bool  disableAfterDeath = false;
    public float disableDelay = 2f;

    [Header("Death VFX")]
    public ParticleSystem deathBurst;     // assign a disabled/stopped ParticleSystem child
    public float deathGroundMargin = 0.005f; // small lift above floor during/after flip
    public float deathExtraLift = 0.0f;       // manual lift if needed

    // --- internal ---
    Transform[] legs;
    Quaternion[] restRot;
    float[] phase;              // seconds as phase-time, not radians
    float smoothedSpeed;
    Vector3 prevPos;
    float lockedWorldY;
    Vector3 bodyBaseLocalPos;

    // Smooth death lift
    float deathLiftVel = 0f;        // SmoothDamp velocity
    public float deathLiftSmooth = 0.08f; // seconds to smooth the vertical lift
    float deathTargetY;             // fixed world-Y to settle at during/after death

    void Awake()
    {
        legs  = new[] { leftFront, leftMid, leftRear, rightFront, rightMid, rightRear };
        restRot = new Quaternion[6];
        phase   = new float[6];

        for (int i = 0; i < legs.Length; i++)
        {
            if (legs[i]) restRot[i] = legs[i].localRotation;
            phase[i] = Random.Range(-perLegPhaseJitter, perLegPhaseJitter);
        }

        // Tripod phasing: LF–RM–LR in phase, RF–LM–RR opposite phase
        AddPhaseSeconds(IndexOf(leftFront), 0f);
        AddPhaseSeconds(IndexOf(rightMid),  0f);
        AddPhaseSeconds(IndexOf(leftRear),  0f);
        AddPhaseSeconds(IndexOf(rightFront), 0.5f); // 180° offset
        AddPhaseSeconds(IndexOf(leftMid),    0.5f);
        AddPhaseSeconds(IndexOf(rightRear),  0.5f);

        prevPos = transform.position;
        lockedWorldY = transform.position.y + worldYOffset;
        if (!body) body = transform;
        bodyBaseLocalPos = body.localPosition;

        // Auto-create a quiet looping skitter source if needed
        if (enableSkitterSFX && !skitterSource && skitterClip)
        {
            skitterSource = gameObject.AddComponent<AudioSource>();
            skitterSource.clip = skitterClip;
            skitterSource.loop = true;
            skitterSource.playOnAwake = false;
            skitterSource.spatialBlend = 1f;   // 3D
            skitterSource.volume = 0f;
            skitterSource.minDistance = 1.5f;
            skitterSource.maxDistance = 8f;
        }
    }

    void AddPhaseSeconds(int idx, float sec)
    {
        if (idx < 0) return;
        phase[idx] += sec;
    }

    int IndexOf(Transform t)
    {
        if (t == leftFront)  return 0;
        if (t == leftMid)    return 1;
        if (t == leftRear)   return 2;
        if (t == rightFront) return 3;
        if (t == rightMid)   return 4;
        if (t == rightRear)  return 5;
        return -1;
    }

    void Update()
    {
        // Dead = no animation/bob; gently hold final Y (no popping)
        if (isDead)
        {
            if (lockWorldY) SmoothHoldDeathY();
            // Fade out skitter
            if (enableSkitterSFX && skitterSource)
            {
                float v = Mathf.Lerp(skitterSource.volume, 0f, 1f - Mathf.Exp(-skitterFade * Time.deltaTime));
                skitterSource.volume = v;
                if (v <= 0.001f && skitterSource.isPlaying) skitterSource.Stop();
            }
            return;
        }

        // 1) measure world speed (works even if we’re a child of a moving NPC)
        Vector3 pos = transform.position;
        float rawSpeed = (pos - prevPos).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
        prevPos = pos;
        smoothedSpeed = Mathf.Lerp(smoothedSpeed, rawSpeed, 1f - Mathf.Exp(-speedSmoothing * Time.deltaTime));

        // Skitter SFX while alive
        if (enableSkitterSFX && skitterSource && skitterClip)
        {
            float t = Mathf.InverseLerp(skitterMinSpeed, skitterMinSpeed * 5f, smoothedSpeed);
            float targetVol = t * skitterMaxVolume;
            float newVol = Mathf.Lerp(skitterSource.volume, targetVol, 1f - Mathf.Exp(-skitterFade * Time.deltaTime));
            skitterSource.volume = newVol;

            // gentle pitch flutter with speed
            float flutter = (Mathf.PerlinNoise(Time.time * 3.1f, 0.37f) - 0.5f) * 2f; // -1..1
            skitterSource.pitch = skitterPitchBase + skitterPitchRange * flutter;

            if (newVol > 0.01f && !skitterSource.isPlaying) skitterSource.Play();
            if (newVol <= 0.01f && skitterSource.isPlaying) skitterSource.Stop();
        }

        // 2) compute cadence (Hz) and amplitude
        float hz = baseHz + hzAt1mps * Mathf.Max(0f, smoothedSpeed);
        float amp = Mathf.Lerp(idleShuffle ? idleAmplitudeDeg : 0f, swingAmplitudeDeg,
                               Mathf.InverseLerp(0f, minActiveSpeed, smoothedSpeed));
        if (hz < 0.01f && amp <= 0.001f) return;

        // 3) rotate each leg by a sine wave with tripod phase offsets
        for (int i = 0; i < legs.Length; i++)
        {
            var leg = legs[i];
            if (!leg) continue;

            phase[i] += Time.deltaTime * hz;       // seconds * Hz = cycles
            float cycles = phase[i];
            float radians = (cycles * 2f * Mathf.PI);

            float angle = Mathf.Sin(radians) * amp; // -amp..amp
            Quaternion swing = Quaternion.AngleAxis(angle, AxisVector(legAxis));

            leg.localRotation = restRot[i] * swing;
        }

        // 4) optional body tilt/bob (won't shove body under legs)
        if (body)
        {
            var bodyPos = bodyBaseLocalPos + new Vector3(0f, bodyYOffset, 0f);

            if (enableBodyBob)
            {
                float bob = Mathf.Sin((phase[0] + phase[3]) * 2f * Mathf.PI) * bobHeight;
                bodyPos.y += bob;
            }

            body.localPosition = bodyPos;

            if (enableBodyTilt)
            {
                float tilt = Mathf.Clamp(smoothedSpeed * tiltDeg, 0f, tiltDeg);
                body.localRotation = Quaternion.AngleAxis(tilt, Vector3.right);
            }
            else
            {
                body.localRotation = Quaternion.identity;
            }
        }

        // Keep authored world Y while alive
        if (lockWorldY)
        {
            var p = transform.position;
            transform.position = new Vector3(p.x, lockedWorldY, p.z);
        }
    }

    // --- Death height helpers (fixed target + smooth hold) ---

    float SampleBaselineClearance()
    {
        // Clearance = how far current Y sits above the lowest renderer at death moment.
        float minY = float.PositiveInfinity;
        var rends = GetComponentsInChildren<Renderer>();
        for (int i = 0; i < rends.Length; i++)
        {
            if (!rends[i].enabled) continue;
            if (rends[i] is ParticleSystemRenderer) continue; // ignore particles
            minY = Mathf.Min(minY, rends[i].bounds.min.y);
        }
        if (float.IsInfinity(minY)) return 0f;
        return transform.position.y - minY; // >= 0
    }

    void SmoothHoldDeathY()
    {
        float y = Mathf.SmoothDamp(transform.position.y, deathTargetY, ref deathLiftVel, deathLiftSmooth);
        transform.position = new Vector3(transform.position.x, y, transform.position.z);
    }

    // --- Public API ---

    public void Die()
    {
        if (isDead) return;
        StartCoroutine(DieRoutine());
    }

    System.Collections.IEnumerator DieRoutine()
    {
        isDead = true;

        // Ensure we’ll keep our world Y reference
        if (lockWorldY) lockedWorldY = transform.position.y + worldYOffset;

        // Compute fixed target Y for the entire death (no per-frame bounds recompute)
        float baselineClearance = SampleBaselineClearance(); // current pose clearance
        deathTargetY = lockedWorldY + deathGroundMargin + deathExtraLift + baselineClearance;
        deathLiftVel = 0f;

        // Fade out skitter
        if (enableSkitterSFX && skitterSource)
        {
            float tEnd = Time.time + 0.15f;
            while (Time.time < tEnd)
            {
                skitterSource.volume = Mathf.Lerp(skitterSource.volume, 0f, 1f - Mathf.Exp(-skitterFade * Time.deltaTime));
                yield return null;
            }
            skitterSource.Stop();
        }

        // Quick spasm
        float spasmEnd = Time.time + deathSpasmSeconds;
        while (Time.time < spasmEnd)
        {
            for (int i = 0; i < legs.Length; i++)
            {
                if (!legs[i]) continue;
                float rndHz  = Random.Range(14f, 22f);
                float rndAmp = Random.Range(swingAmplitudeDeg * 0.7f, swingAmplitudeDeg * 1.2f);
                phase[i] += Time.deltaTime * rndHz;
                float ang = Mathf.Sin(phase[i] * 2f * Mathf.PI) * rndAmp;
                legs[i].localRotation = restRot[i] * Quaternion.AngleAxis(ang, AxisVector(legAxis));
            }
            if (lockWorldY) SmoothHoldDeathY();
            yield return null;
        }

        // Particle burst
        if (deathBurst)
        {
            deathBurst.gameObject.SetActive(true);
            deathBurst.Play(true);
        }

        // Flip onto back (keep above ground smoothly)
        Quaternion start = transform.localRotation;
        Quaternion end   = Quaternion.AngleAxis(180f, Vector3.right) * start; // flip axis as needed
        float tFlip = 0f;
        while (tFlip < 1f)
        {
            tFlip += Time.deltaTime / Mathf.Max(0.0001f, deathFlipSeconds);
            transform.localRotation = Quaternion.Slerp(start, end, Mathf.SmoothStep(0f, 1f, tFlip));

            if (lockWorldY) SmoothHoldDeathY();
            yield return null;
        }

        // Final curled pose
        for (int i = 0; i < legs.Length; i++)
            if (legs[i])
                legs[i].localRotation = restRot[i] * Quaternion.AngleAxis(deathCurlDegrees, AxisVector(legAxis));

        if (disableAfterDeath)
        {
            yield return new WaitForSeconds(disableDelay);
            enabled = false;
        }
    }

    Vector3 AxisVector(Axis a)
    {
        switch (a)
        {
            case Axis.X: return Vector3.right;
            case Axis.Y: return Vector3.up;
            default:     return Vector3.forward;
        }
    }
}
