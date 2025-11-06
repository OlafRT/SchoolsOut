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
    const string KeySeen = "Staply_Seen";
    int index = 0;
    bool tutorialEnabled = true;
    bool stepActive = false;

    void Awake(){
        Subscribe(true);
        if (freezeAtStart) Time.timeScale = 0f;
    }

    void Start(){
        staply.SetExclamation(false);
        staply.Appear();
        if (PlayerPrefs.GetInt(KeySeen,0)==1){
        ui.ShowOk(
        "Back again? I’ll hide in my little corner — click me if you need help.",
        onOk: ()=> { FinishIntro(false); }
        );
        } else {
            ui.ShowYesNo(
                "Hello, I'm Staply! Your personal game assistant! I might be a hallucination, but who can be sure, eh?\nAnyway, have you played this game before?",
                onYes: ()=>{ PlayerPrefs.SetInt(KeySeen,1); FinishIntro(false); },
                onNo:  ()=>{ PlayerPrefs.SetInt(KeySeen,1); FinishIntro(true); }
            );
        }
    }

    void FinishIntro(bool startTutorial){
    ui.Hide();
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
    stepActive = true;

    staply.Appear();
    staply.SetExclamation(false);
    ui.ShowLine(step.line);
    staply.PlayLine(step.voice);
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
        // No more steps — don’t block the screen; bounce back to corner
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
    void OnPickedUpWeapon(){ if(IsCurrent(TutorialStep.Condition.PickedUpWeapon)) CompleteCurrentStep(); }
    void OnToggleAutoAttack(){ if(IsCurrent(TutorialStep.Condition.ToggleAutoAttack)) CompleteCurrentStep(); }
    void OnOpenInventory(){ if(IsCurrent(TutorialStep.Condition.OpenInventory)) CompleteCurrentStep(); }
    void OnOpenQuestLog(){ if(IsCurrent(TutorialStep.Condition.OpenQuestLog)) CompleteCurrentStep(); }
    void OnEquippedItem(){ if(IsCurrent(TutorialStep.Condition.EquippedItem)) CompleteCurrentStep(); }
    void OnPickedUpItem(){ if (IsCurrent(TutorialStep.Condition.PickedUpItem)) CompleteCurrentStep(); }

    void OnDestroy(){ Subscribe(false); }
}
