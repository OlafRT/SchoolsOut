using UnityEngine;

public class BicycleController : MonoBehaviour
{
    [Header("References")]
    public Rigidbody rb;
    public Transform visualRoot;
    public Transform frontWheel;
    public Transform rearWheel;

    [Header("Crank Setup")]
    public Transform crankCenter;
    public Transform crankArmLeft;
    public Transform crankArmRight;
    public Transform leftPedal;
    public Transform rightPedal;

    [Header("Settings")]
    public float wheelRadius = 0.34f;
    public float maxSpeed = 10f;
    public float steerStrength = 2.5f;
    public float leanMaxDeg = 18f;
    public float drag = 0.25f;

    [Header("Ground Stabilization")]
    [Tooltip("Strength of the force pulling the bike toward the ground normal.")]
    public float groundStickForce = 100f;

    [Tooltip("How strongly to cancel any sideways drift when almost idle.")]
    public float idleDriftCancel = 8f;

    [SerializeField] float stickDownForce = 30f;
    [SerializeField] float maxSlopeAngle = 45f;
    [SerializeField] LayerMask groundMask = ~0;

    public enum Axis { X, Y, Z }
    public Axis crankSpinAxis = Axis.X;

    [Header("Steering Setup")]
    public Transform steeringPivot;
    public float maxHandlebarAngle = 30f;
    public float handlebarSteerSpeed = 5f;
    public float handlebarFadeStartSpeed = 3.0f;
    public float handlebarFadeEndSpeed = 9.0f;

    [Header("Grip / Anti-Drift")]
    public float lateralGrip = 20f;
    public float lowSpeedBarYawStrength = 1.6f;

    [Header("Tap Pedaling")]
    public float pedalImpulseAccel = 12f;
    public float pedalMaxSpeed = 8.5f;
    public float cadenceBoostPerTap = 360f;
    public float cadenceDecay = 280f;
    public float maxCadence = 700f;
    public float tapCooldown = 0.12f;

    [Header("Braking")]
    public float frontBrakeAccel = 40f;   // S
    public float rearBrakeAccel = 6f;     // Space (light; for drift)
    public float rearBrakeGripMultiplier = 0.2f;
    public float frontBrakeGripMultiplier = 1.25f;
    public float endoMinSpeed = 1.0f;
    public float endoMaxPitchDeg = 18f;
    public float endoPitchLerp = 8f;
    public float cadenceDecayFrontMul = 2.5f;
    public float cadenceDecayRearMul  = 1.8f;

    [Header("Rolling Loop Pitch")]
    [Tooltip("Base playback speed of the rolling loop at standstill.")]
    public float rollBasePitch = 0.9f;

    [Tooltip("Pitch at reference speed (top of ‘normal riding’).")]
    public float rollPitchAtRefSpeed = 1.8f;

    [Tooltip("Reference speed in m/s where pitch reaches rollPitchAtRefSpeed.")]
    public float rollPitchRefSpeed = 8f;

    [Tooltip("Clamp the rolling pitch to avoid chipmunking.")]
    public Vector2 rollPitchClamp = new Vector2(0.75f, 2.2f);

    // -------- NEW: Audio & FX --------
    [Header("Audio & FX")]
    [Tooltip("Looping tire/rolling noise (loop=true). Vol/Pitch scale with speed.")]
    public AudioSource rollingLoop;

    [Tooltip("Looping rear slide/skid (loop=true). Drives while rear-braking and sliding.")]
    public AudioSource rearSlideLoop;

    [Tooltip("General one-shot source (front brake squeal, breaths).")]
    public AudioSource oneShotSource;

    [Tooltip("Front brake squeal one-shot (on S press).")]
    public AudioClip frontBrakeClip;

    [Tooltip("Breathing effort one-shots, randomly chosen when exerted.")]
    public AudioClip[] breathClips;

    [Tooltip("Dust kicked up when rear-braking & sliding fast.")]
    public ParticleSystem rearBrakeDust;

    [Tooltip("Speed (m/s) above which rolling loop reaches max volume.")]
    public float rollMaxSpeedForVolume = 8f;

    [Tooltip("How strongly pitch scales with speed (1.0 ≈ linear around basePitch).")]
    public float rollPitchScale = 0.06f;

    [Tooltip("Minimum slide speed to begin slide SFX/FX.")]
    public float slideSpeedThreshold = 2.2f;

