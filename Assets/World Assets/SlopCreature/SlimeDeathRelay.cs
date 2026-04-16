using UnityEngine;

/// <summary>
/// Mirrors RoachDeathRelay — watches NPCHealth on the parent and calls SlimeJiggle.Die()
/// when HP hits zero.
///
/// SETUP:
///   Attach anywhere on the NPC (or a child). It will auto-find SlimeJiggle in children
///   and NPCHealth in parents, just like RoachDeathRelay does for the cockroach.
/// </summary>
[DisallowMultipleComponent]
public class SlimeDeathRelay : MonoBehaviour
{
    [Tooltip("Auto-found in children if left empty.")]
    public SlimeJiggle slime;
    [Tooltip("Optional: also trigger a hit-jiggle on each damage event (not just death).")]
    public bool jiggleOnHit = true;

    NPCHealth _hp;
    int _prevHP;
    bool _fired;

    void Awake()
    {
        if (!slime) slime = GetComponentInChildren<SlimeJiggle>();
        _hp = GetComponentInParent<NPCHealth>();
        if (!_hp) _hp = GetComponent<NPCHealth>();

        if (_hp) _prevHP = _hp.currentHP;
    }

    void Update()
    {
        if (!_hp || !slime) return;

        // Hit jiggle on any damage taken
        if (jiggleOnHit && _hp.currentHP < _prevHP && !_fired)
            slime.PlayHitJiggle();

        _prevHP = _hp.currentHP;

        // Death
        if (_fired) return;
        if (_hp.currentHP <= 0)
        {
            _fired = true;
            slime.Die();
        }
    }
}
