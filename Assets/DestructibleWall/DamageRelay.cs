using UnityEngine;

[DisallowMultipleComponent]
public class DamageRelay : MonoBehaviour, IDamageable
{
    public DestructibleWallV2 wallV2;

    void Awake()
    {
        if (!wallV2) wallV2 = GetComponentInParent<DestructibleWallV2>();
        if (!wallV2)
            Debug.LogError($"{name}: DamageRelay couldn't find DestructibleWallV2 in parents.", this);
    }

    public void ApplyDamage(int amount)
    {
        // Only let STRIKE break/boost the wall
        if (!StrikeAbility.IsStrikeWindowOpen) return;

        if (!wallV2)
        {
            wallV2 = GetComponentInParent<DestructibleWallV2>();
            if (!wallV2) return;
        }
        wallV2.RegisterHit();
    }
}
