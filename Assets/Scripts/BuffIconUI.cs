using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BuffIconUI : MonoBehaviour
{
    public Image iconImage;
    public Image overlayImage;        // type: Filled, Vertical
    public TextMeshProUGUI timeText;  // optional

    public void Set(Sprite icon) { if (iconImage) iconImage.sprite = icon; }

    public void SetTime(float remaining, float duration)
    {
        if (overlayImage)
        {
            float fill = (duration <= 0.01f) ? 0f : Mathf.Clamp01(remaining / duration);
            overlayImage.fillAmount = fill;
        }

        if (timeText)
        {
            timeText.text = remaining >= 10f ? Mathf.CeilToInt(remaining).ToString() : remaining.ToString("0.0");
        }
    }
}