// SeagullCameraSequence.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class SeagullCameraSequence : MonoBehaviour
{
    // ── References ────────────────────────────────────────────────────────────
    [Header("Cameras")]
    public Camera      mainCamera;
    public Camera      seagullCamera;   // direct child of SeagullRoot — GameObject ON, component OFF
    public CameraFollow cameraFollow;   // disabled during the sequence

    [Header("Skybox")]
    [Tooltip("Skybox material to use while the seagull camera is active. " +
             "Swapped in when the camera is stolen, restored when back to normal.")]
    public Material flightSkybox;

    [Header("Player")]
    public Transform      player;
    public PlayerMovement playerMovement;
    public Animator       playerAnimator;

    [Header("Seagull Model")]
    [Tooltip("Forward axis of your seagull mesh. (1,0,0) if it flies sideways.")]
    public Vector3 seagullLocalForward = new Vector3(-1f, 0f, 0f);

    [Header("Waypoints — Passes")]
    public Transform passLeftStart;
    public Transform passLeftEnd;
    public Transform passRightStart;
    public Transform passRightEnd;

    [Header("Waypoints — Dive")]
    public Transform diveStart;
    public Transform cameraStealPoint;

    [Header("Waypoints — Stolen Camera Flight Path")]
    [Tooltip("Add as many points as you like. Seagull flies through them in order.")]
    public List<Transform> flightPath = new List<Transform>();

    [Header("Timing")]
    public float flightSpeed          = 8f;
    public float stolenCameraSpeed    = 5f;   // slower for cinematic effect
    [Tooltip("How quickly the seagull rotates toward the next waypoint. Higher = snappier.")]
    public float flightRotationSpeed  = 3f;
    [Tooltip("How fast the player rotates to track the seagull during passes and dive.")]
    public float playerTrackSpeed     = 6f;
    public float pauseBetweenPasses   = 0.4f;
    public float pauseBeforeDive      = 0.6f;
    public float groundPauseSeconds   = 0.8f;

    [Header("Camera Fall")]
    public float cameraFallAccel      = 18f;
    [Tooltip("Extra height above the raycast ground hit to stop at. 0 = exactly on ground.")]
    public float groundStopOffset     = 0f;

    [Header("Fog")]
    [Tooltip("Fog density when the stolen camera is flying. Fades from scene default to this.")]
    public float fogDensityInFlight   = 0f;
    [Tooltip("Fog density restored when back on the ground. Should match your scene setting.")]
    public float fogDensityNormal     = 0.1f;
    [Tooltip("Seconds to fade fog in/out.")]
    public float fogFadeDuration      = 2f;

    [Header("Audio")]
    public AudioClip passSfx;
    public AudioClip stealSfx;
    [Tooltip("Played when the camera smashes into the ground. " +
             "Always 2D (full volume regardless of where the camera lands).")]
    public AudioClip crashSfx;
    [Range(0f,1f)] public float sfxVolume     = 1f;
    [Tooltip("Player reaction when the seagull flies past — 'What the heck?' etc.")]
    public AudioClip playerReactionSfx;
    [Range(0f,1f)] public float reactionVolume = 0.9f;
    [Tooltip("Played after the sequence, back on normal camera.")]
    public AudioClip playerVoiceLine;
    [Range(0f,1f)] public float voiceVolume   = 0.9f;

    [Header("Broken Camera UI")]
    public GameObject  brokenCameraUI;
    public CanvasGroup fadeOverlay;
    public float       fadeDuration = 1.2f;
    [Tooltip("How long to hold on black after fading out before restoring the camera and playing the voice line.")]
    public float       blackoutHoldDuration = 1.5f;
    [Tooltip("A GameObject inside your HUD Canvas that should stay visible during the sequence " +
             "(e.g. the fade overlay itself). All of its siblings will be hidden.")]
    public GameObject  excludeFromHide;

    bool _hasPlayed = false;
    GameObject[] _hiddenUIObjects;
    Material _originalSkybox;

    // Dedicated 2D AudioSource for the crash — spatialBlend = 0 so it plays at
    // full volume regardless of where the camera lands in the world.
    AudioSource _2dAudioSource;

    // ── Awake ─────────────────────────────────────────────────────────────────
    void Awake()
    {
        _2dAudioSource = gameObject.AddComponent<AudioSource>();
        _2dAudioSource.spatialBlend = 0f;   // 2D: no distance attenuation
        _2dAudioSource.playOnAwake  = false;
        _2dAudioSource.loop         = false;
    }

    // ── Entry point ───────────────────────────────────────────────────────────
    public void TriggerSequence()
    {
        if (_hasPlayed) return;
        _hasPlayed = true;
        StartCoroutine(RunSequence());
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  MAIN SEQUENCE
    // ═══════════════════════════════════════════════════════════════════════════
    IEnumerator RunSequence()
    {
        // ── 0. Lock player ────────────────────────────────────────────────────
        LockPlayer();
        SetSeagullVisible(false);

        // ── 1. Left pass ──────────────────────────────────────────────────────
        if (passLeftStart && passLeftEnd)
        {
            SetSeagullVisible(true);
            transform.position = passLeftStart.position;
            FaceDirection(passLeftEnd.position - passLeftStart.position);
            FacePlayerToward(transform.position);

            yield return FlyTo(passLeftEnd.position, flightSpeed,
                onFraction: () => PlaySfx(passSfx, transform.position),
                fraction: 0.15f, trackPlayer: true);

            SetSeagullVisible(false);
        }

        yield return new WaitForSeconds(pauseBetweenPasses);

        // ── 2. Right pass ─────────────────────────────────────────────────────
        if (passRightStart && passRightEnd)
        {
            SetSeagullVisible(true);
            transform.position = passRightStart.position;
            FaceDirection(passRightEnd.position - passRightStart.position);
            FacePlayerToward(transform.position);

            // Player reaction on the second pass when it gets close
            yield return FlyTo(passRightEnd.position, flightSpeed,
                onFraction: () =>
                {
                    PlaySfx(passSfx, transform.position);
                    if (playerReactionSfx && player)
                        AudioSource.PlayClipAtPoint(playerReactionSfx, player.position, reactionVolume);
                },
                fraction: 0.35f, trackPlayer: true);

            SetSeagullVisible(false);
        }

        yield return new WaitForSeconds(pauseBeforeDive);

        // ── 3. Front dive ─────────────────────────────────────────────────────
        if (diveStart && cameraStealPoint)
        {
            SetSeagullVisible(true);
            transform.position = diveStart.position;
            FaceDirection(cameraStealPoint.position - diveStart.position);
            FacePlayerToward(transform.position);
            yield return FlyTo(cameraStealPoint.position, flightSpeed * 1.4f, trackPlayer: true);
        }

        // ── 4. Steal the camera ───────────────────────────────────────────────
        PlaySfx(stealSfx, transform.position);

        // Snap seagull to first flight path point before enabling camera
        // so there's no black frame during the switch
        if (flightPath.Count > 0 && flightPath[0] != null)
        {
            transform.position = flightPath[0].position;
            if (flightPath.Count > 1 && flightPath[1] != null)
                FaceDirection(flightPath[1].position - flightPath[0].position);
        }

        HideGameplayUI();
        if (cameraFollow)    cameraFollow.enabled    = false;
        if (mainCamera)      mainCamera.enabled       = false;
        if (seagullCamera)   seagullCamera.enabled    = true;

        // Swap skybox in — timed with fog fade so the switch is hidden
        if (flightSkybox)
        {
            _originalSkybox = RenderSettings.skybox;
            RenderSettings.skybox = flightSkybox;
            DynamicGI.UpdateEnvironment();
        }

        // Start fading fog out as we take flight
        if (flightPath.Count > 0)
            StartCoroutine(FadeFog(RenderSettings.fogDensity, fogDensityInFlight, fogFadeDuration));

        // ── 5. Fly stolen camera through all path points (smooth rotation) ────
        if (flightPath.Count >= 2)
            yield return FlyPathSmooth(flightPath, stolenCameraSpeed, flightRotationSpeed);

        // ── 6. Drop the camera ────────────────────────────────────────────────
        SetSeagullVisible(false);

        if (seagullCamera)
        {
            seagullCamera.transform.SetParent(null, true);
            seagullCamera.transform.rotation = Quaternion.Euler(10f,
                seagullCamera.transform.eulerAngles.y, 0f);

            yield return CameraFall(seagullCamera.transform);
        }

        // Restore fog as camera hits ground
        StartCoroutine(FadeFog(RenderSettings.fogDensity, fogDensityNormal, fogFadeDuration * 0.5f));

        // ── 7. Ground pause ───────────────────────────────────────────────────
        yield return new WaitForSeconds(groundPauseSeconds);

        // ── 8. Broken UI + fade to black ──────────────────────────────────────
        if (brokenCameraUI) brokenCameraUI.SetActive(true);

        if (fadeOverlay)
        {
            fadeOverlay.gameObject.SetActive(true);
            yield return Fade(fadeOverlay, 0f, 1f, fadeDuration);
        }

        // ── 9. Hold on black ─────────────────────────────────────────────────
        yield return new WaitForSecondsRealtime(blackoutHoldDuration);

        // ── 10. Restore ───────────────────────────────────────────────────────
        if (brokenCameraUI) brokenCameraUI.SetActive(false);
        if (seagullCamera)  seagullCamera.enabled  = false;
        if (mainCamera)     mainCamera.enabled     = true;
        if (cameraFollow)   cameraFollow.enabled   = true;
        RestoreGameplayUI();

        // Restore original skybox under cover of the fade
        if (_originalSkybox)
        {
            RenderSettings.skybox = _originalSkybox;
            DynamicGI.UpdateEnvironment();
            _originalSkybox = null;
        }

        if (fadeOverlay)
        {
            yield return Fade(fadeOverlay, 1f, 0f, fadeDuration * 0.5f);
            fadeOverlay.gameObject.SetActive(false);
        }

        // ── 11. Voice line + unlock ───────────────────────────────────────────
        if (playerVoiceLine && player)
            AudioSource.PlayClipAtPoint(playerVoiceLine, player.position, voiceVolume);

        if (playerMovement) playerMovement.enabled = true;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════════════════════════════════

    void LockPlayer()
    {
        if (playerMovement)
        {
            playerMovement.StopMovement();
            playerMovement.enabled = false;
        }
        if (playerAnimator)
        {
            playerAnimator.SetBool("IsMoving",  false);
            playerAnimator.SetBool("IsRunning", false);
            playerAnimator.SetFloat("Speed01",  0f);
            playerAnimator.SetFloat("Forward",  0f);
            playerAnimator.SetFloat("Right",    0f);
        }
    }

    void FacePlayerToward(Vector3 target)
    {
        if (!player) return;
        Vector3 dir = target - player.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
            player.rotation = Quaternion.LookRotation(dir.normalized);
    }

    void SmoothFacePlayerToward(Vector3 target)
    {
        if (!player) return;
        Vector3 dir = target - player.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return;

        // Snap to nearest of 8 directions, matching the player's movement system
        Vector3 snapped = SnapDirTo8(dir);
        if (snapped.sqrMagnitude > 0.001f)
            player.rotation = Quaternion.LookRotation(snapped);
    }

    static Vector3 SnapDirTo8(Vector3 v)
    {
        v.y = 0f;
        if (v.sqrMagnitude < 0.0001f) return Vector3.zero;
        float ang = Mathf.Atan2(v.z, v.x) * Mathf.Rad2Deg;
        if (ang < 0f) ang += 360f;
        int step = Mathf.RoundToInt(ang / 45f) % 8;
        return step switch
        {
            0 => new Vector3( 1, 0,  0),
            1 => new Vector3( 1, 0,  1).normalized,
            2 => new Vector3( 0, 0,  1),
            3 => new Vector3(-1, 0,  1).normalized,
            4 => new Vector3(-1, 0,  0),
            5 => new Vector3(-1, 0, -1).normalized,
            6 => new Vector3( 0, 0, -1),
            _ => new Vector3( 1, 0, -1).normalized,
        };
    }

    void FaceDirection(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return;
        Quaternion toLookDir  = Quaternion.LookRotation(dir.normalized);
        Quaternion meshOffset = Quaternion.FromToRotation(seagullLocalForward.normalized, Vector3.forward);
        transform.rotation = toLookDir * Quaternion.Inverse(meshOffset);
    }

    [Tooltip("How fast the player rotates to track the seagull during passes and dive.")]
    // (set via Inspector through playerTrackSpeed field below)

    IEnumerator FlyTo(Vector3 target, float speed,
        System.Action onFraction = null, float fraction = -1f,
        bool trackPlayer = false)
    {
        Vector3 start    = transform.position;
        float   total    = Vector3.Distance(start, target);
        float   traveled = 0f;
        bool    fired    = false;

        if (total < 0.001f) yield break;

        while (traveled < total)
        {
            traveled = Mathf.Min(traveled + speed * Time.deltaTime, total);
            float t  = traveled / total;
            transform.position = Vector3.Lerp(start, target, t);

            if (trackPlayer) SmoothFacePlayerToward(transform.position);

            if (!fired && fraction >= 0f && t >= fraction)
            {
                onFraction?.Invoke();
                fired = true;
            }

            yield return null;
        }

        transform.position = target;
        if (!fired) onFraction?.Invoke();
    }

    /// <summary>
    /// Flies through a list of waypoints with smooth rotation blending.
    /// Instead of snapping to face the next waypoint on arrival, rotation is
    /// continuously interpolated toward the UPCOMING direction so the turn
    /// starts early and feels natural.
    /// </summary>
    IEnumerator FlyPathSmooth(List<Transform> points, float speed,
        float rotationSpeed = 3f)
    {
        for (int i = 0; i < points.Count - 1; i++)
        {
            var from = points[i];
            var to   = points[i + 1];
            if (!from || !to) continue;

            // Direction toward the point after next — lets rotation start
            // anticipating the upcoming turn before we even reach the waypoint.
            int lookaheadIdx = Mathf.Min(i + 2, points.Count - 1);
            Vector3 lookahead = points[lookaheadIdx] != null
                ? points[lookaheadIdx].position
                : to.position;

            Vector3 segmentDir  = (to.position   - from.position).normalized;
            Vector3 lookaheadDir= (lookahead      - to.position).normalized;

            Vector3 start = from.position;
            float   total = Vector3.Distance(start, to.position);
            float   traveled = 0f;

            if (total < 0.001f) continue;

            while (traveled < total)
            {
                traveled = Mathf.Min(traveled + speed * Time.deltaTime, total);
                float t  = traveled / total;
                transform.position = Vector3.Lerp(start, to.position, t);

                // Blend facing direction: pure segment dir at t=0,
                // blending toward lookahead dir in the second half of the segment.
                float blendT   = Mathf.Clamp01((t - 0.5f) * 2f); // 0..1 over second half
                Vector3 blendedDir = Vector3.Slerp(segmentDir, lookaheadDir, blendT);

                // Smoothly rotate toward the blended direction
                if (blendedDir.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRot  = Quaternion.LookRotation(blendedDir.normalized);
                    Quaternion meshOffset = Quaternion.FromToRotation(
                        seagullLocalForward.normalized, Vector3.forward);
                    Quaternion desired = targetRot * Quaternion.Inverse(meshOffset);
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation, desired,
                        1f - Mathf.Exp(-rotationSpeed * Time.deltaTime));
                }

                yield return null;
            }

            transform.position = to.position;
        }
    }

    IEnumerator CameraFall(Transform cam)
    {
        float vel     = 0f;
        float groundY = 0f;

        // Cast against everything except triggers to find the real ground
        if (Physics.Raycast(cam.position, Vector3.down, out RaycastHit hit, 500f,
            ~0, QueryTriggerInteraction.Ignore))
            groundY = hit.point.y + groundStopOffset;

        while (cam.position.y > groundY + 0.01f)
        {
            vel += cameraFallAccel * Time.deltaTime;
            Vector3 pos = cam.position;
            pos.y = Mathf.Max(pos.y - vel * Time.deltaTime, groundY);
            cam.position = pos;
            cam.Rotate(vel * 0.8f * Time.deltaTime, 0f, vel * 0.4f * Time.deltaTime, Space.Self);
            yield return null;
        }

        // Snap exactly to ground
        Vector3 final = cam.position;
        final.y = groundY;
        cam.position = final;

        // Crash sound played 2D — full volume regardless of where the camera
        // landed. PlayClipAtPoint would attenuate if the camera is far away.
        PlaySfx2D(crashSfx);
    }

    IEnumerator FadeFog(float from, float to, float duration)
    {
        RenderSettings.fog = true;
        float t = 0f;
        duration = Mathf.Max(0.01f, duration);
        while (t < duration)
        {
            t += Time.deltaTime;
            RenderSettings.fogDensity = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        RenderSettings.fogDensity = to;

        // Turn fog off entirely if we've faded it to zero
        if (to <= 0f) RenderSettings.fog = false;
        else          RenderSettings.fog = true;
    }

    IEnumerator Fade(CanvasGroup cg, float from, float to, float duration)
    {
        float t = 0f;
        duration = Mathf.Max(0.01f, duration);
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        cg.alpha = to;
    }

    void HideGameplayUI()
    {
        if (!excludeFromHide) return;
        Transform parent = excludeFromHide.transform.parent;
        if (!parent) return;

        var toHide = new System.Collections.Generic.List<GameObject>();
        foreach (Transform child in parent)
        {
            if (child.gameObject == excludeFromHide) continue;
            if (!child.gameObject.activeSelf) continue;
            child.gameObject.SetActive(false);
            toHide.Add(child.gameObject);
        }
        _hiddenUIObjects = toHide.ToArray();
    }

    void RestoreGameplayUI()
    {
        if (_hiddenUIObjects == null) return;
        foreach (var go in _hiddenUIObjects)
            if (go) go.SetActive(true);
        _hiddenUIObjects = null;
    }

    void SetSeagullVisible(bool visible)
    {
        foreach (var r in GetComponentsInChildren<Renderer>())
            r.enabled = visible;
    }

    /// <summary>3D positional audio — used for pass/steal/reaction SFX.</summary>
    void PlaySfx(AudioClip clip, Vector3 pos)
    {
        if (clip) AudioSource.PlayClipAtPoint(clip, pos, sfxVolume);
    }

    /// <summary>
    /// 2D audio (spatialBlend = 0) — used for the crash so it hits at full
    /// volume no matter where the seagull dropped the camera.
    /// </summary>
    void PlaySfx2D(AudioClip clip)
    {
        if (!clip || !_2dAudioSource) return;
        _2dAudioSource.PlayOneShot(clip, sfxVolume);
    }
}
