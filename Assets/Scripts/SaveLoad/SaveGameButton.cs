// SaveGameButton.cs
// Attach to any Button in your escape/pause menu.
// Wire that Button's OnClick() to SaveGame(), or assign the button
// below and let Start() wire it automatically.

using UnityEngine;
using UnityEngine.UI;

public class SaveGameButton : MonoBehaviour
{
    [Tooltip("Optional — if assigned, OnClick is wired automatically.")]
    [SerializeField] private Button button;

    [Tooltip("Optional — shown briefly after saving.")]
    [SerializeField] private ScreenToast toast;

    void Start()
    {
        if (button) button.onClick.AddListener(SaveGame);
    }

    public void SaveGame()
    {
        if (GameSaveManager.I == null) return;

        int slot = GameSaveManager.I.ActiveSlot;

        // Safety net: if somehow no slot was claimed yet, use 0
        if (slot < 0) slot = 0;

        GameSaveManager.I.Save(slot);
        toast?.Show("Game saved.", Color.green);
    }
}
