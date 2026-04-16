using System.Collections;
using UnityEngine;

/// <summary>
/// Procedural squish/stretch animation for slime creatures.
/// Analogous to RoachSkitter — no Animator needed.
///
/// HOW IT WORKS:
///   • Moving   → body squishes flat on Y and spreads on XZ (slug flowing forward)
///   • Arrived  → springs back with an underdamped jiggle (like a water balloon)
///   • Idle     → slow breathing pulse (Y rises/falls, XZ inverse)
///   • Moving   → body tilts slightly in travel direction (leans into movement)
///   • Eye glow → pulses brighter while moving / dimmer at rest
///
/// SETUP:
///   1. Assign 'body' to your slime's visual mesh child (not the root).
///      Scale effects are applied here so colliders aren't affected.
///   2. Optionally assign 'eyeGlow' (a child Light or emissive MeshRenderer).
///   3. Drop squelchClips in (wet squishy sounds — played on each tile step).
///   4. Optionally assign a movingLoopClip (continuous low wet sound while sliding).
/// </summary>
[DisallowMultipleComponent]
public class SlimeJiggle : MonoBehaviour
{
    // ──────────────────────────────────────────────────
    [Header("Body (visual mesh — NOT the root transform)")]
    [Tooltip("The mesh child to squish. Defaults to this transform if left empty.")]
    public Transform body;

    [Header("Squish While Moving")]
    [Tooltip("Y scale at peak squish (flatter = more slimy).")]
    public float squishY  = 0.60f;
    [Tooltip("XZ scale at peak squish (wider = volume preserved).")]
    public float squishXZ = 1.28f;

    [Header("Stretch On Arrival Overshoot")]
    [Tooltip("Y slightly over-extends on the spring-back bounce.")]
    public float stretchY  = 1.10f;
    [Tooltip("XZ slightly contracts on the bounce (inverse of stretch).")]
    public float stretchXZ = 0.94f;

    [Header("Spring Physics (underdamped = jiggle)")]
    [Tooltip("Spring stiffness. Higher = faster return. Keep 6..14.")]
    public float springFreq    = 10f;
    [Tooltip("Damping. Below 2*sqrt(springFreq) ≈ 6.3 gives oscillation.")]
    public float springDamping =  4.5f;
    [Tooltip("Impulse applied to spring velocity on each step-start for extra snap.")]
    public float stepImpulse   =  0.8f;

    [Header("Idle Breathing Pulse")]
    public bool  idlePulse          = true;
    public float idlePulseAmplitude = 0.045f;   // peak Y scale delta
    public float idlePulseHz        = 0.65f;
    [Tooltip("XZ inverse of the Y pulse (rises as Y falls).")]
    public float idlePulseXZRatio   = 0.5f;

    [Header("Directional Lean")]
    [Tooltip("Max degrees the body tilts forward in the movement direction.")]
    public float leanMaxDeg   = 14f;
    [Tooltip("How fast the lean follows the direction change.")]
    public float leanFollowSpeed = 7f;

    [Header("Speed Detection")]
    public float speedSmoothing    = 9f;
    [Tooltip("m/s above which squish kicks in fully.")]
    public float moveThreshold     = 0.08f;

    [Header("Eye Glow (optional)")]
    [Tooltip("A child Light whose intensity pulses with movement.")]
    public Light eyeGlow;
    public float eyeIdleIntensity  = 0.6f;
    public float eyeActiveIntensity = 2.5f;
    [Tooltip("Additional fast pulse on the eye when moving.")]
    public float eyePulseHz        = 3.5f;
    public float eyePulseDepth     = 0.4f;     // fraction of active intensity

    [Tooltip("A Renderer child whose emission pulses (alternative to a Light).")]
    public Renderer eyeRenderer;
    [Tooltip("Emission color multiplied by intensity. Only used if eyeRenderer is assigned.")]
    public Color eyeEmissionColor = Color.green;
    public float eyeEmissionIdle   = 0.4f;
    public float eyeEmissionActive = 2.2f;

    [Header("Audio")]
    public bool enableAudio = true;
    [Tooltip("AudioSource for one-shot squelch hits. Auto-created if null.")]
    public AudioSource sfxSource;
    [Tooltip("Short wet squelch clips — a random one plays each tile step.")]
    public AudioClip[] squelchClips;
    [Tooltip("Continuous wet sliding loop, fades in/out with movement.")]
    public AudioClip movingLoopClip;
    public float squelchVolume    = 0.65f;
    public float movingLoopVolume = 0.30f;
    [Range(0f, 0.5f)]
    public float pitchVariation   = 0.18f;
    [Tooltip("How fast the loop volume fades in/out.")]
    public float audioFadeSpeed   = 7f;
    [Tooltip("Minimum seconds between squelch sounds.")]
    public float squelchCooldown  = 0.25f;

