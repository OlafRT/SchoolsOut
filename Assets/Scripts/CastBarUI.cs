using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public class CastBarUI : MonoBehaviour
{
    [Header("Wiring")]
    [Tooltip("Parent GameObject of the whole bar (enable/disable). If empty, this GameObject is used.")]
    public GameObject root;
    public Image fill;                 // bar set to Filled / Horizontal
    public TMP_Text timeText;          // the “1.5s” label
    public TMP_Text titleText;         // optional ability name

    void Awake()
    {
        if (!root) root = gameObject;
        Hide();
    }

    public void Show(string title, float duration)
    {
        if (!root) root = gameObject;
        root.SetActive(true);
        if (titleText) titleText.text = title;
        SetProgress(0f, duration);
    }

    /// <param name="progress01">0..1 where 1 == finished</param>
    public void SetProgress(float progress01, float remainingSeconds)
    {
        if (fill) fill.fillAmount = Mathf.Clamp01(progress01);
        if (timeText) timeText.text = remainingSeconds > 0f
            ? $"{remainingSeconds:0.0}s"
            : "";
    }

    public void Hide()
    {
        if (!root) root = gameObject;
        root.SetActive(false);
    }
}
