using UnityEngine;
using TMPro;

public class DialogueController : MonoBehaviour
{
    public static DialogueController I;

    [Header("References")]
    public GameObject promptSpacebar;     // UI image for space prompt
    public GameObject dialoguePanel;      // background panel (set inactive at start)
    public TMP_Text dialogueText;         // text component inside the panel
    public PlayerMovement player;         // your existing PlayerMovement

    DialogueInteractable active;
    int idx = 0;

    void Awake()
    {
        I = this;
        if (dialoguePanel) dialoguePanel.SetActive(false);
        if (promptSpacebar) promptSpacebar.SetActive(false);
    }

    bool _externalLock = false;
    public bool IsLocked => _externalLock;
    public void SetExternalLock(bool locked)
    {
        _externalLock = locked;
        ShowPrompt(false); // hide prompt immediately if locked
    }

    public bool IsDialogueOpen => active != null;

    public void ShowPrompt(bool show)
    {
        if (IsDialogueOpen) show = false;
        if (promptSpacebar && promptSpacebar.activeSelf != show)
            promptSpacebar.SetActive(show);
    }

    public void Begin(DialogueInteractable target)
    {
        if (!target || target.GetLineCount() == 0) return;
        active = target;
        idx = 0;

        // freeze player movement while talking
        if (player) player.canMove = false;

        // optional: snap NPC to face player
        if (target.facePlayerOnStart && player)
        {
            Vector3 toPlayer = player.transform.position - target.transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.0001f)
                target.transform.rotation = Quaternion.LookRotation(toPlayer.normalized, Vector3.up);
        }

        if (promptSpacebar) promptSpacebar.SetActive(false);
        if (dialoguePanel) dialoguePanel.SetActive(true);

        // first line
        dialogueText.text = active.GetText(idx);
        active.ApplyPerLineEffects(idx);
    }

    public void Advance()
    {
        if (active == null) return;
        idx++;
        if (idx >= active.GetLineCount())
        {
            End();
            return;
        }
        dialogueText.text = active.GetText(idx);
        active.ApplyPerLineEffects(idx);
    }

    public void End()
    {
        // cache the transform and any quest/report components BEFORE we null 'active'
        var source = active ? active.transform : null;

        DialogueNpcReporter talkRep = null;
        QuestGiver giver = null;

        if (source)
        {
            talkRep = source.GetComponent<DialogueNpcReporter>()
                    ?? source.GetComponentInParent<DialogueNpcReporter>()
                    ?? source.GetComponentInChildren<DialogueNpcReporter>(true);

            giver = source.GetComponent<QuestGiver>()
                ?? source.GetComponentInParent<QuestGiver>()
                ?? source.GetComponentInChildren<QuestGiver>(true);
        }

        if (dialoguePanel) dialoguePanel.SetActive(false);

        active = null;
        idx = 0;
        if (player) player.canMove = true;

        // Report talk now
        if (talkRep) talkRep.ReportTalked();

        // Try to open immediately (handles cases where next-frame timing fails)
        if (giver) giver.TryOpenOffer();

        // Also try again on the next frame (covers UI/Canvas race conditions)
        if (giver) StartCoroutine(DeferredOffer(giver));
    }

    private System.Collections.IEnumerator DeferredOffer(QuestGiver giver)
    {
        yield return null; // one frame
        if (giver) giver.TryOpenOffer();
    }

    System.Collections.IEnumerator PostDialogueActionsNextFrame(Transform source)
    {
        // wait exactly one frame; lets the dialogue UI fully close first
        yield return null;

        // -- TALK reporter (search parent & children) --
        var talkRep = source.GetComponent<DialogueNpcReporter>()
                ?? source.GetComponentInParent<DialogueNpcReporter>()
                ?? source.GetComponentInChildren<DialogueNpcReporter>(true);
        if (talkRep)
            talkRep.ReportTalked();
        // else Debug.Log("[Dialogue] No DialogueNpcReporter found on/under " + source.name);

        // -- QUEST giver (search parent & children) --
        var giver = source.GetComponent<QuestGiver>()
                ?? source.GetComponentInParent<QuestGiver>()
                ?? source.GetComponentInChildren<QuestGiver>(true);

        if (giver)
            giver.TryOpenOffer();
        // else Debug.Log("[Dialogue] No QuestGiver found on/under " + source.name);
    }
}
