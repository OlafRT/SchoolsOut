// StaplyConditionWatcher.cs
using UnityEngine;

public class StaplyConditionWatcher : MonoBehaviour {
    Vector3 lastMouse;
    void Update(){
        // Movement: any WASD / arrows pressed this frame
        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.A) ||
            Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.D) ||
            Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.LeftArrow) ||
            Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.RightArrow))
            TutorialEvents.RaiseMoved();

        // Aim (RMB held + noticeable mouse delta)
        if (Input.GetKey(KeyCode.Mouse1)){
            Vector3 delta = (Vector3)Input.mousePosition - lastMouse;
            if (delta.sqrMagnitude > 16f) TutorialEvents.RaiseAimed(); // ~4px move
        }
        lastMouse = Input.mousePosition;

        // Sprint
        if (Input.GetKeyDown(KeyCode.LeftShift)) TutorialEvents.RaiseSprint();

        // Interact
        if (Input.GetKeyDown(KeyCode.Space)) TutorialEvents.RaiseInteract();

        // Toggles / panels
        if (Input.GetKeyDown(KeyCode.I)) TutorialEvents.RaiseOpenEquipment();
        if (Input.GetKeyDown(KeyCode.B)) TutorialEvents.RaiseOpenInventory();
        if (Input.GetKeyDown(KeyCode.L)) TutorialEvents.RaiseOpenQuestLog();
        if (Input.GetKeyDown(KeyCode.R)) TutorialEvents.RaiseToggleAutoAttack();

        // For PickedUpWeapon / EquippedItem: call from your pickup/equip logic OR add heuristics here if you prefer.
    }
}
