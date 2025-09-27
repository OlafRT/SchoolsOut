using UnityEngine;

public class NerdAnimRelay : MonoBehaviour
{
    [SerializeField] private AutoAttackAbility autoAttack; // optional; auto-find
    [SerializeField] private BombAbility bomb;             // optional; auto-find
    [SerializeField] private ChainShockAbility chainShock; // optional; auto-find

    void Awake()
    {
        if (!autoAttack) autoAttack = GetComponentInParent<AutoAttackAbility>();
        if (!bomb)       bomb       = GetComponentInParent<BombAbility>();
        if (!chainShock) chainShock = GetComponentInParent<ChainShockAbility>();
    }

    // Nerd auto attack (Throw)
    public void AnimEvent_FireAutoAttack()
    {
        if (autoAttack) autoAttack.AnimEvent_FireAutoAttack();
    }

    // Nerd Bomb
    public void AnimEvent_ReleaseBomb()
    {
        if (bomb) bomb.AnimEvent_ReleaseBomb();
    }

    // Nerd Chain Shock
    public void AnimEvent_ChainShockRelease()
    {
        if (chainShock) chainShock.AnimEvent_ChainShockRelease();
    }
}
