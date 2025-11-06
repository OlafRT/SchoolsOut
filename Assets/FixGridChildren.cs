using UnityEngine;
using UnityEngine.UI;

public class FixGridChildren : MonoBehaviour
{
    void Awake()
    {
        var grid = GetComponent<GridLayoutGroup>();
        if (!grid) return;

        foreach (RectTransform child in transform)
        {
            child.anchorMin = new Vector2(0f, 1f);   // top-left
            child.anchorMax = new Vector2(0f, 1f);   // top-left
            child.pivot     = new Vector2(0.5f, 0.5f);
            child.localScale = Vector3.one;
            // Let GridLayoutGroup size/position; ensure no leftover size
            child.sizeDelta = Vector2.zero;
            child.anchoredPosition3D = new Vector3(child.anchoredPosition.x, child.anchoredPosition.y, 0f);
        }
    }
}
