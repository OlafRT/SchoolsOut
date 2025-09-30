using System.Collections;
using UnityEngine;
using UnityEngine.Events;

#if CINEMACHINE
using Cinemachine;
#endif

[RequireComponent(typeof(Collider))]
public class HackingStation : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private Transform player;
    [SerializeField] private MonoBehaviour[] disableWhileHacking;

    [Header("UI (shared prompt)")]
    [SerializeField] private GameObject promptUI;          // same prompt for both classes (e.g. "Press E")
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Header("Puzzle (Nerd only)")]
    [SerializeField] private GameObject puzzleRoot;
    [SerializeField] private FlowBoard flowBoard;
    [SerializeField] private bool resetPuzzleOnEnter = true;

    [Header("Fusebox Door (opens on attempt)")]
    [SerializeField] private Transform fuseboxDoorHinge;
    [SerializeField] private Vector3 fuseboxDoorOpenLocalEuler = new Vector3(0, -90, 0);
    [SerializeField] private float fuseboxOpenSeconds = 0.6f;

    [Header("World Door (opens after solving - Nerd)")]
    [SerializeField] private Transform worldDoorHinge;
    [SerializeField] private Vector3 worldDoorOpenLocalEuler = new Vector3(0, 90, 0);
    [SerializeField] private float worldDoorOpenSeconds = 0.8f;

    [Header("Camera - choose one mode")]
    [SerializeField] private bool useCinemachine = false;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Transform puzzleCamPose;
    [SerializeField] private Transform playerCamPose;
    [SerializeField] private float camMoveSeconds = 0.7f;
    [SerializeField] private AnimationCurve camEase = AnimationCurve.EaseInOut(0,0,1,1);

#if CINEMACHINE
    [Header("Cinemachine (optional)")]
    [SerializeField] private CinemachineVirtualCamera vcamPuzzle;
    [SerializeField] private CinemachineVirtualCamera vcamPlayer;
