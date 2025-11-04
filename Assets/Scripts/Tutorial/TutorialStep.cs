// TutorialStep.cs
using UnityEngine;

[CreateAssetMenu(menuName="Staply/Tutorial Step")]
public class TutorialStep : ScriptableObject {
    public enum Condition {
        None, Moved, Aimed, Sprint, Interact, OpenEquipment, PickedUpWeapon,
        ToggleAutoAttack, OpenInventory, OpenQuestLog, EquippedItem
    }
    [TextArea] public string line;
    public AudioClip voice;
    public Condition condition;
    public bool showExclamationWhileHidden = true;
}