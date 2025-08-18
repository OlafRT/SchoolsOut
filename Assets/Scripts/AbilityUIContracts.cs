using UnityEngine;

public interface IAbilityUI
{
    string AbilityName { get; }
    Sprite Icon { get; }
    KeyCode Key { get; }

    // Cooldown state
    float CooldownRemaining { get; }   // seconds remaining (0 when ready)
    float CooldownDuration { get; }    // total duration (0 if none)

    // Whether the player currently knows this ability
    bool IsLearned { get; }
}

// Optional class restriction for abilities
public enum AbilityClassRestriction { Both, Jock, Nerd }

public interface IClassRestrictedAbility
{
    AbilityClassRestriction AllowedFor { get; }
}

// NEW (optional): exposes stack/charge info to the UI
public interface IChargeableAbility
{
    int CurrentCharges { get; }
    int MaxCharges { get; }
}