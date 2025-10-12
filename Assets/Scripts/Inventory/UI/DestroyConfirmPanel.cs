using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DestroyConfirmPanel : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text message;
    public Button yesButton;
    public Button noButton;

    Action onYes, onNo;

    void Awake()
    {
        if (yesButton) yesButton.onClick.AddListener(ClickYes);
        if (noButton)  noButton.onClick.AddListener(ClickNo);
        gameObject.SetActive(false);
    }

    public void Show(string text, Action yes, Action no)
    {
        if (message) message.text = text;
        onYes = yes; onNo = no;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        onYes = onNo = null;
    }

    void ClickYes() { onYes?.Invoke(); Hide(); }
    void ClickNo()  { onNo?.Invoke();  Hide(); }
}
