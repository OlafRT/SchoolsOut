using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIRaycastDebugger : MonoBehaviour
{
    public Canvas canvas;          // assign your root UI canvas (Screen Space - Overlay)

    void Update()
    {
        // Always log once per frame so we know it's running
        string framePrefix = $"[UIRaycastDebugger t={Time.frameCount}] ";

        if (EventSystem.current == null)
        {
            Debug.LogWarning(framePrefix + "No EventSystem in scene.");
            return;
        }

        // Raycast everything under the pointer
        var data = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(data, results);

        if (results.Count == 0)
        {
            Debug.Log(framePrefix + "No UI under mouse");
        }
        else
        {
            // Print the full stack (top first)
            var msg = framePrefix + "Hits:";
            for (int i = 0; i < results.Count; i++)
            {
                var r = results[i];
                msg += $"\n  [{i}] {r.gameObject.name} (sortingLayer:{r.sortingLayer} order:{r.sortingOrder} dist:{r.distance:F2})";
            }
            Debug.Log(msg);
        }
    }
}
