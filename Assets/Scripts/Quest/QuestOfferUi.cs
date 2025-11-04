using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class QuestOfferUI : MonoBehaviour
{
    [Header("If left empty, will use this GameObject as the panel")]
    public GameObject panel;

    [Header("Main")]
    public TMP_Text titleText;
    public TMP_Text descText;
    public Button acceptBtn;
    public Button declineBtn;

    [Header("Rewards (optional)")]
    public GameObject rewardsRoot; // parent container for rewards (can be the row)
    public TMP_Text xpText;        // e.g. "XP: +150"
    public TMP_Text moneyText;     // e.g. "Money: +$20"

    [Header("Item Reward (optional)")]
    public GameObject itemRoot;   // a small container row/panel
    public UnityEngine.UI.Image itemIcon;
    public TMP_Text itemNameText;

    QuestDefinition current;
    QuestGiver giver;
    bool initialized = false;

    void Awake()
    {
        if (!panel) panel = gameObject;
        if (panel.activeSelf) panel.SetActive(false);
    }

    void EnsureInit()
    {
        if (initialized) return;
        if (acceptBtn) { acceptBtn.onClick.RemoveAllListeners(); acceptBtn.onClick.AddListener(Accept); }
        if (declineBtn){ declineBtn.onClick.RemoveAllListeners(); declineBtn.onClick.AddListener(Decline); }
        initialized = true;
    }

    public void ShowOffer(QuestDefinition def, QuestGiver g)
    {
        EnsureInit();
        current = def; giver = g;

        if (titleText) titleText.text = def ? def.title : "";
        if (descText)  descText.text  = def ? def.description : "";

        // --- Rewards ---
        if (def != null)
        {
            bool any = false;

            if (xpText)
            {
                if (def.xpReward > 0) {
                    xpText.gameObject.SetActive(true);
                    xpText.text = $"XP: +{def.xpReward}";
                    any = true;
                } else xpText.gameObject.SetActive(false);
            }

            if (moneyText)
            {
                if (def.moneyReward > 0) {
                    moneyText.gameObject.SetActive(true);
                    // tweak currency label to your liking
                    moneyText.text = $"Money: +${def.moneyReward}";
                    any = true;
                } else moneyText.gameObject.SetActive(false);
            }

            if (rewardsRoot) rewardsRoot.SetActive(any);
        }

        // ----- Item reward preview -----
        if (itemRoot) itemRoot.SetActive(false);
        if (def != null && def.itemReward)
        {
            if (itemIcon)    itemIcon.sprite = def.itemReward.icon;
            if (itemNameText)
            {
                // Name with amount (xN)
                string baseName = string.IsNullOrEmpty(def.itemReward.overrideName)
                    ? def.itemReward.baseName
                    : def.itemReward.overrideName;
                int amt = Mathf.Max(1, def.itemAmount);
                itemNameText.text = amt > 1 ? $"{baseName}  x{amt}" : baseName;
            }
            if (itemRoot) itemRoot.SetActive(true);
        }
        // ---------------

        if (panel) panel.SetActive(true);
        DialogueController.I?.SetExternalLock(true);

        // make it render immediately even if it was inactive
        Canvas.ForceUpdateCanvases();
    }

    public void Hide()
    {
        if (panel) panel.SetActive(false);
        DialogueController.I?.SetExternalLock(false);
        current = null; giver = null;
    }

    void Accept()
    {
        if (giver && current) giver.OnAccepted();
        Hide();
    }

    void Decline()
    {
        if (giver) giver.OnDeclined();
        Hide();
    }
}
