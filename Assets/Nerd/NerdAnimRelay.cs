using UnityEngine;

public class NerdAnimRelay : MonoBehaviour
{
    [SerializeField] private AutoAttackAbility autoAttack; // optional; auto-find
    [SerializeField] private BombAbility bomb;             // optional; auto-find

    void Awake()
    {
        if (!autoAttack) autoAttack = GetComponentInParent<AutoAttackAbility>();
        if (!bomb)       bomb       = GetComponentInParent<BombAbility>();
    }

    // Called by the animation event on the Nerd "Throw" clip
    public void AnimEvent_FireAutoAttack()
    {
        if (autoAttack) autoAttack.AnimEvent_FireAutoAttack();
    }

    // Called by the animation event on the Nerd "Bomb" clip
    public void AnimEvent_ReleaseBomb()
    {
        if (bomb) bomb.AnimEvent_ReleaseBomb();
    }
}
