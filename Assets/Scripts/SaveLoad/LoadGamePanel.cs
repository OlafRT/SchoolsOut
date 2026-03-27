// LoadGamePanel.cs
using System;
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
    }

    [Header("Slot Entries  (one per save slot)")]
    public SlotEntry[] slots = new SlotEntry[GameSaveManager.SlotCount];

    // ── Slot colors ───────────────────────────────────────────────────────────
    // Jock  → blue  #2357CA
    private static readonly Color32 JockColor  = new Color32(0x23, 0x57, 0xCA, 0xFF);
    // Nerd  → green #24CB2F
    private static readonly Color32 NerdColor  = new Color32(0x24, 0xCB, 0x2F, 0xFF);
    // Empty → dark grey
    private static readonly Color32 EmptyColor = new Color32(0x3A, 0x3A, 0x3A, 0xFF);

    // ── Unity ─────────────────────────────────────────────────────────────────

    // Refresh every time the panel becomes visible
    void OnEnable()
    {
        RefreshSlots();
    }

    // ── Slot click ─────────────────────────────────────────────────────────────
    // Named methods so they show up in the Inspector OnClick dropdown.
    // Wire each slot button's OnClick() to the matching method below.

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

    // ── Refresh ───────────────────────────────────────────────────────────────

    void RefreshSlots()
    {
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
            }
            else
            {
                if (slots[i].infoText)
                    slots[i].infoText.text = "Empty";

                if (slots[i].background)
                    slots[i].background.color = EmptyColor;

                if (slots[i].button)
                    slots[i].button.interactable = false;
            }
        }
    }
}