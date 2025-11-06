using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIRaycastProbe : MonoBehaviour
{
    public Canvas canvas;          // your UI canvas (Overlay)
    public RectTransform slotRect; // assign one of your slot items

    void Update()
    {
        if (!EventSystem.current) return;

        var data = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(data, hits);

        if (hits.Count > 0)
            Debug.Log($"Top hit: {hits[0].gameObject.name}");
        else
            Debug.Log("No UI under mouse");

        if (slotRect &&
            RectTransformUtility.ScreenPointToLocalPointInRectangle(slotRect, Input.mousePosition, null, out var local))
        {
            bool inside = slotRect.rect.Contains(local);
            Debug.Log($"Mouse inside '{slotRect.name}': {inside}, local={local}");
        }
    }
}
