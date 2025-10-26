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

    public bool IsDialogueOpen => active != null;

    public void ShowPrompt(bool show)
    {
        if (IsDialogueOpen) show = false;
        if (promptSpacebar && promptSpacebar.activeSelf != show)
            promptSpacebar.SetActive(show);
    }

    public void Begin(DialogueInteractable target)
    {
        if (!target || target.lines == null || target.lines.Length == 0) return;
        active = target;
        idx = 0;

        // freeze player movement while talking
        if (player) player.canMove = false;

        // optional: snap NPC to face player
        if (target.facePlayerOnStart)
        {
            Vector3 toPlayer = player.transform.position - target.transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.0001f)
                target.transform.rotation = Quaternion.LookRotation(toPlayer.normalized, Vector3.up);
        }

        if (promptSpacebar) promptSpacebar.SetActive(false);
        if (dialoguePanel) dialoguePanel.SetActive(true);

        dialogueText.text = active.lines[idx];
    }

    public void Advance()
    {
        if (active == null) return;
        idx++;
        if (idx >= active.lines.Length)
        {
            End();
            return;
        }
        dialogueText.text = active.lines[idx];
    }

    public void End()
    {
        if (dialoguePanel) dialoguePanel.SetActive(false);
        active = null;
        idx = 0;

        if (player) player.canMove = true;
    }
}
