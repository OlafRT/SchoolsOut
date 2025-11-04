// StaplyTutorialUI.cs
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class StaplyTutorialUI : MonoBehaviour {
    public CanvasGroup bubble;
    public TMP_Text text;
    public GameObject yesNoGroup;
    public Button yesBtn, noBtn;
    public Button okBtn;            // assign in Inspector
    public GameObject okGroup;      // a container for the single OK button

    public void ShowYesNo(string msg, System.Action onYes, System.Action onNo){
        if (okGroup) okGroup.SetActive(false);
        text.text = msg;
        yesNoGroup.SetActive(true);
        yesBtn.onClick.RemoveAllListeners(); noBtn.onClick.RemoveAllListeners();
        yesBtn.onClick.AddListener(()=>onYes?.Invoke());
        noBtn.onClick.AddListener(()=>onNo?.Invoke());
        SetVisible(true);
    }

    public void ShowLine(string msg){
        if (okGroup) okGroup.SetActive(false);
        text.text = msg;
        yesNoGroup.SetActive(false);
        SetVisible(true);
    }

    public void ShowOk(string msg, System.Action onOk){
    text.text = msg;
    yesNoGroup.SetActive(false);
    if (okGroup) okGroup.SetActive(true);
    if (okBtn){
        okBtn.onClick.RemoveAllListeners();
        okBtn.onClick.AddListener(()=> onOk?.Invoke());
    }
    SetVisible(true);
    }

    public void Hide() => SetVisible(false);

    void SetVisible(bool v){
        bubble.alpha = v?1:0;
        bubble.blocksRaycasts = v;
        bubble.interactable = v;
    }
}