    [Header("Death")]
    public bool  isDead              = false;
    [Tooltip("Seconds to flatten into a puddle on death.")]
    public float deathFlattenSeconds = 0.55f;
    public float deathYScale         = 0.07f;
    public float deathXZScale        = 1.70f;
    public AudioClip deathSquelchClip;
    public ParticleSystem deathBurst;

    // ──────────────────────────────────────────────────
    // Spring state (independent springs for Y and XZ)
    float _curY,  _velY;
    float _curXZ, _velXZ;

    // Lean state
    Quaternion _leanRot = Quaternion.identity;

    // Speed tracking
    float _smoothedSpeed;
    Vector3 _prevPos;
    Vector3 _baseScale;

    // Idle
    float _idlePhase;

    // Audio
    AudioSource _loopSource;
    float _squelchTimer;
    float _eyePhase;
    bool  _wasMoving;

    // ──────────────────────────────────────────────────
    void Awake()
    {
        if (!body) body = transform;
        _baseScale = body.localScale;

        _curY  = _curXZ = 1f;
        _velY  = _velXZ = 0f;
        _prevPos = transform.position;

        SetupAudio();
    }

    void SetupAudio()
    {
        if (!enableAudio) return;

        // One-shot SFX source
        if (!sfxSource)
            sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.spatialBlend = 1f;
        sfxSource.playOnAwake  = false;

        // Loop source
        if (movingLoopClip && !_loopSource)
        {
            _loopSource = gameObject.AddComponent<AudioSource>();
            _loopSource.clip          = movingLoopClip;
            _loopSource.loop          = true;
            _loopSource.playOnAwake   = false;
            _loopSource.spatialBlend  = 1f;
            _loopSource.volume        = 0f;
            _loopSource.minDistance   = 1.5f;
            _loopSource.maxDistance   = 8f;
        }
    }

    // ──────────────────────────────────────────────────
    void Update()
    {
        if (isDead) return;

        UpdateSpeed();
        bool isMoving = _smoothedSpeed > moveThreshold;

        if (isMoving && !_wasMoving) OnStepStart();
        _wasMoving = isMoving;

        UpdateSpringTargets(isMoving);
        StepSpring(ref _curY,  ref _velY,  isMoving ? squishY  : 1f, Time.deltaTime);
        StepSpring(ref _curXZ, ref _velXZ, isMoving ? squishXZ : 1f, Time.deltaTime);

        float pulseY = 0f, pulseXZ = 0f;
        if (idlePulse && !isMoving) IdlePulse(out pulseY, out pulseXZ);

        ApplyScale(pulseY, pulseXZ);
        UpdateLean(isMoving);
        UpdateEyeGlow(isMoving);
        UpdateLoopAudio(isMoving);

        _squelchTimer += Time.deltaTime;
    }

