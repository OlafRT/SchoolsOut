using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class StaplyTutorialButtonHook : MonoBehaviour, IPointerClickHandler, ISubmitHandler
{
    public enum Action { OpenInventory, OpenEquipment, OpenQuestLog, ToggleAutoAttack }
    public Action action;

    [Tooltip("If ON, Enter/Space (Submit) will also complete this step. OFF = clicks only.")]
    public bool countSubmit = false;

    // Optional: prevent this button from ever being 'selected' by keyboard navigation
    [Tooltip("Set Navigation=None on this Selectable at runtime to avoid accidental Submit.")]
    public bool disableNavigation = true;

    bool firedThisFrame;

    void Awake()
    {
        if (disableNavigation)
        {
            var sel = GetComponent<Selectable>();
            if (sel)
            {
                var nav = sel.navigation;
                nav.mode = Navigation.Mode.None;
                sel.navigation = nav;
            }
        }
    }

    void LateUpdate() { firedThisFrame = false; }

    // Mouse/touch – this is what we want to count as a real click.
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        Fire();
    }

    // Keyboard/gamepad submit (Space/Enter/A). Only counts if you allow it.
    public void OnSubmit(BaseEventData eventData)
    {
        if (!countSubmit || firedThisFrame) return; // avoid double fire if both happen this frame
        Fire();
    }

    void Fire()
    {
        firedThisFrame = true;
        switch (action)
        {
            case Action.OpenInventory:    TutorialEvents.RaiseOpenInventory();    break;
            case Action.OpenEquipment:    TutorialEvents.RaiseOpenEquipment();    break;
            case Action.OpenQuestLog:     TutorialEvents.RaiseOpenQuestLog();     break;
            case Action.ToggleAutoAttack: TutorialEvents.RaiseToggleAutoAttack(); break;
        }
    }
}