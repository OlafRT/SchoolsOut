using UnityEngine;
using System;

public class DialogueInteractable : MonoBehaviour
{
    [Serializable]
    public class DialogueLine
    {
        [TextArea(2,5)] public string text;

        [Header("Optional One-shot Audio")]
        public AudioClip audio;
        [Range(0f,1f)] public float volume = 1f;
        public Transform audioOriginOverride;

        [Header("Optional Actions on Entering This Line")]
        public GameObject[] enableOnEnter;
        public GameObject[] disableOnEnter;

        [Header("Optional Portrait For This Line")]
        public Sprite portrait;   // <- NEW
    }

    [Header("Portraits")]
    [Tooltip("Used when a rich line has no portrait, and for legacy string lines.")]
    public Sprite defaultPortrait;

    [Header("NEW: Rich lines (preferred)")]
    public DialogueLine[] richLines;

    [Header("LEGACY: Plain text lines (still works)")]
    [TextArea(2,5)]
    public string[] lines;

    [Header("Optional alternate dialogues (legacy strings)")]
    [TextArea(2,5)] public string[] linesWhenActive;    // e.g. "So, did you kill them yet?"
    [TextArea(2,5)] public string[] linesWhenCompleted; // e.g. "Thank you for helping me!"
    [TextArea(2,5)] public string[] linesWhenReadyToTurn;  // e.g. "You did it! You killed the rats!"
    [TextArea(2,5)] public string[] linesAfterCompletion;  // same idea as linesWhenCompleted (use either)

    [Header("LEGACY Portraits (match the text arrays 1:1)")]
    public Sprite[] legacyPortraits;                 // for: lines
    public Sprite[] portraitsWhenActive;             // for: linesWhenActive
    public Sprite[] portraitsWhenCompleted;          // for: linesWhenCompleted
    public Sprite[] portraitsWhenReadyToTurn;        // for: linesWhenReadyToTurn
    public Sprite[] portraitsAfterCompletion;        // for: linesAfterCompletion

    [Tooltip("If true, NPC will rotate to face the player when dialogue starts.")]
    public bool facePlayerOnStart = true;

    // Optional: place this exactly on the NPC's tile center if your model origin isn't centered.
    public Transform tileCenterOverride;

    public Vector3 GetTileCenter()
        => tileCenterOverride ? tileCenterOverride.position : transform.position;

    public Sprite GetPortrait(int i)
    {
        // If you’re using rich lines, those take priority
        if (richLines != null && richLines.Length > 0)
        {
            if (i >= 0 && i < richLines.Length)
                return richLines[i].portrait ? richLines[i].portrait : defaultPortrait;
            return defaultPortrait;
        }

        // LEGACY: figure out which legacy array is currently active
        var current = ResolveCurrentLines(); // your existing method that picks the right string[]
        var portraits = ResolveCurrentPortraitsFor(current);

        // Safe lookups with graceful fallback
        if (portraits != null && i >= 0 && i < portraits.Length && portraits[i] != null)
            return portraits[i];

        return defaultPortrait; // hide if null on the UI side
    }

    // Helpers for controller
    public int GetLineCount()
    {
        var arr = ResolveCurrentLines();
        return arr?.Length ?? 0;
    }
    public string GetText(int i)
    {
        var arr = ResolveCurrentLines();
        return (arr != null && i >= 0 && i < arr.Length) ? arr[i] : "";
    }

    string[] ResolveCurrentLines()
    {
        // Rich lines take over (unchanged)
        if (richLines != null && richLines.Length > 0) return null;

        var giver = GetComponent<QuestGiver>();
        if (!giver || !giver.quest) return lines;

        var id = giver.quest.questId;

        // After turn-in (completed list)
        if (QuestManager.I.IsCompleted(id))
            return (linesAfterCompletion != null && linesAfterCompletion.Length>0) ? linesAfterCompletion :
                (linesWhenCompleted != null && linesWhenCompleted.Length>0) ? linesWhenCompleted : lines;

        // While active
        if (QuestManager.I.HasActive(id))
        {
            // If the active quest instance is complete -> use ready-to-turn lines
            var qi = QuestManager.I.active.Find(q=>q.def.questId==id);
            if (qi != null && qi.IsComplete)
                return (linesWhenReadyToTurn != null && linesWhenReadyToTurn.Length>0) ? linesWhenReadyToTurn :
                    (linesWhenActive != null && linesWhenActive.Length>0) ? linesWhenActive : lines;

            // Not yet complete -> active lines
            return (linesWhenActive != null && linesWhenActive.Length>0) ? linesWhenActive : lines;
        }

        // Not accepted yet
        return lines;
    }

    public void ApplyPerLineEffects(int i)
    {
        if (richLines == null || richLines.Length == 0) return;
        var L = richLines[i];

        // Toggle objects
        if (L.enableOnEnter != null)
            foreach (var go in L.enableOnEnter) if (go) go.SetActive(true);
        if (L.disableOnEnter != null)
            foreach (var go in L.disableOnEnter) if (go) go.SetActive(false);

        // Play one-shot audio
        if (L.audio)
        {
            Vector3 pos = (L.audioOriginOverride ? L.audioOriginOverride.position : transform.position);
            AudioSource.PlayClipAtPoint(L.audio, pos, L.volume);
        }
    }

    /// <summary>
    /// Return the portrait array that matches whichever legacy text array is currently in use.
    /// Uses reference equality so it always mirrors ResolveCurrentLines() without duplicating logic.
    /// </summary>
    private Sprite[] ResolveCurrentPortraitsFor(string[] current)
    {
        if (current == lines)                   return legacyPortraits;
        if (current == linesWhenActive)         return portraitsWhenActive;
        if (current == linesWhenCompleted)      return portraitsWhenCompleted;
        if (current == linesWhenReadyToTurn)    return portraitsWhenReadyToTurn;
        if (current == linesAfterCompletion)    return portraitsAfterCompletion;
        return null;
    }
}