    // ──────────────────────────────────────────────────
    void UpdateSpeed()
    {
        float rawSpeed = (transform.position - _prevPos).magnitude
                         / Mathf.Max(Time.deltaTime, 0.00001f);
        _prevPos = transform.position;
        _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, rawSpeed,
                         1f - Mathf.Exp(-speedSmoothing * Time.deltaTime));
    }

    void OnStepStart()
    {
        // Slam the spring toward squish immediately for a snappy feel
        _velY  -= stepImpulse;
        _velXZ += stepImpulse * 0.55f;

        PlaySquelch();
    }

    void UpdateSpringTargets(bool moving)
    {
        // Targets are set inline in StepSpring calls — nothing needed here currently.
        // Keeping the method for future per-phase overrides (e.g., attack wind-up).
    }

    void IdlePulse(out float py, out float pxz)
    {
        _idlePhase += Time.deltaTime * idlePulseHz;
        float s = Mathf.Sin(_idlePhase * 2f * Mathf.PI);
        py  =  s * idlePulseAmplitude;
        pxz = -s * idlePulseAmplitude * idlePulseXZRatio;
    }

    void ApplyScale(float pulseY, float pulseXZ)
    {
        body.localScale = new Vector3(
            _baseScale.x * (_curXZ + pulseXZ),
            _baseScale.y * (_curY  + pulseY),
            _baseScale.z * (_curXZ + pulseXZ));
    }

    void UpdateLean(bool isMoving)
    {
        // Direction of travel (ignore Y)
        Vector3 vel = transform.position - _prevPos; vel.y = 0f;
        Quaternion targetLean = Quaternion.identity;

        if (isMoving && vel.sqrMagnitude > 0.00001f)
        {
            Vector3 dir = vel.normalized;
            // Tilt forward in movement direction (around the axis perpendicular to dir)
            Vector3 tiltAxis = Vector3.Cross(Vector3.up, dir).normalized;
            targetLean = Quaternion.AngleAxis(leanMaxDeg * Mathf.Clamp01(_smoothedSpeed / 0.5f), tiltAxis);
        }

        _leanRot = Quaternion.Slerp(_leanRot, targetLean,
                   1f - Mathf.Exp(-leanFollowSpeed * Time.deltaTime));

        body.localRotation = _leanRot;
    }

    void UpdateEyeGlow(bool isMoving)
    {
        float target = isMoving ? eyeActiveIntensity : eyeIdleIntensity;

        // Fast pulse overlaid on top when moving
        float pulse = 0f;
        if (isMoving)
        {
            _eyePhase += Time.deltaTime * eyePulseHz;
            pulse = (Mathf.Sin(_eyePhase * 2f * Mathf.PI) * 0.5f + 0.5f) * eyePulseDepth * eyeActiveIntensity;
        }
        else
        {
            _eyePhase = 0f;
        }

        float intensity = Mathf.Lerp(eyeGlow ? eyeGlow.intensity : eyeIdleIntensity,
                                     target + pulse,
                                     1f - Mathf.Exp(-8f * Time.deltaTime));

        if (eyeGlow)
            eyeGlow.intensity = intensity;

        if (eyeRenderer && eyeRenderer.material.HasProperty("_EmissionColor"))
        {
            float e = Mathf.Lerp(eyeEmissionIdle, eyeEmissionActive, (intensity - eyeIdleIntensity)
                                  / Mathf.Max(0.001f, eyeActiveIntensity - eyeIdleIntensity));
            eyeRenderer.material.SetColor("_EmissionColor", eyeEmissionColor * e);
        }
    }

    void UpdateLoopAudio(bool isMoving)
    {
        if (!_loopSource) return;

        float targetVol = isMoving ? movingLoopVolume : 0f;
        _loopSource.volume = Mathf.Lerp(_loopSource.volume, targetVol,
                             1f - Mathf.Exp(-audioFadeSpeed * Time.deltaTime));

        if (_loopSource.volume > 0.01f && !_loopSource.isPlaying) _loopSource.Play();
        if (_loopSource.volume <= 0.01f &&  _loopSource.isPlaying) _loopSource.Stop();
    }

    void PlaySquelch()
    {
        if (!enableAudio || !sfxSource) return;
        if (squelchClips == null || squelchClips.Length == 0) return;
        if (_squelchTimer < squelchCooldown) return;

        var clip = squelchClips[Random.Range(0, squelchClips.Length)];
        if (!clip) return;

        sfxSource.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        sfxSource.PlayOneShot(clip, squelchVolume);
        _squelchTimer = 0f;
    }

    // Semi-implicit Euler spring (underdamped when damping < 2*sqrt(springFreq²))
    void StepSpring(ref float value, ref float vel, float target, float dt)
    {
        float k = springFreq * springFreq;
        float force = -k * (value - target) - springDamping * vel;
        vel   += force * dt;
        value += vel   * dt;
    }

    // ──────────────────────────────────────────────────
    //   PUBLIC API
    // ──────────────────────────────────────────────────

    /// <summary>
    /// Trigger the death flatten. Called by SlimeDeathRelay when NPCHealth reaches 0.
    /// </summary>
    public void Die()
    {
        if (isDead) return;
        isDead = true;
        StartCoroutine(DieRoutine());
    }

    /// <summary>
    /// Trigger a quick squish impulse (e.g., when taking a hit).
    /// </summary>
    public void PlayHitJiggle()
    {
        _velY  -= stepImpulse * 1.4f;
        _velXZ += stepImpulse * 0.8f;
    }

    // ──────────────────────────────────────────────────
    IEnumerator DieRoutine()
    {
        // Stop loop audio
        if (_loopSource)
        {
            float t = 0f;
            float startVol = _loopSource.volume;
            while (t < 0.15f)
            {
                t += Time.deltaTime;
                _loopSource.volume = Mathf.Lerp(startVol, 0f, t / 0.15f);
                yield return null;
            }
            _loopSource.Stop();
        }

        // Death squelch
        if (enableAudio && deathSquelchClip && sfxSource)
        {
            sfxSource.pitch = Random.Range(0.80f, 1.05f);
            sfxSource.PlayOneShot(deathSquelchClip, squelchVolume * 1.4f);
        }

        // Kill eye glow
        if (eyeGlow) eyeGlow.intensity = 0f;
        if (eyeRenderer && eyeRenderer.material.HasProperty("_EmissionColor"))
            eyeRenderer.material.SetColor("_EmissionColor", Color.black);

        // Death particle burst
        if (deathBurst)
        {
            deathBurst.gameObject.SetActive(true);
            deathBurst.Play(true);
        }

        // Flatten to puddle with a quick squish overshoot then settle
        Vector3 startScale = body.localScale;
        Vector3 endScale = new Vector3(
            _baseScale.x * deathXZScale,
            _baseScale.y * deathYScale,
            _baseScale.z * deathXZScale);

        float elapsed = 0f;
        while (elapsed < deathFlattenSeconds)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / deathFlattenSeconds);
            body.localScale = Vector3.Lerp(startScale, endScale, t);
            yield return null;
        }

        body.localScale  = endScale;
        body.localRotation = Quaternion.identity; // reset any lean
    }
}
