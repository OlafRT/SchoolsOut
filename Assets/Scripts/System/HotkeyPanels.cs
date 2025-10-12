using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class HotkeyPanels : MonoBehaviour
{
    [System.Serializable]
    public class PanelBinding
    {
        [Header("Main")]
        public GameObject panel;                 // the UI window root to toggle
        public List<GameObject> alsoToggle;      // extra objects to mirror state (e.g., EquipmentCam)
        public bool bringToFront = true;

        [Header("Audio (optional)")]
        public AudioSource onOpen;               // e.g., BagSound, ButtonClickAudio
        public AudioSource onClose;              // sound when closing (can be same as onOpen)

        public bool IsOpen => panel && panel.activeSelf;

        public void Open()
        {
            if (!panel) return;
            panel.SetActive(true);
            if (bringToFront) panel.transform.SetAsLastSibling();
            if (alsoToggle != null) foreach (var go in alsoToggle) if (go) go.SetActive(true);
            if (onOpen) onOpen.Play();
        }

        public void Close()
        {
            if (!panel) return;
            panel.SetActive(false);
            if (alsoToggle != null) foreach (var go in alsoToggle) if (go) go.SetActive(false);
            if (onClose) onClose.Play();
        }

        public void Toggle()
        {
            if (!panel) return;
            if (panel.activeSelf) Close();
            else Open();
        }
    }

    [Header("Bindings")]
    public PanelBinding bag;
    public PanelBinding equipment;
    public PanelBinding questLog;

    [Header("Keys")]
    public KeyCode bagKey = KeyCode.B;
    public KeyCode equipmentKey = KeyCode.I;
    public KeyCode questKey = KeyCode.L;
    public bool escapeClosesTop = true;

    // remembers open order so Esc closes the most recently opened first
    private readonly List<PanelBinding> openStack = new();

    void Update()
    {
        if (IsTypingIntoInputField()) return;

        if (Input.GetKeyDown(bagKey))       ToggleAndStack(bag);
        if (Input.GetKeyDown(equipmentKey)) ToggleAndStack(equipment);
        if (Input.GetKeyDown(questKey))     ToggleAndStack(questLog);

        if (escapeClosesTop && Input.GetKeyDown(KeyCode.Escape))
            CloseTop();
    }

    void ToggleAndStack(PanelBinding b)
    {
        if (b == null || b.panel == null) return;

        bool willOpen = !b.IsOpen;
        b.Toggle();

        // maintain open order
        openStack.RemoveAll(x => x == null || x.panel == null); // clean
        openStack.Remove(b);
        if (willOpen) openStack.Add(b);
    }

    void CloseTop()
    {
        openStack.RemoveAll(x => x == null || x.panel == null);
        if (openStack.Count == 0) return;
        var top = openStack[openStack.Count - 1];
        top.Close();
        openStack.RemoveAt(openStack.Count - 1);
    }

    bool IsTypingIntoInputField()
    {
        var es = EventSystem.current;
        if (!es) return false;
        var go = es.currentSelectedGameObject;
        return go && go.GetComponent<TMP_InputField>() != null;
    }
}