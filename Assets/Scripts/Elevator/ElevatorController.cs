using UnityEngine;
using System.Collections;

/// <summary>
/// The main brain for the elevator. Drag references in from the Inspector.
/// Call OpenExteriorDoors() from ElevatorButton (call button outside),
/// and StartRide() from ElevatorButton (panel button inside).
/// </summary>
public class ElevatorController : MonoBehaviour
{
    // ── Exterior Doors (outside, called by the call button) ─────────────────
    [Header("Exterior Doors")]
    public ElevatorDoor exteriorDoorLeft;
    public ElevatorDoor exteriorDoorRight;

    // ── Interior Doors (inside, closed when ride starts, opened on arrival) ─
    [Header("Interior Doors")]
    public ElevatorDoor interiorDoorLeft;
    public ElevatorDoor interiorDoorRight;

    // ── Mesh to vibrate while "moving" ──────────────────────────────────────
    [Header("Vibration")]
    [Tooltip("The visual-only mesh transform to shake. Should NOT be the collider root.")]
    public Transform vibrationTarget;
    [Tooltip("Max random offset per axis each frame (world units).")]
    public float vibrationIntensity = 0.003f;

    // ── Emissive Arrow Material ──────────────────────────────────────────────
    [Header("Emission")]
    [Tooltip("Drag the renderer(s) here.")]
    public Renderer[] arrowRenderers;
    [Tooltip("Name of the emission color property on the material.")]
    public string emissionColorProperty = "_EmissionColor";
    [Tooltip("The base colour of the arrow (tint before intensity is applied).")]
    public Color arrowColor = Color.yellow;
    [Tooltip("HDR emission intensity when OFF (matching your current -10 setting).")]
    public float emissionOff = -10f;
    [Tooltip("HDR emission intensity when ON (your target of 5).")]
    public float emissionOn  =  5f;
    [Tooltip("Seconds to ramp emission up or down.")]
    public float emissionRampDuration = 0.4f;
    Material[] arrowMats;

    // ── Audio ────────────────────────────────────────────────────────────────
    [Header("Audio")]
    [Tooltip("Played when either set of doors opens.")]
    public AudioClip doorsOpenClip;

    [Tooltip("Played when interior doors close at ride start.")]
    public AudioClip doorsCloseClip;

    [Tooltip("Looped while the elevator is 'moving'.")]
    public AudioClip humClip;

    [Tooltip("Elevator music played during the ride (looped).")]
    public AudioClip musicClip;

    [Tooltip("Played at the end of the ride (e.g. '20th floor, Penthouse').")]
    public AudioClip arrivalClip;

    [Range(0f, 1f)] public float sfxVolume   = 1f;
    [Range(0f, 1f)] public float humVolume   = 0.5f;
    [Range(0f, 1f)] public float musicVolume = 0.4f;

    // ── Floor Objects ────────────────────────────────────────────────────────
    [Header("Floor Objects")]
    [Tooltip("Active on the ground floor. Disabled when the elevator departs.")]
    public GameObject floorOneRoot;
    [Tooltip("Active on the top floor. Enabled when the elevator arrives.")]
    public GameObject floorTwoRoot;

    // ── Ride Settings ────────────────────────────────────────────────────────
    [Header("Ride")]
    [Tooltip("How long (seconds) the elevator 'travels' before arrival.")]
    public float rideDuration = 20f;

    // ── State ────────────────────────────────────────────────────────────────
    bool isRiding     = false;
    bool exteriorOpen = false;

    AudioSource humSource;
    AudioSource musicSource;

    Material arrowMat; // instanced copy so we never dirty the shared asset

    // ── Unity ────────────────────────────────────────────────────────────────

    void Awake()
    {
        humSource   = CreateLoopingSource("ElevatorHum",   humClip,   humVolume);
        musicSource = CreateLoopingSource("ElevatorMusic", musicClip, musicVolume);

        // Create one runtime material reference per renderer
        if (arrowRenderers != null && arrowRenderers.Length > 0)
        {
            arrowMats = new Material[arrowRenderers.Length];

            for (int i = 0; i < arrowRenderers.Length; i++)
            {
                if (arrowRenderers[i] != null)
                    arrowMats[i] = arrowRenderers[i].material;
            }
        }

        SetEmission(emissionOff);
    }

