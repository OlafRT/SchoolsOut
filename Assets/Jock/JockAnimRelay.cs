using UnityEngine;

public class JockAnimRelay : MonoBehaviour
{
    [Header("Abilities")]
    [SerializeField] private StrikeAbility strike;
    [SerializeField] private AutoAttackAbility autoAttack;

    void Awake()
    {
        if (!strike)     strike     = GetComponentInParent<StrikeAbility>()     ?? FindObjectOfType<StrikeAbility>(true);
        if (!autoAttack) autoAttack = GetComponentInParent<AutoAttackAbility>() ?? FindObjectOfType<AutoAttackAbility>(true);
    }

    // ====== STRIKE ======
    // Impact event (already used)
    public void AnimEvent_FireStrike()         { if (strike) strike.AnimEvent_FireStrike(); }
    // whoosh pre-impact
    public void AnimEvent_StrikeWhoosh()       { if (strike) strike.AnimEvent_StrikeWhoosh(); }

    // ====== JOCK AUTO ATTACK ======
    public void AnimEvent_FireJockAutoAttack() { if (autoAttack) autoAttack.AnimEvent_FireJockAutoAttack(); }
    public void AnimEvent_JockSwingWhoosh()    { if (autoAttack) autoAttack.AnimEvent_JockSwingWhoosh(); }

    // ====== existing fusebox events (unchanged) ======
    public void AnimEvent_JockSmashImpact()    { HackingStation.NotifyJockSmashImpact(); }
    public void AnimEvent_JockEnableShockFX()  { HackingStation.NotifyJockEnableShockFX(); }
    public void AnimEvent_JockShock()          { HackingStation.NotifyJockShock(); }
    public void AnimEvent_JockFall()           { HackingStation.NotifyJockFall(); }
    public void AnimEvent_JockDisableShockFX() { HackingStation.NotifyJockDisableShockFX(); }
}
