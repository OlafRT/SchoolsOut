using System.Collections;
using UnityEngine;
using UnityEngine.Events;

#if CINEMACHINE
using Cinemachine;
#endif

[RequireComponent(typeof(Collider))]
public class PuzzleStation : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private Transform player;
    [SerializeField] private MonoBehaviour[] disableWhileHacking;

    [Header("UI")]
    [SerializeField] private GameObject promptUI;
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Header("Puzzle")]
    [SerializeField] private GameObject puzzleRoot;
    [SerializeField] private FlowBoard flowBoard;
    [SerializeField] private bool resetPuzzleOnEnter = true; // rebuild level when entering puzzle

    [Header("Fusebox Door (opens when you start hacking)")]
    [SerializeField] private Transform fuseboxDoorHinge;
    [SerializeField] private Vector3 fuseboxDoorOpenLocalEuler = new Vector3(0, -90, 0);
    [SerializeField] private float fuseboxOpenSeconds = 0.6f;

    [Header("World Door (opens after solving)")]
    [SerializeField] private Transform worldDoorHinge;
    [SerializeField] private Vector3 worldDoorOpenLocalEuler = new Vector3(0, 90, 0);
    [SerializeField] private float worldDoorOpenSeconds = 0.8f;

    [Header("Camera - choose one mode")]
    [SerializeField] private bool useCinemachine = false;

    [SerializeField] private Camera mainCamera;          // manual mode camera
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

    [Header("Behavior")]
    [SerializeField] private bool allowCancelWithInteractKey = true;   // press E again to exit
    [SerializeField] private KeyCode cancelKey = KeyCode.E;            // can change to Escape, etc.
    [SerializeField] private bool lockAfterSolved = true;              // prevent re-use after success
    [SerializeField] private bool disableTriggerAfterLock = true;      // disable collider AFTER exit finishes

    [Header("Events")]
    public UnityEvent onHackStart;
    public UnityEvent onHackEnd;
    public UnityEvent onSolvedDoorOpening;

    // ----- runtime -----
    private bool playerInside;
    private bool hacking;
    private bool exiting;
    private bool solved;
    private bool locked;
    private Quaternion fuseboxDoorClosedRot;
    private Quaternion worldDoorClosedRot;
    private Collider triggerCol;

    // manual cam cache
    private Vector3 camOrigPos;
    private Quaternion camOrigRot;
    private Transform camOrigParent;

    void Awake()
    {
        triggerCol = GetComponent<Collider>();
        triggerCol.isTrigger = true;

        if (!mainCamera) mainCamera = Camera.main;
        if (puzzleRoot) puzzleRoot.SetActive(false);
        if (promptUI) promptUI.SetActive(false);

        if (fuseboxDoorHinge) fuseboxDoorClosedRot = fuseboxDoorHinge.localRotation;
        if (worldDoorHinge)   worldDoorClosedRot   = worldDoorHinge.localRotation;

        if (flowBoard) flowBoard.onSolved.AddListener(HandleSolved);
    }

    void OnDestroy()
    {
        if (flowBoard) flowBoard.onSolved.RemoveListener(HandleSolved);
    }

    // ----- trigger -----
    void OnTriggerEnter(Collider other)
    {
        if (IsPlayer(other))
        {
            playerInside = true;
            if (!hacking && !locked && promptUI) promptUI.SetActive(true);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (IsPlayer(other))
        {
            playerInside = false;
            if (promptUI) promptUI.SetActive(false);
        }
    }

    bool IsPlayer(Collider c)
    {
        return !player || c.transform == player || c.CompareTag("Player");
    }

    void Update()
    {
        // Start hacking
        if (playerInside && !hacking && !locked && Input.GetKeyDown(interactKey))
        {
            StartCoroutine(BeginHackRoutine());
            return;
        }

        // Cancel/exit while hacking
        if (hacking && !exiting && allowCancelWithInteractKey && Input.GetKeyDown(cancelKey))
        {
            StartCoroutine(AbortRoutine());
        }
    }

    // ----- main sequences -----
    IEnumerator BeginHackRoutine()
    {
        hacking = true;
        solved = false;
        exiting = false;
        if (promptUI) promptUI.SetActive(false);

        // disable player control
        foreach (var b in disableWhileHacking) if (b) b.enabled = false;

        // open fusebox + camera in
        if (fuseboxDoorHinge) StartCoroutine(RotateLocal(fuseboxDoorHinge, fuseboxDoorClosedRot, Quaternion.Euler(fuseboxDoorOpenLocalEuler), fuseboxOpenSeconds));
        if (doorOpenClip && sfx) sfx.PlayOneShot(doorOpenClip);
        onHackStart?.Invoke();

        // camera to puzzle
        yield return MoveCameraToPuzzle();

        // show puzzle
        if (puzzleRoot) puzzleRoot.SetActive(true);

        // rebuild puzzle on reset
        if (resetPuzzleOnEnter && flowBoard) flowBoard.BuildBoard();
    }

    void HandleSolved()
    {
        if (!hacking || exiting) return;
        solved = true;
        if (lockAfterSolved) locked = true;   // lock immediately (we’ll optionally disable collider after exit)
        StartCoroutine(ExitAfterSolvedRoutine());
    }

    IEnumerator ExitAfterSolvedRoutine()
    {
        exiting = true;

        if (puzzleRoot) puzzleRoot.SetActive(false);

        onSolvedDoorOpening?.Invoke();
        if (worldDoorHinge)
            yield return RotateLocal(worldDoorHinge, worldDoorClosedRot, Quaternion.Euler(worldDoorOpenLocalEuler), worldDoorOpenSeconds);

        yield return MoveCameraToPlayer();

        if (fuseboxDoorHinge)
        {
            if (doorCloseClip && sfx) sfx.PlayOneShot(doorCloseClip);
            yield return RotateLocal(fuseboxDoorHinge, fuseboxDoorHinge.localRotation, fuseboxDoorClosedRot, 0.5f);
        }

        FinalizeHackEnd(solvedExit: true);
    }

    public void AbortHack()
    {
        if (!hacking || exiting) return;
        StartCoroutine(AbortRoutine());
    }

    IEnumerator AbortRoutine()
    {
        exiting = true;

        if (puzzleRoot) puzzleRoot.SetActive(false);

        // back to player cam
        yield return MoveCameraToPlayer();

        // close fusebox since we didn’t solve
        if (fuseboxDoorHinge)
        {
            if (doorCloseClip && sfx) sfx.PlayOneShot(doorCloseClip);
            yield return RotateLocal(fuseboxDoorHinge, fuseboxDoorHinge.localRotation, fuseboxDoorClosedRot, 0.4f);
        }

        FinalizeHackEnd(solvedExit: false);
    }

    // ----- camera helpers -----
    IEnumerator MoveCameraToPuzzle()
    {
#if CINEMACHINE
        if (useCinemachine && vcamPuzzle && vcamPlayer)
        {
            vcamPuzzle.Priority = 11;
            vcamPlayer.Priority = 10;
            yield return null; // let CM Brain blend
            yield break;
        }
#endif
        if (!mainCamera || !puzzleCamPose) yield break;

        camOrigParent = mainCamera.transform.parent;
        camOrigPos    = mainCamera.transform.position;
        camOrigRot    = mainCamera.transform.rotation;

        yield return SmoothMove(mainCamera.transform, puzzleCamPose.position, puzzleCamPose.rotation, camMoveSeconds, camEase);
    }

    IEnumerator MoveCameraToPlayer()
    {
#if CINEMACHINE
        if (useCinemachine && vcamPuzzle && vcamPlayer)
        {
            vcamPuzzle.Priority = 10;
            vcamPlayer.Priority = 11;
            yield return null;
            yield break;
        }
#endif
        if (!mainCamera) yield break;

        Vector3 targetPos; Quaternion targetRot;
        if (playerCamPose)
        {
            targetPos = playerCamPose.position;
            targetRot = playerCamPose.rotation;
        }
        else
        {
            targetPos = camOrigPos;
            targetRot = camOrigRot;
        }

        yield return SmoothMove(mainCamera.transform, targetPos, targetRot, camMoveSeconds, camEase);
        if (camOrigParent) mainCamera.transform.SetParent(camOrigParent, true);
    }

    IEnumerator SmoothMove(Transform t, Vector3 toPos, Quaternion toRot, float secs, AnimationCurve curve)
    {
        Vector3 fromPos = t.position;
        Quaternion fromRot = t.rotation;
        secs = Mathf.Max(0.0001f, secs);
        float t0 = 0f;

        while (t0 < 1f)
        {
            t0 += Time.deltaTime / secs;
            float k = curve.Evaluate(Mathf.Clamp01(t0));
            t.position = Vector3.LerpUnclamped(fromPos, toPos, k);
            t.rotation = Quaternion.SlerpUnclamped(fromRot, toRot, k);
            yield return null;
        }
        t.position = toPos;
        t.rotation = toRot;
    }

    IEnumerator RotateLocal(Transform tr, Quaternion from, Quaternion to, float secs)
    {
        secs = Mathf.Max(0.0001f, secs);
        float t0 = 0f;
        while (t0 < 1f)
        {
            t0 += Time.deltaTime / secs;
            tr.localRotation = Quaternion.SlerpUnclamped(from, to, t0);
            yield return null;
        }
        tr.localRotation = to;
    }

    void FinalizeHackEnd(bool solvedExit)
    {
        // re-enable controls
        foreach (var b in disableWhileHacking) if (b) b.enabled = true;

        // fire the shared end event
        onHackEnd?.Invoke();

        // lock station after a solve (we set locked=true when solved)
        if (solvedExit && lockAfterSolved && disableTriggerAfterLock && triggerCol)
            triggerCol.enabled = false;

        // prompt visibility after exit
        if (promptUI) promptUI.SetActive(playerInside && !locked);

        hacking = false;
        exiting = false;
    }
}
