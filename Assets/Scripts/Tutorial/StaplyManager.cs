// StaplyManager.cs
using UnityEngine;
using System.Collections.Generic;

public class StaplyManager : MonoBehaviour {
    [Header("Refs")]
    public StaplyController staply;
    public StaplyTutorialUI ui;
    [Header("Flow")]
    public List<TutorialStep> steps;
    public bool freezeAtStart = true;
    [Header("Intro Voice")]
    public AudioClip introVoiceNewPlayer;   // "Well hey there! I'm Staply..."
    public AudioClip introVoiceReturning;   // "Back again? I'll hide in my little corner..."
    const string KeySeen = "Staply_Seen";
    int index = 0;
    bool tutorialEnabled = true;
    bool stepActive = false;

    // Track pickups that can happen before their tutorial step is reached.
    // Set to true the moment the event fires, regardless of current step.
    bool _itemPickedUp   = false;
    bool _weaponPickedUp = false;
    bool _itemEquipped   = false;

    void Awake(){
        Subscribe(true);
        if (freezeAtStart) Time.timeScale = 0f;
    }

    void Start(){
        staply.SetExclamation(false);
        staply.Appear();
        if (PlayerPrefs.GetInt(KeySeen,0)==1){
            ui.ShowOk(
                "Back again? I'll hide in my little corner - click me if you need help.",
                onOk: ()=> { FinishIntro(false); }
            );
            staply.PlayLine(introVoiceReturning);
        } else {
            ui.ShowYesNo(
                "Well hey there! I'm Staply, your personal game assistant! Folks keep sayin' I'm a hallucination, but near as I can tell, I'm the only one here makin' any sense.\nNow, have you played this game before?",
                onYes: ()=>{ PlayerPrefs.SetInt(KeySeen,1); FinishIntro(false); },
                onNo:  ()=>{ PlayerPrefs.SetInt(KeySeen,1); FinishIntro(true); }
            );
            staply.PlayLine(introVoiceNewPlayer);
        }
    }

    void FinishIntro(bool startTutorial){
        ui.Hide();
        staply.StopTalking();
        ResumeTime();

        if (startTutorial){
            ShowCurrentStep();
        } else {
            tutorialEnabled = false;
            staply.HideToCorner(true);
            staply.SetExclamation(false);
        }
    }

    void ResumeTime(){ if (Mathf.Approximately(Time.timeScale,0f)) Time.timeScale = 1f; }

    void ShowCurrentStep(){
        if(!tutorialEnabled || index >= steps.Count) { staply.SetExclamation(false); return; }

        var step = steps[index];

        // If the player already satisfied this condition before reaching the step, skip it silently.
        if (IsAlreadySatisfied(step.condition)){
            index++;
            ShowCurrentStep(); // recurse to find the next unsatisfied step
            return;
        }

        stepActive = true;

        staply.Appear();
        staply.SetExclamation(false);
        ui.ShowLine(step.line);
        staply.PlayLine(step.voice);
    }

    // Returns true if the condition was already met before this step became active.
    bool IsAlreadySatisfied(TutorialStep.Condition c){
        if (c == TutorialStep.Condition.PickedUpItem)   return _itemPickedUp;
        if (c == TutorialStep.Condition.PickedUpWeapon) return _weaponPickedUp;
        if (c == TutorialStep.Condition.EquippedItem)   return _itemEquipped;
        return false;
    }

    void CompleteCurrentStep(){
        if(!stepActive) return;
        stepActive = false;

        ui.Hide();
        staply.StopTalking();
        staply.HideToCorner(true);

        bool more = ++index < steps.Count;

        // Show the exclamation on the proxy only if there's another step waiting.
        staply.SetExclamation(more && steps[index].showExclamationWhileHidden);

        // IMPORTANT: do NOT auto ShowCurrentStep() here anymore.
        // We wait for the player to click the corner proxy.
    }