    [Tooltip("Minimum lateral slip to drive slide loop/dust.")]
    public float slideLateralThreshold = 0.8f;

    [Tooltip("Particle bursts per second at max slide intensity.")]
    public float dustBurstsPerSecond = 10f;

    [Header("Breathing Effort")]
    [Tooltip("How much each W tap adds to exertion.")]
    public float exertionPerTap = 1.0f;

    [Tooltip("How fast exertion decays per second.")]
    public float exertionDecayPerSec = 0.9f;

    [Tooltip("Exertion needed before breaths can play (still also requires gates below).")]
    public float breathTriggerThreshold = 6f;

    [Tooltip("Minimum seconds between breath one-shots.")]
    public float breathCooldown = 2.8f;

    [Tooltip("Chance (0..1) to play a breath when above threshold each check.")]
    public float breathPlayChance = 0.45f;

    // NEW gating
    [Tooltip("Minimum speed (m/s) required to allow breathing SFX.")]
    public float minBreathSpeed = 3.0f;

    [Tooltip("Minimum crank cadence (deg/s) to allow breathing SFX.")]
    public float minBreathCadence = 140f;

    [Tooltip("Measure taps per second in this rolling window (seconds).")]
    public float breathTapWindowSeconds = 2.5f;

    [Tooltip("Minimum W taps per second to allow breathing SFX.")]
    public float minBreathTapsPerSec = 3.0f;

    [Tooltip("Extra decay on exertion while braking or nearly stopped.")]
    public float exertionExtraDecayWhileIdle = 2.0f;

    [Header("Breath / Exhaustion")]
    public float maxBreath = 100f;
    public float breath = 100f;
    [Tooltip("Passive drain per second while moving.")]
    public float breathDrainPerSec = 2.0f;
    [Tooltip("Extra drain per pedal tap.")]
    public float breathDrainPerTap = 1.5f;
    [Tooltip("Speed is clamped when breath is very low.")]
    public float lowBreathSpeedCap = 4.0f;
    [Tooltip("If breath hits 0, we stop and 'fail'.")]
    public bool stopOnZeroBreath = true;

    [Header("Death On Zero Breath")]
    public bool enableDeathUI = true;
    public BreathDeathUI deathUI;      // assign in inspector
    [SerializeField] float stopSpeedToDie = 0.08f; // how slow before we fade in UI
    bool deathTriggered;

    [Header("Inhaler Effect")]
    public float inhaleRefillAmount = 35f;
    public float boostMultiplier = 1.25f;     // multiplies pedalImpulseAccel & maxSpeed
    public float boostSeconds = 2.0f;
    public AudioClip inhalerClip;             // one-shot on pickup/use
    public ParticleSystem inhalerSpray;       // assign the child PS on the rider here

    [Header("Rider")]
    public BicycleRiderIK riderIK;            // to trigger hand-to-mouth
    public Transform riderMouth;              // same as riderIK.mouthTarget
    // ----------------------------------

    private float wheelDeg;
    private float crankDeg;
    private float cadenceDegPerSec;
    private bool  WPressedThisFrame;
    private float tapTimer;
    float boostTimer;
    float baseMaxSpeed;
    float basePedalImpulse;
    // audio bookkeeping
    float breathTimer;
    float exertion;
    System.Collections.Generic.List<float> tapTimes = new System.Collections.Generic.List<float>();

    public float CurrentSpeed { get; private set; }
    public float HandlebarBlend { get; private set; }

    private Quaternion leftDefaultLocalRot;
    private Quaternion rightDefaultLocalRot;
    float endoPitch;

    void Awake()
    {
        if (!rb) rb = GetComponent<Rigidbody>();
        if (rb)
        {
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            rb.drag = 0f;
            rb.angularDrag = 0.5f;
        }

        // Make sure loops don't auto-play if clips are set
        if (rollingLoop) rollingLoop.playOnAwake = false;
        if (rearSlideLoop) rearSlideLoop.playOnAwake = false;
    }

