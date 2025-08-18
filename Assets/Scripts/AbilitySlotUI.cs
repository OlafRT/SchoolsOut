using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public class AbilitySlotUI : MonoBehaviour
{
    [Header("Wiring")]
    public MonoBehaviour abilityComponent;   // must implement IAbilityUI
    public Image iconImage;                  // colored icon underneath
    public Image overlayImage;               // gray/black cover (Filled Vertical)
    public TextMeshProUGUI keybindLabel;     // optional
    [Tooltip("Optional: shows remaining charges (e.g., '2').")]
    public TextMeshProUGUI stacksLabel;      // optional

    [Header("Colors")]
    public Color readyColor = Color.white;
    public Color blockedColor = new Color(0.6f, 0.6f, 0.6f, 1f);

    private IAbilityUI ability;              // cached interface
    private IChargeableAbility chargeable;   // optional
    private MonoBehaviour boundComponent;    // track rebinding

    void Awake()
    {
        PrepareOverlay();
        RebindIfNeeded();
        PrimeVisuals();
    }

    void OnEnable()
    {
        PrepareOverlay();
        RebindIfNeeded();
        PrimeVisuals();
    }

    void Update()
    {
        // If the auto-binder assigns after Start, rebind here.
        RebindIfNeeded();
        if (ability == null) return;

        // Charges (optional)
        int curCharges = -1, maxCharges = -1;
        if (chargeable != null)
        {
            curCharges = chargeable.CurrentCharges;
            maxCharges = chargeable.MaxCharges;
            if (stacksLabel)
            {
                stacksLabel.text = curCharges > 0 ? curCharges.ToString() : "0";
                stacksLabel.enabled = maxCharges > 1;
            }
        }
        else if (stacksLabel)
        {
            stacksLabel.enabled = false;
        }

        // Cooldown → overlay.fillAmount
        // Rule: if we have at least 1 charge, overlay is 0 (looks ready).
        float remain = ability.CooldownRemaining;
        float dur = ability.CooldownDuration;

        if (overlayImage)
        {
            bool hasCharge = (chargeable != null && curCharges > 0);
            if (hasCharge)
            {
                overlayImage.fillAmount = 0f;
            }
            else if (dur <= 0f)
            {
                overlayImage.fillAmount = ability.IsLearned ? 0f : 1f;
            }
            else
            {
                float fill = Mathf.Clamp01(remain / Mathf.Max(0.0001f, dur));
                overlayImage.fillAmount = fill;
            }
        }

        // Icon tint (ready vs blocked or not learned)
        if (iconImage)
        {
            bool ready = ability.IsLearned &&
                         ((chargeable != null && curCharges > 0) || remain <= 0.0001f);
            iconImage.color = ready ? readyColor : blockedColor;

            // keep sprite refreshed in case icon changes at runtime
            var sp = ability.Icon;
            if (sp && iconImage.sprite != sp) iconImage.sprite = sp;
        }

        // Key label (keeps in sync if you rebind at runtime)
        if (keybindLabel)
        {
            string want = ability.Key.ToString();
            if (keybindLabel.text != want) keybindLabel.text = want;
        }
    }

    // ---------- helpers ----------

    void PrepareOverlay()
    {
        if (!overlayImage) return;

        overlayImage.type = Image.Type.Filled;
        overlayImage.fillMethod = Image.FillMethod.Vertical;
        overlayImage.fillOrigin = (int)Image.OriginVertical.Top;

        // If no sprite assigned, use Unity's built-in UI sprite so Fill works.
        if (overlayImage.sprite == null)
        {
            var uiSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
            overlayImage.sprite = uiSprite;
            overlayImage.preserveAspect = false;
        }
    }

    void PrimeVisuals()
    {
        if (ability == null) return;

        if (iconImage && ability.Icon) iconImage.sprite = ability.Icon;
        if (keybindLabel) keybindLabel.text = ability.Key.ToString();
    }

    void RebindIfNeeded()
    {
        if (abilityComponent == boundComponent) return;

        boundComponent = abilityComponent;
        ability = boundComponent as IAbilityUI;
        chargeable = boundComponent as IChargeableAbility;

        if (ability == null && boundComponent != null)
            Debug.LogError($"{name}: Assigned abilityComponent does not implement IAbilityUI.", this);

        // Immediately reflect new binding
        PrimeVisuals();
    }
}