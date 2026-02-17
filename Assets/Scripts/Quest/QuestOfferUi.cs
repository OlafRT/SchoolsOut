using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class QuestOfferUI : MonoBehaviour
{
    enum Mode { Offer, TurnIn }

    [Header("If left empty, will use this GameObject as the panel")]
    public GameObject panel;

    [Header("Main")]
    public TMP_Text titleText;
    public TMP_Text descText;
    public Button acceptBtn;
    public Button declineBtn;

    [Header("Rewards (optional)")]
    public GameObject rewardsRoot;
    public TMP_Text xpText;
    public TMP_Text moneyText;

    [Header("Item Reward (optional)")]
    public GameObject itemRoot;
    public UnityEngine.UI.Image itemIcon;
    public TMP_Text itemNameText;

    QuestDefinition current;
    QuestGiver giver;
    bool initialized = false;
    Mode mode = Mode.Offer;

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
        mode = Mode.Offer;
        current = def; giver = g;

        if (titleText) titleText.text = def ? def.title : "";
        if (descText)  descText.text  = def ? def.description : "";

        if (declineBtn) declineBtn.gameObject.SetActive(true);
        SetAcceptButtonLabel("Accept");

        RefreshRewards(def);

        if (panel) panel.SetActive(true);
        DialogueController.I?.SetExternalLock(true);
        Canvas.ForceUpdateCanvases();
    }

    public void ShowTurnIn(QuestDefinition def, QuestGiver g)
    {
        EnsureInit();
        mode = Mode.TurnIn;
        current = def; giver = g;

        if (titleText) titleText.text = def ? def.title : "";
        if (descText)  descText.text  = "Turn in this quest?";

        if (declineBtn) declineBtn.gameObject.SetActive(false);
        SetAcceptButtonLabel("Turn In");

        RefreshRewards(def);

        if (panel) panel.SetActive(true);
        DialogueController.I?.SetExternalLock(true);
        Canvas.ForceUpdateCanvases();
    }

    void SetAcceptButtonLabel(string label)
    {
        if (!acceptBtn) return;
        var t = acceptBtn.GetComponentInChildren<TMP_Text>();
        if (t) t.text = label;
    }

    void RefreshRewards(QuestDefinition def)
    {
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
            if (itemIcon) itemIcon.sprite = def.itemReward.icon;
            if (itemNameText)
            {
                string baseName = string.IsNullOrEmpty(def.itemReward.overrideName)
                    ? def.itemReward.baseName
                    : def.itemReward.overrideName;
                int amt = Mathf.Max(1, def.itemAmount);
                itemNameText.text = amt > 1 ? $"{baseName}  x{amt}" : baseName;
            }
            if (itemRoot) itemRoot.SetActive(true);
        }
    }

    public void Hide()
    {
        if (panel) panel.SetActive(false);
        DialogueController.I?.SetExternalLock(false);
        current = null; giver = null;
        mode = Mode.Offer;
    }

    void Accept()
    {
        if (giver && current)
        {
            if (mode == Mode.Offer)
                giver.OnAccepted();
            else
                giver.OnTurnInConfirmed();
        }
        Hide();
    }

    void Decline()
    {
        if (giver) giver.OnDeclined();
        Hide();
    }
}
