using UnityEngine;

public class JockAnimRelay : MonoBehaviour
{
    [Header("Animator")]
    [SerializeField] private Animator animator;

    [Header("Param Names")]
    [SerializeField] private string chargingBool = "Charging";
    [SerializeField] private string chargeSpeedParam = "ChargeSpeed";

    // cached parameter presence + hashes
    bool hasChargingBool, hasChargeSpeed;
    int  chargingBoolHash, chargeSpeedHash;
    bool lastChargingValue = false;

    void Awake()
    {
        if (!animator) animator = GetComponent<Animator>();
        CacheParams();
    }

    void CacheParams()
    {
        hasChargingBool = false;
        hasChargeSpeed  = false;

        if (!animator || animator.runtimeAnimatorController == null) return;

        foreach (var p in animator.parameters)
        {
            if (!string.IsNullOrEmpty(chargingBool) && p.name == chargingBool && p.type == AnimatorControllerParameterType.Bool)
            {
                hasChargingBool = true;
                chargingBoolHash = Animator.StringToHash(chargingBool);
            }

            if (!string.IsNullOrEmpty(chargeSpeedParam) && p.name == chargeSpeedParam && p.type == AnimatorControllerParameterType.Float)
            {
                hasChargeSpeed = true;
                chargeSpeedHash = Animator.StringToHash(chargeSpeedParam);
            }
        }
    }

    // ===== Charge control (called by abilities) =====
    public void SetCharging(bool on)
    {
        if (!animator || !hasChargingBool) return;
        if (on == lastChargingValue) return; // avoid hammering the animator each frame
        lastChargingValue = on;
        animator.SetBool(chargingBoolHash, on);

        if (!on && hasChargeSpeed) animator.SetFloat(chargeSpeedHash, 0f);
    }

    public void SetChargeSpeed(float normalizedSpeed)
    {
        if (!animator || !hasChargeSpeed) return;
        animator.SetFloat(chargeSpeedHash, normalizedSpeed);
    }

    // ===== Existing events you already had (kept) =====
    public void AnimEvent_FireStrike()         { FindObjectOfType<StrikeAbility>(true)?.AnimEvent_FireStrike(); }
    public void AnimEvent_StrikeWhoosh()       { FindObjectOfType<StrikeAbility>(true)?.AnimEvent_StrikeWhoosh(); }

    public void AnimEvent_FireJockAutoAttack() { FindObjectOfType<AutoAttackAbility>(true)?.AnimEvent_FireJockAutoAttack(); }
    public void AnimEvent_JockSwingWhoosh()    { FindObjectOfType<AutoAttackAbility>(true)?.AnimEvent_JockSwingWhoosh(); }

    public void AnimEvent_JockSmashImpact()    { HackingStation.NotifyJockSmashImpact(); }
    public void AnimEvent_JockEnableShockFX()  { HackingStation.NotifyJockEnableShockFX(); }
    public void AnimEvent_JockShock()          { HackingStation.NotifyJockShock(); }
    public void AnimEvent_JockFall()           { HackingStation.NotifyJockFall(); }
    public void AnimEvent_JockDisableShockFX() { HackingStation.NotifyJockDisableShockFX(); }
}