    void Start()
    {
        // Done in Start (not Awake) so ElevatorDoor.Awake() has already run and
        // cached closedLocalPos / openLocalPos before we ask it to Open().
        interiorDoorLeft?.Open();
        interiorDoorRight?.Open();

        if (floorOneRoot) floorOneRoot.SetActive(true);
        if (floorTwoRoot) floorTwoRoot.SetActive(false);
    }

    // ── Public API called by ElevatorButton ─────────────────────────────────

    /// <summary>Called by the exterior call button.</summary>
    public void OpenExteriorDoors()
    {
        if (exteriorOpen) return;
        exteriorOpen = true;

        PlaySFX(doorsOpenClip);
        exteriorDoorLeft?.Open();
        exteriorDoorRight?.Open();
    }

    /// <summary>Called by the interior panel button.</summary>
    public void StartRide()
    {
        if (isRiding) return;
        StartCoroutine(RideSequence());
    }

    // ── Ride Sequence ────────────────────────────────────────────────────────

    IEnumerator RideSequence()
    {
        isRiding = true;

        // Turn emission on immediately when the button is pressed
        StartCoroutine(RampEmission(emissionOff, emissionOn, emissionRampDuration));

        // 1. Close both sets of doors and swap floor objects
        PlaySFX(doorsCloseClip);
        exteriorDoorLeft?.Close();
        exteriorDoorRight?.Close();
        interiorDoorLeft?.Close();
        interiorDoorRight?.Close();

        if (floorOneRoot) floorOneRoot.SetActive(false);

        // Wait for doors to finish sliding before starting ride effects
        float doorWait = Mathf.Max(
            interiorDoorLeft  ? interiorDoorLeft.slideDuration  : 0f,
            interiorDoorRight ? interiorDoorRight.slideDuration : 0f
        );
        yield return new WaitForSeconds(doorWait + 0.1f);

        // 2. Hum + music
        humSource.Play();
        if (musicClip) musicSource.Play();

        // 3. Vibrate for the ride duration
        float elapsed = 0f;
        Vector3 baseLocalPos = vibrationTarget ? vibrationTarget.localPosition : Vector3.zero;

        while (elapsed < rideDuration)
        {
            elapsed += Time.deltaTime;

            if (vibrationTarget)
            {
                float vx = Random.Range(-vibrationIntensity, vibrationIntensity);
                float vy = Random.Range(-vibrationIntensity, vibrationIntensity);
                vibrationTarget.localPosition = baseLocalPos + new Vector3(vx, vy, 0f);
            }

            yield return null;
        }

        if (vibrationTarget) vibrationTarget.localPosition = baseLocalPos;

        // 4. Stop hum + music, ramp emission down, play arrival clip
        humSource.Stop();
        musicSource.Stop();
        StartCoroutine(RampEmission(emissionOn, emissionOff, emissionRampDuration));
        PlaySFX(arrivalClip);

        // 5. Short pause, then reveal floor two and open interior doors
        yield return new WaitForSeconds(1.5f);

        if (floorTwoRoot) floorTwoRoot.SetActive(true);

        interiorDoorLeft?.Open();
        interiorDoorRight?.Open();

        isRiding = false;
    }

    // ── Emission ─────────────────────────────────────────────────────────────

    void SetEmission(float intensity)
    {
        if (arrowMats == null) return;

        Color finalColor = arrowColor * Mathf.LinearToGammaSpace(intensity);

        for (int i = 0; i < arrowMats.Length; i++)
        {
            if (arrowMats[i] == null) continue;

            arrowMats[i].EnableKeyword("_EMISSION");
            arrowMats[i].SetColor(emissionColorProperty, finalColor);
        }
    }

    IEnumerator RampEmission(float from, float to, float duration)
    {
        float t = 0f;

        while (t < emissionRampDuration)
        {
            t += Time.deltaTime;
            float value = Mathf.Lerp(from, to, t / emissionRampDuration);
            SetEmission(value);
            yield return null;
        }

        SetEmission(to);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    void PlaySFX(AudioClip clip)
    {
        if (!clip) return;
        AudioSource.PlayClipAtPoint(clip, transform.position, sfxVolume);
    }

    AudioSource CreateLoopingSource(string sourceName, AudioClip clip, float volume)
    {
        var go  = new GameObject(sourceName);
        go.transform.SetParent(transform, false);
        var src = go.AddComponent<AudioSource>();
        src.clip         = clip;
        src.loop         = true;
        src.playOnAwake  = false;
        src.spatialBlend = 0f;
        src.volume       = volume;
        return src;
    }
}