// DialogueNpcReporter.cs
using UnityEngine;

[RequireComponent(typeof(QuestGiver))]
public class DialogueNpcReporter : MonoBehaviour
{
    public string npcId = "nerd";
    // Call this from your DialogueController when the conversation with this NPC ends.
    public void ReportTalked(){ QuestEvents.NpcTalked?.Invoke(npcId); }
}
