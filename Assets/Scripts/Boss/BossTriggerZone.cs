using System.Collections;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class BossTriggerZone : MonoBehaviour
{
    [Header("Refs")]
    public CafeteriaLadyBoss boss;
    public PrincipalBoss     principalBoss;
    public BossHazardZone    hazardZone;

    [Header("Pre-Fight Dialogue (optional)")]
    [Tooltip("Assign a DialogueInteractable on the boss NPC. "
           + "If set, this dialogue plays before the fight starts. "
           + "Leave empty to start the fight immediately.")]
    public DialogueInteractable preFightDialogue;
    [Tooltip("The boss NPC GameObject — its NPCAI is disabled while dialogue plays "
           + "so it stands still. Auto-found from CafeteriaLadyBoss/PrincipalBoss if left empty.")]
    public GameObject bossNpcObject;

    [Header("Settings")]
    public string triggerTag = "Player";
    [Tooltip("Optionally block the entrance once the fight starts.")]
    public GameObject arenaGate;

    bool _triggered;

    void Awake()
    {
        GetComponent<BoxCollider>().isTrigger = true;

        // Auto-find the boss NPC object if not explicitly assigned
        if (!bossNpcObject)
        {
            if (boss)          bossNpcObject = boss.gameObject;
            else if (principalBoss) bossNpcObject = principalBoss.gameObject;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (_triggered) return;
        if (!other.CompareTag(triggerTag)) return;
        _triggered = true;

        if (preFightDialogue)
            StartCoroutine(DialogueThenFight());
        else
            StartFight();

        // NOTE: Do NOT call gameObject.SetActive(false) here.
        // SetActive(false) kills all running coroutines on this GameObject, which would
        // prevent DialogueThenFight() from ever reaching StartFight().
        // _triggered = true above is sufficient to prevent re-entry.
    }

    // ──────────────────────────────────────────
    IEnumerator DialogueThenFight()
    {
        // Freeze the boss AI so it just stands there during dialogue
        NPCAI bossAI = bossNpcObject ? bossNpcObject.GetComponent<NPCAI>() : null;
        NPCMovement bossMover = bossNpcObject ? bossNpcObject.GetComponent<NPCMovement>() : null;

        if (bossAI)    bossAI.enabled    = false;
        if (bossMover) bossMover.enabled = false;

        // Make the boss face the player
        if (bossNpcObject && DialogueController.I && DialogueController.I.player)
        {
            Vector3 dir = DialogueController.I.player.transform.position
                          - bossNpcObject.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
                bossNpcObject.transform.rotation =
                    Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        // Explicitly lock player movement — do NOT rely on DialogueController to do this.
        PlayerMovement playerMover = null;
        var playerGO = GameObject.FindWithTag("Player");
        if (playerGO) playerMover = playerGO.GetComponent<PlayerMovement>();
        if (playerMover) { playerMover.StopMovement(); playerMover.canMove = false; }

        // Start the dialogue
        if (DialogueController.I)
            DialogueController.I.Begin(preFightDialogue);
        else
            Debug.LogWarning("[BossTriggerZone] No DialogueController found in scene.");

        // Wait until the dialogue is fully closed
        yield return new WaitUntil(() =>
            DialogueController.I == null || !DialogueController.I.IsDialogueOpen);

        // Restore player movement now that dialogue is done
        if (playerMover) playerMover.canMove = true;

        // Brief pause so the UI fully closes before the fight music / bar appears
        yield return new WaitForSeconds(0.25f);

        // Re-enable the boss AI and start the fight
        if (bossAI)    bossAI.enabled    = true;
        if (bossMover) bossMover.enabled = true;

        StartFight();
    }

    // ──────────────────────────────────────────
    void StartFight()
    {
        // Disable all DialogueInteractables on the boss so the player can't talk to it mid-fight.
        // This covers both the "dialogue then fight" path and the immediate-start path.
        if (bossNpcObject)
        {
            foreach (var di in bossNpcObject.GetComponents<DialogueInteractable>())
                di.enabled = false;
        }
        // Also explicitly handle the preFightDialogue reference in case it lives elsewhere
        if (preFightDialogue) preFightDialogue.enabled = false;

        if (boss)          boss.ActivateFight();
        if (principalBoss) principalBoss.ActivateFight();
        if (hazardZone)    hazardZone.Activate();
        if (arenaGate)     arenaGate.SetActive(true);
    }

    // ──────────────────────────────────────────
    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 1f, 0f, 0.25f);
        var col = GetComponent<BoxCollider>();
        if (col) Gizmos.DrawCube(
            transform.TransformPoint(col.center),
            Vector3.Scale(transform.lossyScale, col.size));
    }
}