    void Start()
    {
        if (leftPedal)  leftDefaultLocalRot  = Quaternion.Inverse(transform.rotation) * leftPedal.rotation;
        if (rightPedal) rightDefaultLocalRot = Quaternion.Inverse(transform.rotation) * rightPedal.rotation;
        baseMaxSpeed = maxSpeed;
        basePedalImpulse = pedalImpulseAccel;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.W))
        {
            WPressedThisFrame = true;
            exertion += exertionPerTap;
            tapTimes.Add(Time.time);
            breath = Mathf.Max(0f, breath - breathDrainPerTap);
        }

        // Drop old taps outside the window
        float cutoff = Time.time - breathTapWindowSeconds;
        for (int i = tapTimes.Count - 1; i >= 0; i--)
            if (tapTimes[i] < cutoff) tapTimes.RemoveAt(i);

        // Base decay
        float extraDecay = 0f;
        bool braking = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.Space);
        if (braking || CurrentSpeed < 0.5f) extraDecay = exertionExtraDecayWhileIdle;

        exertion = Mathf.Max(0f, exertion - (exertionDecayPerSec + extraDecay) * Time.deltaTime);

        // Passive drain when moving
        if (CurrentSpeed > 0.2f)
        breath = Mathf.Max(0f, breath - breathDrainPerSec * Time.deltaTime);

        // Handle zero-breath fail
        if (breath <= 0f && stopOnZeroBreath)
        {
            // Gently stop the bike
            rb.velocity = Vector3.MoveTowards(rb.velocity, Vector3.zero, 8f * Time.deltaTime);

            // When we're essentially stopped, trigger death once
            if (!deathTriggered && CurrentSpeed <= stopSpeedToDie)
                TriggerBreathDeath();
        }

        // Boost timer tick
        if (boostTimer > 0f)
        {
            boostTimer -= Time.deltaTime;
            float mul = (boostTimer > 0f) ? boostMultiplier : 1f;
            maxSpeed = baseMaxSpeed * mul;
            pedalImpulseAccel = basePedalImpulse * mul;
            if (boostTimer <= 0f)
            {
                maxSpeed = baseMaxSpeed;
                pedalImpulseAccel = basePedalImpulse;
            }
        }

        breathTimer -= Time.deltaTime;
    }

    void FixedUpdate()
    {
        tapTimer -= Time.fixedDeltaTime;

        HandleMovement();
        HandleRotationAndLean();
        HandleWheelSpin();
        HandleCrankAndPedals();

        WPressedThisFrame = false;
    }

    void LateUpdate()
    {
        UpdateAudioAndFX();
    }

    void HandleMovement()
    {
        // --- Detect ground ---
        bool grounded = Physics.Raycast(transform.position + Vector3.up * 0.2f, Vector3.down, out var hit, 2f, groundMask);
        Vector3 groundNormal = grounded ? hit.normal : Vector3.up;

        // --- Compute forward projected onto ground ---
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, groundNormal).normalized;
        float speedAlongForward = Vector3.Dot(rb.velocity, forward);

        bool frontBrake = Input.GetKey(KeyCode.S);
        bool rearBrake  = Input.GetKey(KeyCode.Space);

        // --- Base drag ---
        rb.AddForce(-rb.velocity * drag, ForceMode.Acceleration);

        // --- Downforce to keep grounded ---
        if (grounded)
            rb.AddForce(-groundNormal * stickDownForce, ForceMode.Acceleration);

        // --- Flatten unwanted vertical movement ---
        rb.velocity = new Vector3(rb.velocity.x, Mathf.Min(rb.velocity.y, 0f), rb.velocity.z);

        // Low-breath cap (soft limiter)
        float effectiveMax = maxSpeed;
        if (breath <= 5f) effectiveMax = Mathf.Min(effectiveMax, lowBreathSpeedCap);

        // --- Tap pedaling ---
        // Use effectiveMax instead of maxSpeed where relevant (speed checks):
        if (WPressedThisFrame && tapTimer <= 0f && speedAlongForward < pedalMaxSpeed && !frontBrake && !rearBrake && breath > 0f)
        {
            rb.AddForce(forward * pedalImpulseAccel, ForceMode.Acceleration);
            cadenceDegPerSec = Mathf.Min(maxCadence, cadenceDegPerSec + cadenceBoostPerTap);
            tapTimer = tapCooldown;
        }

        // --- Braking ---
        if (frontBrake && speedAlongForward > 0.05f)
            rb.AddForce(-forward * frontBrakeAccel, ForceMode.Acceleration);

        if (rearBrake && speedAlongForward > 0.05f)
            rb.AddForce(-forward * rearBrakeAccel, ForceMode.Acceleration);

        // --- Anti-drift (lateral velocity damping) ---
        Vector3 horizVel = Vector3.ProjectOnPlane(rb.velocity, groundNormal);
        Vector3 lateral = horizVel - forward * Vector3.Dot(horizVel, forward);

        float gripMul = 1f;
        if (rearBrake)  gripMul *= Mathf.Clamp01(rearBrakeGripMultiplier);
        if (frontBrake) gripMul *= Mathf.Max(1f, frontBrakeGripMultiplier);

        // When nearly idle, clamp all drift strongly to zero
        float slowFactor = Mathf.Clamp01(1f - CurrentSpeed / 2f);
        float cancelStrength = (lateralGrip + idleDriftCancel * slowFactor) * gripMul;
        rb.AddForce(-lateral * cancelStrength, ForceMode.Acceleration);

        // --- Prevent backward rolling ---
        float vFwd = Vector3.Dot(rb.velocity, forward);
        if (vFwd < 0f)
            rb.velocity -= forward * vFwd;

        // --- Update speed cache ---
        CurrentSpeed = rb.velocity.magnitude;

        // Kill micro drift when no input and nearly flat
        bool noInput = !Input.GetKey(KeyCode.W) && !Input.GetKey(KeyCode.A) && !Input.GetKey(KeyCode.D)
                    && !Input.GetKey(KeyCode.S) && !Input.GetKey(KeyCode.Space);
        if (noInput && CurrentSpeed < 0.05f)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // --- Keep bike upright and stable (no auto-yaw torque) ---
        if (grounded)
        {
            Vector3 targetUp = groundNormal;
            Quaternion targetRot = Quaternion.FromToRotation(transform.up, targetUp) * rb.rotation;
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRot, Time.fixedDeltaTime * 5f));
        }

        // --- Visual endo lean (unchanged) ---
        float endoTarget = 0f;
        if (frontBrake && speedAlongForward > endoMinSpeed)
        {
            float speedT = Mathf.Clamp01(speedAlongForward / Mathf.Max(0.01f, maxSpeed));
            float brakeT = Mathf.Clamp01(frontBrakeAccel / 40f);
            endoTarget = endoMaxPitchDeg * (0.5f * speedT + 0.5f * brakeT);
        }
        endoPitch = Mathf.Lerp(endoPitch, endoTarget, endoPitchLerp * Time.fixedDeltaTime);

        // --- Cadence decay ---
        if (frontBrake)
            cadenceDegPerSec = Mathf.MoveTowards(cadenceDegPerSec, 0f, cadenceDecay * cadenceDecayFrontMul * Time.fixedDeltaTime);
        else if (rearBrake)
            cadenceDegPerSec = Mathf.MoveTowards(cadenceDegPerSec, 0f, cadenceDecay * cadenceDecayRearMul * Time.fixedDeltaTime);
        else
            cadenceDegPerSec = Mathf.MoveTowards(cadenceDegPerSec, 0f, cadenceDecay * Time.fixedDeltaTime);
    }


    // Raycast the ground and return bike forward projected on that plane.
    // Falls back to world-forward if no hit.
    Vector3 GetGroundForward()
    {
        Vector3 origin = transform.position + Vector3.up * 0.2f;
        if (Physics.Raycast(origin, Vector3.down, out var hit, 1.0f, groundMask, QueryTriggerInteraction.Ignore))
        {
            Vector3 f = Vector3.ProjectOnPlane(transform.forward, hit.normal);
            if (f.sqrMagnitude > 1e-4f) return f.normalized;
        }
        return Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
    }

    void HandleRotationAndLean()
    {
        float steerInput = Input.GetAxisRaw("Horizontal");
        float speed = CurrentSpeed;

        float handlebarFactor = Mathf.InverseLerp(handlebarFadeStartSpeed, handlebarFadeEndSpeed, speed);
        handlebarFactor = Mathf.Clamp01(handlebarFactor);
        HandlebarBlend = handlebarFactor;

        // HANDLEBARS
        float handlebarAngle = steerInput * maxHandlebarAngle * (1f - handlebarFactor);
        if (steeringPivot)
        {
            Quaternion targetRot = Quaternion.Euler(0f, handlebarAngle, 0f);
            steeringPivot.localRotation = Quaternion.Slerp(
                steeringPivot.localRotation,
                targetRot,
                Time.fixedDeltaTime * handlebarSteerSpeed
            );
        }

        // LEAN + ENDO (visual only)
        float leanAmount = -steerInput * leanMaxDeg * handlebarFactor;
        if (visualRoot)
        {
            Quaternion baseRot = Quaternion.AngleAxis(leanAmount, transform.forward) * transform.rotation;
            Quaternion endoRot = Quaternion.AngleAxis(endoPitch, transform.right);
            Quaternion target = endoRot * baseRot;
            visualRoot.rotation = Quaternion.Slerp(visualRoot.rotation, target, 0.15f);
        }

        // YAW
        float yawFromLean = steerInput * steerStrength * Mathf.Clamp01(speed / (maxSpeed * 0.6f)) * handlebarFactor;
        float yawFromBars = steerInput * (1f - handlebarFactor) * (steerStrength * lowSpeedBarYawStrength);
        float totalTurn = yawFromLean + yawFromBars;
        rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, totalTurn, 0f));
    }

    void HandleWheelSpin()
    {
        float wheelCirc = 2f * Mathf.PI * wheelRadius;
        float wheelRps = rb.velocity.magnitude / Mathf.Max(0.01f, wheelCirc);
        wheelDeg += wheelRps * 360f * Time.fixedDeltaTime;

        if (frontWheel) frontWheel.localRotation = Quaternion.Euler(wheelDeg, 0f, 0f);
        if (rearWheel)  rearWheel.localRotation  = Quaternion.Euler(wheelDeg, 0f, 0f);
    }

    void HandleCrankAndPedals()
    {
        // base decay (extra braking decay is applied in HandleMovement)
        cadenceDegPerSec = Mathf.MoveTowards(cadenceDegPerSec, 0f, cadenceDecay * Time.fixedDeltaTime);

        if (crankCenter)
        {
            Vector3 localAxis =
                crankSpinAxis == Axis.X ? Vector3.right :
                crankSpinAxis == Axis.Y ? Vector3.up :
                                          Vector3.forward;

            crankDeg += cadenceDegPerSec * Time.fixedDeltaTime;
            crankCenter.localRotation = Quaternion.AngleAxis(crankDeg, localAxis);
        }

        if (leftPedal && crankArmLeft)
        {
            leftPedal.position = crankArmLeft.position;
            leftPedal.rotation = transform.rotation * leftDefaultLocalRot;
        }

        if (rightPedal && crankArmRight)
        {
            rightPedal.position = crankArmRight.position;
            rightPedal.rotation = transform.rotation * rightDefaultLocalRot;
        }
    }

    // ----------------- AUDIO & FX -----------------
    void UpdateAudioAndFX()
    {
        // Speed & slip metrics
        Vector3 forward = transform.forward;
        Vector3 horizVel = Vector3.ProjectOnPlane(rb.velocity, Vector3.up);
        float speed = horizVel.magnitude;
        float forwardSpeed = Vector3.Dot(horizVel, forward);
        float lateralSpeed = (horizVel - forward * forwardSpeed).magnitude;

        bool frontBrake = Input.GetKey(KeyCode.S);
        bool rearBrake  = Input.GetKey(KeyCode.Space);

        // --- Rolling loop (volume & pitch by speed) ---
        if (rollingLoop)
        {
            // Volume still ramps with speed
            float vt = Mathf.Clamp01(speed / Mathf.Max(0.01f, rollMaxSpeedForVolume));
            if (!rollingLoop.isPlaying) rollingLoop.Play();
            rollingLoop.volume = vt;

            // Pitch from speed (or use wheel RPM if you prefer)
            float pt = Mathf.Clamp01(speed / Mathf.Max(0.01f, rollPitchRefSpeed));
            float targetPitch = Mathf.Lerp(rollBasePitch, rollPitchAtRefSpeed, pt);
            targetPitch = Mathf.Clamp(targetPitch, rollPitchClamp.x, rollPitchClamp.y);

            // Smooth a little to prevent zipper noise
            rollingLoop.pitch = Mathf.Lerp(rollingLoop.pitch, targetPitch, 10f * Time.deltaTime);
        }

        // --- Front brake squeal (one-shot on key down) ---
        if (frontBrake && Input.GetKeyDown(KeyCode.S) && oneShotSource && frontBrakeClip && forwardSpeed > 0.5f)
        {
            oneShotSource.PlayOneShot(frontBrakeClip, Mathf.Clamp01(forwardSpeed / maxSpeed + 0.2f));
        }

        // --- Rear slide loop & dust ---
        float slideIntensity = 0f;
        if (rearBrake && speed > slideSpeedThreshold)
            slideIntensity = Mathf.InverseLerp(slideLateralThreshold, slideLateralThreshold * 2.5f, lateralSpeed);

        // Slide loop (unchanged logic, ok to keep)
        if (rearSlideLoop)
        {
            if (slideIntensity > 0.01f)
            {
                if (!rearSlideLoop.isPlaying) rearSlideLoop.Play();
                rearSlideLoop.volume = Mathf.Clamp01(slideIntensity);
                rearSlideLoop.pitch  = 0.9f + 0.3f * slideIntensity;
            }
            else if (rearSlideLoop.isPlaying)
            {
                rearSlideLoop.volume = Mathf.MoveTowards(rearSlideLoop.volume, 0f, 5f * Time.deltaTime);
                if (rearSlideLoop.volume <= 0.001f) rearSlideLoop.Stop();
            }
        }

        // Dust: use rateOverTime instead of Emit()
        if (rearBrakeDust)
        {
            var emission = rearBrakeDust.emission;
            if (slideIntensity > 0.05f)
            {
                if (!rearBrakeDust.isPlaying) rearBrakeDust.Play();
                // drive rate by slide intensity (expose this as a field if you want)
                emission.rateOverTimeMultiplier = dustBurstsPerSecond * slideIntensity;
            }
            else
            {
                emission.rateOverTimeMultiplier = 0f;
                if (rearBrakeDust.isPlaying) rearBrakeDust.Stop();
            }
        }

        // --- Breathing efforts ---
        if (oneShotSource && breathClips != null && breathClips.Length > 0)
        {
            // Gates
            float tapsPerSec = (tapTimes.Count > 0) ? (tapTimes.Count / Mathf.Max(0.001f, breathTapWindowSeconds)) : 0f;
            bool brakingNow = frontBrake || rearBrake;
            bool fastEnough = speed >= minBreathSpeed;
            bool pedalingHard = cadenceDegPerSec >= minBreathCadence || tapsPerSec >= minBreathTapsPerSec;

            if (!brakingNow && fastEnough && pedalingHard && exertion >= breathTriggerThreshold && breathTimer <= 0f)
            {
                if (Random.value < breathPlayChance)
                {
                    var clip = breathClips[Random.Range(0, breathClips.Length)];
                    oneShotSource.PlayOneShot(clip, Mathf.Clamp01(0.7f + 0.6f * (exertion / (breathTriggerThreshold * 2f))));
                }
                breathTimer = breathCooldown;
            }
        }
    }

    // Public API for pickups:
    public void ApplyInhaler(float refillAmount, float speedBoostMul, float duration)
    {
        breath = Mathf.Min(maxBreath, breath + Mathf.Max(0f, refillAmount));
        boostMultiplier = Mathf.Max(1f, speedBoostMul);
        boostSeconds = Mathf.Max(0f, duration);
        boostTimer = boostSeconds;

        // Hand pose & SFX
        if (riderIK) riderIK.PlayInhalerPose(boostSeconds * 0.6f);
        if (oneShotSource && inhalerClip) oneShotSource.PlayOneShot(inhalerClip, 1f);

        // --- PLAY EXISTING CHILD PARTICLES (no Instantiate) ---
        if (inhalerSpray)
        {
            // Optional: align to mouth each time
            if (riderMouth)
            {
                inhalerSpray.transform.position = riderMouth.position;
                inhalerSpray.transform.rotation = riderMouth.rotation;
            }

            // Make sure we restart cleanly
            inhalerSpray.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            inhalerSpray.Play(true);
        }
    } 

    void TriggerBreathDeath()
    {
        if (deathTriggered) return;
        deathTriggered = true;

        // Stop physics/control
        if (rb)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.constraints = RigidbodyConstraints.FreezeAll;
        }

        // Mute ongoing loops if you want (optional):
        if (rollingLoop && rollingLoop.isPlaying) rollingLoop.Stop();
        if (rearSlideLoop && rearSlideLoop.isPlaying) rearSlideLoop.Stop();

        // Show UI
        if (enableDeathUI && deathUI) deathUI.Show();
    }
}
