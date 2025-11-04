using UnityEngine;

public class StaplyTutorialButtonHook : MonoBehaviour {
    public enum Action { OpenInventory, OpenEquipment, OpenQuestLog, ToggleAutoAttack }

    public Action action;

    // Call this from the Button’s OnClick()
    public void Fire(){
        switch(action){
            case Action.OpenInventory:   TutorialEvents.RaiseOpenInventory();   break;
            case Action.OpenEquipment:   TutorialEvents.RaiseOpenEquipment();   break;
            case Action.OpenQuestLog:    TutorialEvents.RaiseOpenQuestLog();    break;
            case Action.ToggleAutoAttack:TutorialEvents.RaiseToggleAutoAttack();break;
        }
    }
}