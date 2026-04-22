using System.Collections;
using UnityEngine;

/// <summary>
/// Applied to the player when they stand in gas too long.
/// Locks movement + attacking, walks them toward the principal,
/// principal laughs then hits + knocks back, control returns.
/// </summary>
[DisallowMultipleComponent]
public class MindControlEffect : MonoBehaviour
{
    [Header("Walk to Boss")]
    public float walkSpeed = 2.5f;
    [Tooltip("How close the player needs to get before the boss hits.")]
    public float hitRange = 1.2f;

    [Header("Knockback")]
    [Tooltip("How many tiles the player is knocked back away from the boss. Uses wall-safe tile stepping.")]
    public int knockbackTiles = 4;

    [Header("Visual")]
    [Tooltip("Optional VFX parented to the player while mind controlled.")]
    public GameObject mindControlVfxPrefab;

    PrincipalBoss       _boss;
    bool                _active;
    GameObject          _vfxInstance;
    PlayerMovement      _playerMovement;
    AutoAttackAbility   _autoAttack;

    void Awake()
    {
        _playerMovement = GetComponent<PlayerMovement>();
        _autoAttack     = GetComponent<AutoAttackAbility>();
    }

    public void Activate(PrincipalBoss boss)
    {
        if (_active) return;
        _boss = boss;
        StartCoroutine(MindControlSequence());
    }

    IEnumerator MindControlSequence()
    {
        _active = true;

        // Lock movement via PlayerMovement.canMove
        if (_playerMovement) _playerMovement.canMove = false;

        // Suppress auto-attack
        if (_autoAttack) _autoAttack.IsSuppressedByOtherAbilities = true;

        // Show HUD banner
        PlayerHUD.SetMindControlled(true);

        // Spawn VFX
        if (mindControlVfxPrefab)
            _vfxInstance = Instantiate(mindControlVfxPrefab, transform);

        // Let boss play a laugh reaction
        if (_boss) _boss.OnMindControlLand();
        yield return new WaitForSeconds(0.6f);

        // Walk toward boss
        while (_boss && !_boss.IsDead)
        {
            Vector3 dir = _boss.transform.position - transform.position;
            dir.y = 0f;
            if (dir.magnitude <= hitRange) break;
            dir.Normalize();

            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(dir, Vector3.up),
                1f - Mathf.Exp(-10f * Time.deltaTime));

            Vector3 move = dir * walkSpeed * Time.deltaTime;
            transform.position += move;

            yield return null;
        }

        // Boss hit
        if (_boss && !_boss.IsDead)
            _boss.HitMindControlledPlayer(gameObject);

        // Knockback — wall-safe via PlayerMovement.ApplyKnockback
        if (_boss && _playerMovement)
        {
            Vector3 away = transform.position - _boss.transform.position;
            away.y = 0f;
            if (away.sqrMagnitude < 0.001f) away = Vector3.back;
            _playerMovement.ApplyKnockback(away, knockbackTiles);
        }

        Release();
    }

    void Release()
    {
        _active = false;
        if (_playerMovement) _playerMovement.canMove = true;
        if (_autoAttack)     _autoAttack.IsSuppressedByOtherAbilities = false;
        PlayerHUD.SetMindControlled(false);
        if (_vfxInstance) { Destroy(_vfxInstance); _vfxInstance = null; }
        Destroy(this);
    }
}