#endif

    [Header("SFX (optional)")]
    [SerializeField] private AudioSource sfx;
    [SerializeField] private AudioClip doorOpenClip;
    [SerializeField] private AudioClip doorCloseClip;
    [SerializeField] private AudioClip smashImpactClip;
    [SerializeField] private AudioClip shockClip;
    [SerializeField] private AudioClip thudClip;

    [Header("Behavior")]
    [SerializeField] private bool allowCancelWithInteractKey = true; // Nerd only
    [SerializeField] private KeyCode cancelKey = KeyCode.E;          // Nerd only
    [SerializeField] private bool lockAfterSolved = true;            // Nerd only
    [SerializeField] private bool disableTriggerAfterLock = true;    // Nerd only

    [Header("Jock Attempt UI")]
    [Tooltip("UI shown to Jock when he 'attempts' to hack (your messy image).")]
    [SerializeField] private GameObject jockAttemptUI;
    [SerializeField] private KeyCode jockSmashKey = KeyCode.F;       // press to smash from the fake UI

    [Header("Jock Smash Flow")]
    [SerializeField] private Animator jockAnimator;                  // Jock model animator
    [SerializeField] private string jockSmashTrigger = "Smash";
    [SerializeField] private string jockShockTrigger = "Shock";
    [SerializeField] private string jockFallTrigger  = "Fall";
    [SerializeField] private GameObject shockFX;                     // sparks on the box (inactive by default)

    [Header("Events")]
    public UnityEvent onHackStart;
    public UnityEvent onHackEnd;
    public UnityEvent onSolvedDoorOpening;
    public UnityEvent onJockSmashStarted;
    public UnityEvent onJockShocked;
    public UnityEvent onJockFell;

    // ----- runtime -----
    private bool playerInside;
    private bool hacking;     // Nerd puzzle active
    private bool exiting;     // Nerd puzzle exiting
    private bool solved;
    private bool locked;      // after nerd solves (or after jock smashes)

    private bool jockUIOpen;  // Jock fake-hack UI is shown
    private bool jockBusy;    // Jock smash sequence is running
    private bool nerdCancelArmed = false;

    // NEW: becomes true after Jock smashes; station is unusable thereafter
    private bool stationDestroyed = false;

    private Quaternion fuseboxDoorClosedRot;
    private Quaternion worldDoorClosedRot;
    private Collider triggerCol;

    // manual cam cache
    private Vector3 camOrigPos;
    private Quaternion camOrigRot;
    private Transform camOrigParent;

    // static relay target for JockAnimRelay
    private static HackingStation _currentJockStation;

    void Awake()
    {
        triggerCol = GetComponent<Collider>();
        triggerCol.isTrigger = true;

        if (!mainCamera) mainCamera = Camera.main;

        if (puzzleRoot)    puzzleRoot.SetActive(false);
        if (promptUI)      promptUI.SetActive(false);
        if (jockAttemptUI) jockAttemptUI.SetActive(false);
        if (shockFX)       shockFX.SetActive(false);

        if (fuseboxDoorHinge) fuseboxDoorClosedRot = fuseboxDoorHinge.localRotation;
        if (worldDoorHinge)   worldDoorClosedRot   = worldDoorHinge.localRotation;

        if (flowBoard)
        {
            flowBoard.onSolved.AddListener(HandleSolved);
            flowBoard.onTimeExpired.AddListener(HandleTimeExpired); // NEW
        }
    }

    void OnDestroy()
    {
        if (flowBoard)
        {
            flowBoard.onSolved.RemoveListener(HandleSolved);
            flowBoard.onTimeExpired.RemoveListener(HandleTimeExpired); // NEW
        }
        if (_currentJockStation == this) _currentJockStation = null;
    }

    // ----- triggers -----
    void OnTriggerEnter(Collider other)
    {
        if (IsPlayer(other))
        {
            playerInside = true;
            if (!locked && !stationDestroyed && promptUI) promptUI.SetActive(true);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (IsPlayer(other))
        {
            playerInside = false;
            if (promptUI)      promptUI.SetActive(false);
            if (jockUIOpen)    ForceCloseJockUI(); // safety if they walk away
        }
    }

    bool IsPlayer(Collider c)
    {
        if (player) return c.transform == player;
        return c.CompareTag("Player");
    }

    void Update()
    {
        if (!playerInside || locked || stationDestroyed) return;

        // Jock fake UI input handling
        if (jockUIOpen)
        {
            if (Input.GetKeyDown(interactKey))
            {
                StartCoroutine(JockExitAttemptRoutine());
                return;
            }

            if (Input.GetKeyDown(jockSmashKey) && !jockBusy)
            {
                StartCoroutine(JockSmashRoutineFromUI());
                return;
            }

            return; // while jock UI is up, ignore other inputs
        }

        // Start flows when pressing E at the prompt
        if (Input.GetKeyDown(interactKey))
        {
            if (IsNerd())
            {
                if (!hacking && !exiting)
                    StartCoroutine(NerdBeginHackRoutine());
            }
            else
            {
                if (!jockBusy)
                    StartCoroutine(JockBeginAttemptRoutine()); // open fake UI instead of puzzle
            }
        }

        // Nerd cancel (only after we armed it)
        if (hacking && !exiting && allowCancelWithInteractKey && nerdCancelArmed && Input.GetKeyDown(cancelKey))
        {
            StartCoroutine(NerdAbortRoutine());
        }
    }

    // ----- class helpers -----
    private bool IsNerd()
    {
        var sess = GameSession.Instance;
        if (sess != null)
            return sess.SelectedClass == GameSession.ClassType.Nerd;

        Transform p = player ? player : FindPlayerRoot();
        if (!p) return false;
        var stats = p.GetComponent<PlayerStats>();
        if (stats) return stats.playerClass == PlayerStats.PlayerClass.Nerd;
        return false;
    }

    private Transform FindPlayerRoot()
    {
        if (player) return player;
        var go = GameObject.FindGameObjectWithTag("Player");
        return go ? go.transform : null;
    }

    // =======================
    // NERD FLOW
    // =======================
    IEnumerator NerdBeginHackRoutine()
    {
        hacking = true;
        solved = false;
        exiting = false;
        nerdCancelArmed = false;

        if (promptUI) promptUI.SetActive(false);
        foreach (var b in disableWhileHacking) if (b) b.enabled = false;

        if (fuseboxDoorHinge)
            StartCoroutine(RotateLocal(fuseboxDoorHinge, fuseboxDoorClosedRot, Quaternion.Euler(fuseboxDoorOpenLocalEuler), fuseboxOpenSeconds));
        if (doorOpenClip && sfx) sfx.PlayOneShot(doorOpenClip);

        onHackStart?.Invoke();

        // move camera to puzzle (same helper used by Jock attempt)
        yield return MoveCameraToPuzzle();

        if (puzzleRoot) puzzleRoot.SetActive(true);
        if (resetPuzzleOnEnter && flowBoard) flowBoard.BuildBoard();
        if (flowBoard) flowBoard.ResetTimerAndStart();

        nerdCancelArmed = true;
    }

    void HandleSolved()
    {
        if (flowBoard) flowBoard.StopTimerWithoutExpire();
        if (!hacking || exiting) return;
        solved = true;
        if (lockAfterSolved) locked = true;
        StartCoroutine(NerdExitAfterSolvedRoutine());
    }

    IEnumerator NerdExitAfterSolvedRoutine()
    {
        exiting = true;
        if (puzzleRoot) puzzleRoot.SetActive(false);

        nerdCancelArmed = false;

        // Fire event and start opening the world door, but DO NOT wait for it.
        onSolvedDoorOpening?.Invoke();
        if (worldDoorHinge)
            StartCoroutine(RotateLocal(worldDoorHinge, worldDoorClosedRot,
                                    Quaternion.Euler(worldDoorOpenLocalEuler),
                                    worldDoorOpenSeconds)); // ← fire-and-forget

        // Immediately return camera to the player so they can SEE the door opening.
        yield return MoveCameraToPlayer();

        // Close the fusebox door after we’re back at the player.
        if (fuseboxDoorHinge)
        {
            if (doorCloseClip && sfx) sfx.PlayOneShot(doorCloseClip);
            yield return RotateLocal(fuseboxDoorHinge, fuseboxDoorHinge.localRotation, fuseboxDoorClosedRot, 0.5f);
        }

        FinalizeEndCommon();
        if (locked && disableTriggerAfterLock && triggerCol) triggerCol.enabled = false;
    }

    IEnumerator NerdAbortRoutine()
    {
        if (flowBoard) flowBoard.StopTimerWithoutExpire();
        exiting = true;
        if (puzzleRoot) puzzleRoot.SetActive(false);
        nerdCancelArmed = false;

        yield return MoveCameraToPlayer();

        if (fuseboxDoorHinge)
        {
            if (doorCloseClip && sfx) sfx.PlayOneShot(doorCloseClip);
            yield return RotateLocal(fuseboxDoorHinge, fuseboxDoorHinge.localRotation, fuseboxDoorClosedRot, 0.4f);
        }

        hacking = false;
        FinalizeEndCommon();
    }

    // =======================
    // JOCK FLOW – ATTEMPT -> FAKE UI
    // =======================
    IEnumerator JockBeginAttemptRoutine()
    {
        if (promptUI) promptUI.SetActive(false);
        foreach (var b in disableWhileHacking) if (b) b.enabled = false;

        // Door open + SFX (same as Nerd)
        if (fuseboxDoorHinge)
            StartCoroutine(RotateLocal(fuseboxDoorHinge, fuseboxDoorClosedRot,
                                    Quaternion.Euler(fuseboxDoorOpenLocalEuler), fuseboxOpenSeconds));
        if (doorOpenClip && sfx) sfx.PlayOneShot(doorOpenClip);

        // 🔧 NEW: fire the same event Nerd uses so your CameraFollow gets disabled
        onHackStart?.Invoke();

        // Move to the SAME puzzle cam pose as Nerd
        yield return MoveCameraToPuzzle();

        // Show the fake Jock hacking UI
        jockUIOpen = true;
        if (jockAttemptUI) jockAttemptUI.SetActive(true);
    }

    IEnumerator JockExitAttemptRoutine()
    {
        // Hide fake UI and back out
        jockUIOpen = false;
        if (jockAttemptUI) jockAttemptUI.SetActive(false);

        yield return MoveCameraToPlayer();

        if (fuseboxDoorHinge)
        {
            if (doorCloseClip && sfx) sfx.PlayOneShot(doorCloseClip);
            yield return RotateLocal(fuseboxDoorHinge, fuseboxDoorHinge.localRotation, fuseboxDoorClosedRot, 0.4f);
        }

        FinalizeEndCommon();
    }

    IEnumerator JockSmashRoutineFromUI()
    {
        jockBusy = true;
        _currentJockStation = this;

        // Hide fake UI
        jockUIOpen = false;
        if (jockAttemptUI) jockAttemptUI.SetActive(false);

        // ### NEW: move camera BACK TO PLAYER before we start the smash,
        // so the player sees the shock gag clearly.
        yield return MoveCameraToPlayer();

        // Station is now ruined: prevent any future attempts right away.
        stationDestroyed = true;
        locked = true; // reuse lock semantics to hide prompt & block interactions
        if (promptUI) promptUI.SetActive(false);
        if (disableTriggerAfterLock && triggerCol) triggerCol.enabled = false;

        onJockSmashStarted?.Invoke();

        // Start the smash animation (events will drive impact/shock/fall)
        if (jockAnimator) jockAnimator.SetTrigger(jockSmashTrigger);

        // We let the animation events finish the sequence; no immediate wrap-up here.
        yield break;
    }

    private void ForceCloseJockUI()
    {
        jockUIOpen = false;
        if (jockAttemptUI) jockAttemptUI.SetActive(false);
        // Don’t move camera/door here; we only force-hide the UI on exit-safety.
    }

    // ======= Jock animation-event relays (called from JockAnimRelay on the Jock model) =======
    public static void NotifyJockSmashImpact()
    {
        var st = _currentJockStation;
        if (!st) return;
        if (st.smashImpactClip && st.sfx) st.sfx.PlayOneShot(st.smashImpactClip);
    }

    public static void NotifyJockEnableShockFX()
    {
        var st = _currentJockStation;
        if (!st) return;
        if (st.shockFX) st.shockFX.SetActive(true);
        if (st.shockClip && st.sfx) st.sfx.PlayOneShot(st.shockClip);
        if (st.jockAnimator) st.jockAnimator.SetTrigger(st.jockShockTrigger);
        st.onJockShocked?.Invoke();
    }

    public static void NotifyJockShock()
    {
        var st = _currentJockStation;
        if (!st) return;
        if (st.jockAnimator) st.jockAnimator.SetTrigger(st.jockShockTrigger);
        st.onJockShocked?.Invoke();
    }

    public static void NotifyJockFall()
    {
        var st = _currentJockStation;
        if (!st) return;
        if (st.jockAnimator) st.jockAnimator.SetTrigger(st.jockFallTrigger);
        if (st.thudClip && st.sfx) st.sfx.PlayOneShot(st.thudClip);
        st.onJockFell?.Invoke();

        // After the gag resolves, we do not re-open the station (it's destroyed).
        st.StartCoroutine(st.JockWrapUpAfterFall());
    }

    public static void NotifyJockDisableShockFX()
    {
        var st = _currentJockStation;
        if (!st) return;
        if (st.shockFX) st.shockFX.SetActive(false);
    }

    private IEnumerator JockWrapUpAfterFall()
    {
        // Optional tiny pause to let the fall land
        yield return new WaitForSeconds(0.35f);

        // Camera is already at player. We just close the fusebox door if you want it shut.
        if (fuseboxDoorHinge)
        {
            if (doorCloseClip && sfx) sfx.PlayOneShot(doorCloseClip);
            yield return RotateLocal(fuseboxDoorHinge, fuseboxDoorHinge.localRotation, fuseboxDoorClosedRot, 0.4f);
        }

        jockBusy = false;
        _currentJockStation = null;
        FinalizeEndCommon();
    }

    private void HandleTimeExpired()
    {
        // Timer ran out while the Nerd puzzle was active (or just finished).
        // 1) Permanently disable further use of this station
        stationDestroyed = true;
        locked = true;

        // 2) Show the same shock particles the Jock gets (no audio per your request)
        if (shockFX) shockFX.SetActive(true);

        // 3) Stop input & hide the puzzle UI if it’s still up
        if (puzzleRoot) puzzleRoot.SetActive(false);
        nerdCancelArmed = false;

        // 4) Hide prompt and block the trigger if you use that toggle
        if (promptUI) promptUI.SetActive(false);
        if (disableTriggerAfterLock && triggerCol) triggerCol.enabled = false;

        // 5) Cleanly return the camera to the player and close the fusebox door
        //    (and re-enable the things you disabled via On Hack Start through onHackEnd)
        StartCoroutine(TimeExpiredExitRoutine());
    }

    private System.Collections.IEnumerator TimeExpiredExitRoutine()
    {
        // Move camera back
        yield return MoveCameraToPlayer();

        // Close fusebox door (quietly)
        if (fuseboxDoorHinge)
            yield return RotateLocal(fuseboxDoorHinge, fuseboxDoorHinge.localRotation, fuseboxDoorClosedRot, 0.4f);

        // Re-enable any disabled player scripts, fire On Hack End, and clear flags
        FinalizeEndCommon();
    }

    // ----- camera helpers -----
    IEnumerator MoveCameraToPuzzle()
    {
#if CINEMACHINE
        if (useCinemachine && vcamPuzzle && vcamPlayer)
        {
            vcamPlayer.Priority = 10;
            vcamPuzzle.Priority = 20;
            yield return new WaitForSeconds(camMoveSeconds);
            yield break;
        }
#endif
        if (!mainCamera || !puzzleCamPose) yield break;
        CacheCamIfNeeded();
        yield return SmoothMove(mainCamera.transform, puzzleCamPose.position, puzzleCamPose.rotation, camMoveSeconds, camEase);
    }

    IEnumerator MoveCameraToPlayer()
    {
#if CINEMACHINE
        if (useCinemachine && vcamPuzzle && vcamPlayer)
        {
            vcamPuzzle.Priority = 10;
            vcamPlayer.Priority = 20;
            yield return new WaitForSeconds(camMoveSeconds);
            yield break;
        }
#endif
        if (!mainCamera) yield break;

        if (playerCamPose)
            yield return SmoothMove(mainCamera.transform, playerCamPose.position, playerCamPose.rotation, camMoveSeconds, camEase);
        else
            yield return SmoothMove(mainCamera.transform, camOrigPos, camOrigRot, camMoveSeconds, camEase);

        if (camOrigParent) mainCamera.transform.SetParent(camOrigParent);
    }

    void CacheCamIfNeeded()
    {
        if (!mainCamera) return;
        camOrigParent = mainCamera.transform.parent;
        camOrigPos    = mainCamera.transform.position;
        camOrigRot    = mainCamera.transform.rotation;
        mainCamera.transform.SetParent(null, true);
    }

    IEnumerator SmoothMove(Transform t, Vector3 toPos, Quaternion toRot, float secs, AnimationCurve curve)
    {
        Vector3 fromPos = t.position;
        Quaternion fromRot = t.rotation;
        float t0 = Time.time;
        float dur = Mathf.Max(0.0001f, secs);

        while (true)
        {
            float u = Mathf.Clamp01((Time.time - t0) / dur);
            float w = curve != null ? curve.Evaluate(u) : u;
            t.position = Vector3.LerpUnclamped(fromPos, toPos, w);
            t.rotation = Quaternion.SlerpUnclamped(fromRot, toRot, w);
            if (u >= 1f) break;
            yield return null;
        }
    }

    IEnumerator RotateLocal(Transform tr, Quaternion from, Quaternion to, float secs)
    {
        if (!tr) yield break;
        float t0 = Time.time;
        float dur = Mathf.Max(0.0001f, secs);
        while (true)
        {
            float u = Mathf.Clamp01((Time.time - t0) / dur);
            tr.localRotation = Quaternion.SlerpUnclamped(from, to, u);
            if (u >= 1f) break;
            yield return null;
        }
    }

    // ----- shared teardown -----
    private void FinalizeEndCommon()
    {
        foreach (var b in disableWhileHacking) if (b) b.enabled = true;

        onHackEnd?.Invoke();

        // Hide prompt if solved/locked or station destroyed
        if (promptUI) promptUI.SetActive(playerInside && !locked && !stationDestroyed);

        // reset per-flow flags
        hacking = false;
        exiting = false;
        jockUIOpen = false;
        nerdCancelArmed = false;
        // jockBusy is reset in JockWrapUpAfterFall()
    }
}