    // Optional: bind this to a click target on Staply in the corner
    public void OnStaplyClicked(){
        // Only open if there is a pending step and not already speaking.
        if (!tutorialEnabled) { staply.HideToCorner(true); return; }
        if (stepActive) { return; }

        if (index < steps.Count){
            ShowCurrentStep();                 // opens the next queued line
        } else {
            // No more steps - don't block the screen; bounce back to corner
            staply.HideToCorner(true);
        }
    }

    // ----- Condition handling -----
    void Subscribe(bool on){
        if(on){
            TutorialEvents.Moved += OnMoved;
            TutorialEvents.Aimed += OnAimed;
            TutorialEvents.Sprint += OnSprint;
            TutorialEvents.Interact += OnInteract;
            TutorialEvents.OpenEquipment += OnOpenEquipment;
            TutorialEvents.PickedUpWeapon += OnPickedUpWeapon;
            TutorialEvents.ToggleAutoAttack += OnToggleAutoAttack;
            TutorialEvents.OpenInventory += OnOpenInventory;
            TutorialEvents.OpenQuestLog += OnOpenQuestLog;
            TutorialEvents.EquippedItem += OnEquippedItem;
            TutorialEvents.PickedUpItem += OnPickedUpItem;
        } else {
            TutorialEvents.Moved -= OnMoved;
            TutorialEvents.Aimed -= OnAimed;
            TutorialEvents.Sprint -= OnSprint;
            TutorialEvents.Interact -= OnInteract;
            TutorialEvents.OpenEquipment -= OnOpenEquipment;
            TutorialEvents.PickedUpWeapon -= OnPickedUpWeapon;
            TutorialEvents.ToggleAutoAttack -= OnToggleAutoAttack;
            TutorialEvents.OpenInventory -= OnOpenInventory;
            TutorialEvents.OpenQuestLog -= OnOpenQuestLog;
            TutorialEvents.EquippedItem -= OnEquippedItem;
            TutorialEvents.PickedUpItem -= OnPickedUpItem;
        }
    }

    bool IsCurrent(TutorialStep.Condition c){
        return tutorialEnabled && index < steps.Count && steps[index].condition == c && stepActive;
    }

    void OnMoved(){ if(IsCurrent(TutorialStep.Condition.Moved)) CompleteCurrentStep(); }
    void OnAimed(){ if(IsCurrent(TutorialStep.Condition.Aimed)) CompleteCurrentStep(); }
    void OnSprint(){ if(IsCurrent(TutorialStep.Condition.Sprint)) CompleteCurrentStep(); }
    void OnInteract(){ if(IsCurrent(TutorialStep.Condition.Interact)) CompleteCurrentStep(); }
    void OnOpenEquipment(){ if(IsCurrent(TutorialStep.Condition.OpenEquipment)) CompleteCurrentStep(); }
    void OnPickedUpWeapon(){
        _weaponPickedUp = true; // remember even if this step isn't active yet
        if(IsCurrent(TutorialStep.Condition.PickedUpWeapon)) CompleteCurrentStep();
    }
    void OnToggleAutoAttack(){ if(IsCurrent(TutorialStep.Condition.ToggleAutoAttack)) CompleteCurrentStep(); }
    void OnOpenInventory(){ if(IsCurrent(TutorialStep.Condition.OpenInventory)) CompleteCurrentStep(); }
    void OnOpenQuestLog(){ if(IsCurrent(TutorialStep.Condition.OpenQuestLog)) CompleteCurrentStep(); }
    void OnEquippedItem(){
        _itemEquipped = true; // remember even if this step isn't active yet
        if(IsCurrent(TutorialStep.Condition.EquippedItem)) CompleteCurrentStep();
    }
    void OnPickedUpItem(){
        _itemPickedUp = true; // remember even if this step isn't active yet
        if (IsCurrent(TutorialStep.Condition.PickedUpItem)) CompleteCurrentStep();
    }

    void OnDestroy(){ Subscribe(false); }
}