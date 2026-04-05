using UnityEngine;

/// <summary>
/// Mirrors StatsEquipmentBridge but for weapon-specific combat behaviour.
///
/// Drop this on the player GameObject alongside AutoAttackAbility.
/// Wire up the three references in the Inspector and it will automatically
/// update AutoAttackAbility and PlayerAbilities whenever the Weapon slot changes.
///
/// When the weapon is unequipped (or has no weapon profile) everything reverts
/// to the values you originally set in the AutoAttackAbility / PlayerAbilities
/// Inspectors, so those remain your "unarmed / default" values.
/// </summary>
public class WeaponEquipBridge : MonoBehaviour
{
    [Header("References")]
    public EquipmentState equipment;
    public PlayerAbilities playerAbilities;
    public AutoAttackAbility autoAttack;

    // ---- Inspector-set defaults, captured once in Awake ----
    private GameObject _defaultProjectilePrefab;
    private float      _defaultProjectileSpeed;
    private int        _defaultRangeTiles;
    private float      _defaultAttackInterval;
    private GameObject _defaultImpactVfx;
    private AudioClip  _defaultImpactSfx;

    // -------------------------------------------------------------------------
    void Awake()
    {
        // Snapshot whatever the designer set in the Inspector.
        // These become the fallback when no weapon (or a weapon without a profile)
        // is equipped.
        if (playerAbilities)
        {
            _defaultProjectilePrefab = playerAbilities.projectilePrefab;
            _defaultProjectileSpeed  = playerAbilities.projectileSpeed;
        }

        if (autoAttack)
        {
            _defaultRangeTiles     = autoAttack.weaponRangeTiles;
            _defaultAttackInterval = autoAttack.weaponAttackInterval;
            _defaultImpactVfx      = autoAttack.nerdImpactVfx;
            _defaultImpactSfx      = autoAttack.nerdImpactSfx;
        }
    }

    void OnEnable()
    {
        if (equipment) equipment.OnEquipmentChanged += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        if (equipment) equipment.OnEquipmentChanged -= Refresh;
    }

    // -------------------------------------------------------------------------
    /// <summary>
    /// Called automatically whenever anything in EquipmentState changes.
    /// Reads the Weapon slot and pushes its profile to AutoAttackAbility /
    /// PlayerAbilities, or restores defaults if no weapon profile is present.
    /// </summary>
    public void Refresh()
    {
        var weapon   = equipment ? equipment.Get(EquipSlot.Weapon) : null;
        var template = weapon?.template;

        if (template == null || !template.hasWeaponProfile)
        {
            RestoreDefaults();
            return;
        }

        // ---- PlayerAbilities ------------------------------------------------
        if (playerAbilities)
        {
            playerAbilities.projectilePrefab =
                template.weaponProjectilePrefab ? template.weaponProjectilePrefab : _defaultProjectilePrefab;

            playerAbilities.projectileSpeed =
                template.weaponProjectileSpeed > 0f ? template.weaponProjectileSpeed : _defaultProjectileSpeed;
        }

        // ---- AutoAttackAbility ----------------------------------------------
        if (autoAttack)
        {
            autoAttack.weaponRangeTiles     = template.weaponRangeTiles > 0
                ? template.weaponRangeTiles : _defaultRangeTiles;

            autoAttack.weaponAttackInterval = template.weaponAttackInterval > 0f
                ? template.weaponAttackInterval : _defaultAttackInterval;

            // VFX / SFX — fall back to Inspector defaults when null
            autoAttack.nerdImpactVfx = template.weaponImpactVfx ? template.weaponImpactVfx : _defaultImpactVfx;
            autoAttack.nerdImpactSfx = template.weaponImpactSfx ? template.weaponImpactSfx : _defaultImpactSfx;

            // Shot pattern + per-pattern tuning
            autoAttack.activePattern           = template.shotPattern;
            autoAttack.spreadShots             = template.weaponSpreadShots;
            autoAttack.spreadAngleDegrees      = template.weaponSpreadAngle;
            autoAttack.spreadRangeMultiplier   = template.weaponSpreadRangeMultiplier;
            autoAttack.spreadDamageMultiplier  = template.weaponSpreadDamageMultiplier;
            autoAttack.burstCount              = template.weaponBurstCount;
            autoAttack.burstDelay              = template.weaponBurstDelay;
        }
    }

    // -------------------------------------------------------------------------
    void RestoreDefaults()
    {
        if (playerAbilities)
        {
            playerAbilities.projectilePrefab = _defaultProjectilePrefab;
            playerAbilities.projectileSpeed  = _defaultProjectileSpeed;
        }

        if (autoAttack)
        {
            autoAttack.weaponRangeTiles     = _defaultRangeTiles;
            autoAttack.weaponAttackInterval = _defaultAttackInterval;
            autoAttack.nerdImpactVfx        = _defaultImpactVfx;
            autoAttack.nerdImpactSfx        = _defaultImpactSfx;
            autoAttack.activePattern        = WeaponShotPattern.Single;
            // spread / burst fields don't matter when pattern is Single
        }
    }
}
