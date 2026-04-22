// LoadGamePanel.cs
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LoadGamePanel : MonoBehaviour
{
    [Serializable]
    public struct SlotEntry
    {
        [Tooltip("The Image component on the slot — used to tint it by class.")]
        public Image    background;
        [Tooltip("The Button component on the slot.")]
        public Button   button;
        [Tooltip("The InfoText TMP_Text child of the slot.")]
        public TMP_Text infoText;

        [Header("Delete")]
        [Tooltip("The delete Button on this slot. Only interactable when the slot has a save.")]
        public Button   deleteButton;
        [Tooltip("TMP_Text label on the delete button — swapped to 'Sure?' during confirmation.")]
        public TMP_Text deleteButtonText;
    }

    [Header("Slot Entries  (one per save slot)")]
    public SlotEntry[] slots = new SlotEntry[GameSaveManager.SlotCount];

    // ── Slot colors ───────────────────────────────────────────────────────────
    private static readonly Color32 JockColor  = new Color32(0x23, 0x57, 0xCA, 0xFF);
    private static readonly Color32 NerdColor  = new Color32(0x24, 0xCB, 0x2F, 0xFF);
    private static readonly Color32 EmptyColor = new Color32(0x3A, 0x3A, 0x3A, 0xFF);

    // ── Confirmation state ────────────────────────────────────────────────────
    // Tracks which slot (if any) is currently pending confirmation, and the
    // running coroutine that will reset it after the timeout.
    private int       _pendingDeleteSlot = -1;
    private Coroutine _confirmResetCo;

    [Tooltip("Seconds before an unconfirmed delete press resets back to 'Delete'.")]
    public float confirmTimeout = 3f;

    // ── Unity ─────────────────────────────────────────────────────────────────

    void OnEnable()
    {
        RefreshSlots();
    }

    // ── Load ──────────────────────────────────────────────────────────────────

    public void LoadSlot0() => LoadSlot(0);
    public void LoadSlot1() => LoadSlot(1);
    public void LoadSlot2() => LoadSlot(2);
    public void LoadSlot3() => LoadSlot(3);

    void LoadSlot(int slot)
    {
        Debug.Log($"[LoadPanel] Slot {slot} clicked. Manager={(GameSaveManager.I != null ? "OK" : "NULL")} HasSave={GameSaveManager.I?.HasSave(slot)}");
        if (GameSaveManager.I == null) { Debug.LogError("[LoadPanel] GameSaveManager.I is null — it may have been destroyed."); return; }
        if (!GameSaveManager.I.HasSave(slot)) { Debug.LogWarning($"[LoadPanel] Slot {slot} is empty."); return; }
        Debug.Log($"[LoadPanel] Calling Load({slot})...");
        GameSaveManager.I.Load(slot);
    }

    // ── Delete ────────────────────────────────────────────────────────────────
    // Wire each slot's delete button OnClick() to the matching method below.

    public void DeleteSlot0() => HandleDeletePress(0);
    public void DeleteSlot1() => HandleDeletePress(1);
    public void DeleteSlot2() => HandleDeletePress(2);
    public void DeleteSlot3() => HandleDeletePress(3);

    void HandleDeletePress(int slot)
    {
        if (GameSaveManager.I == null) return;
        if (!GameSaveManager.I.HasSave(slot)) return;

        if (_pendingDeleteSlot == slot)
        {
            // Second press — confirmed, do the delete.
            ConfirmDelete(slot);
        }
        else
        {
            // First press — cancel any existing pending confirmation on another slot,
            // then enter confirmation mode for this slot.
            if (_pendingDeleteSlot >= 0)
                ResetDeleteButton(_pendingDeleteSlot);

            _pendingDeleteSlot = slot;
            SetDeleteLabel(slot, "Sure?");

            if (_confirmResetCo != null) StopCoroutine(_confirmResetCo);
            _confirmResetCo = StartCoroutine(AutoResetConfirm(slot));
        }
    }

    void ConfirmDelete(int slot)
    {
        if (_confirmResetCo != null) { StopCoroutine(_confirmResetCo); _confirmResetCo = null; }
        _pendingDeleteSlot = -1;

        GameSaveManager.I.DeleteSlot(slot);
        Debug.Log($"[LoadPanel] Slot {slot} deleted.");

        // Refresh the entire panel so the slot instantly shows as empty.
        RefreshSlots();
    }

    IEnumerator AutoResetConfirm(int slot)
    {
        yield return new WaitForSecondsRealtime(confirmTimeout);

        // Only reset if this slot is still the pending one (not already confirmed).
        if (_pendingDeleteSlot == slot)
        {
            _pendingDeleteSlot = -1;
            ResetDeleteButton(slot);
        }
        _confirmResetCo = null;
    }

    void ResetDeleteButton(int slot)
    {
        if (slot < 0 || slot >= slots.Length) return;
        SetDeleteLabel(slot, "Delete");
    }

    void SetDeleteLabel(int slot, string label)
    {
        if (slot < 0 || slot >= slots.Length) return;
        if (slots[slot].deleteButtonText)
            slots[slot].deleteButtonText.text = label;
    }

    // ── Refresh ───────────────────────────────────────────────────────────────

    void RefreshSlots()
    {
        // Cancel any pending confirmation when the panel refreshes (e.g. after a delete).
        if (_confirmResetCo != null) { StopCoroutine(_confirmResetCo); _confirmResetCo = null; }
        _pendingDeleteSlot = -1;

        if (GameSaveManager.I == null) return;

        for (int i = 0; i < slots.Length && i < GameSaveManager.SlotCount; i++)
        {
            bool        hasSave = GameSaveManager.I.HasSave(i);
            GameSaveData meta   = hasSave ? GameSaveManager.I.PeekSlot(i) : null;

            if (hasSave && meta?.playerStats != null && meta.playerStats.level > 0)
            {
                string cls   = meta.playerStats.playerClass ?? "";
                int    level = meta.playerStats.level;

                if (slots[i].infoText)
                    slots[i].infoText.text = $"{cls} Lvl {level}";

                bool isNerd = string.Equals(cls, "Nerd", StringComparison.OrdinalIgnoreCase);
                bool isJock = string.Equals(cls, "Jock", StringComparison.OrdinalIgnoreCase);
                Color bg = isNerd ? (Color)NerdColor : isJock ? (Color)JockColor : (Color)EmptyColor;
                if (slots[i].background)
                    slots[i].background.color = bg;

                if (slots[i].button)
                    slots[i].button.interactable = true;

                // Enable delete and reset its label.
                if (slots[i].deleteButton)
                    slots[i].deleteButton.interactable = true;
                SetDeleteLabel(i, "Delete");
            }
            else
            {
                if (slots[i].infoText)
                    slots[i].infoText.text = "Empty";

                if (slots[i].background)
                    slots[i].background.color = EmptyColor;

                if (slots[i].button)
                    slots[i].button.interactable = false;

                // Disable and reset delete button on empty slots.
                if (slots[i].deleteButton)
                    slots[i].deleteButton.interactable = false;
                SetDeleteLabel(i, "Delete");
            }
        }
    }
}
