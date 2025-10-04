using System.Collections;
using UnityEngine;
using System.Linq;

/// <summary>
/// Disables ability components that are not allowed for the current player class,
/// so they can't be used even if they're present on the player.
/// Runs once on Start and can be called again (e.g., after a class swap).
/// </summary>
[DefaultExecutionOrder(-20)] // after PlayerBootstrap (-100), before most gameplay
public class AbilityClassGate : MonoBehaviour
{
    PlayerAbilities ctx;

    void Awake()
    {
        ctx = GetComponent<PlayerAbilities>();
        if (!ctx) Debug.LogWarning($"{name}: AbilityClassGate requires PlayerAbilities on the same GameObject.", this);
    }

    void Start()
    {
        ApplyGate();
    }

    /// <summary>Call this if you ever change class at runtime.</summary>
    public void ApplyGate()
    {
        if (!ctx) return;

        // Gather all MonoBehaviours that look like abilities (either declare IAbilityUI or IClassRestrictedAbility).
        var abilities = GetComponents<MonoBehaviour>()
            .Where(mb => mb != null && mb.enabled)
            .Where(mb => mb is IAbilityUI || mb is IClassRestrictedAbility)
            .ToArray();

        foreach (var mb in abilities)
        {
            bool allowed = AllowedForPlayer(mb, ctx.playerClass);
            mb.enabled = allowed; // hard gate: if not allowed, it won't read input or tick Update

#if UNITY_EDITOR
            // For visibility in the inspector during play mode
            // Debug.Log($"[AbilityClassGate] {(allowed ? "ENABLED " : "DISABLED")} {mb.GetType().Name} for {ctx.playerClass}", mb);
#endif
        }
    }

    // Mirrors the filtering used by ActionBarAutoBinder, so UI and logic agree.
    bool AllowedForPlayer(MonoBehaviour mb, PlayerAbilities.PlayerClass playerClass)
    {
        // If the component declares its restriction, honor it.
        if (mb is IClassRestrictedAbility cra)
        {
            switch (cra.AllowedFor)
            {
                case AbilityClassRestriction.Both: return true;
                case AbilityClassRestriction.Jock: return playerClass == PlayerAbilities.PlayerClass.Jock;
                case AbilityClassRestriction.Nerd: return playerClass == PlayerAbilities.PlayerClass.Nerd;
            }
        }

        // If it also implements IAbilityUI, try to use the explicit AbilityName first
        if (mb is IAbilityUI ui && !string.IsNullOrEmpty(ui.AbilityName))
        {
            string n = ui.AbilityName.ToLowerInvariant();
            if (n.Contains("charge") || n.Contains("kick") || n.Contains("strike"))
                return playerClass == PlayerAbilities.PlayerClass.Jock;
            if (n.Contains("bomb") || n.Contains("warp"))
                return playerClass == PlayerAbilities.PlayerClass.Nerd;
            if (n.Contains("autoattack") || n == "auto attack" || n == "auto-attack")
                return true; // shared
        }

        // Fallback: infer from type name so older abilities still get gated.
        string t = mb.GetType().Name.ToLowerInvariant();
        if (t.Contains("strike") || t.Contains("kick") || t.Contains("charge"))
            return playerClass == PlayerAbilities.PlayerClass.Jock;
        if (t.Contains("bomb") || t.Contains("warp"))
            return playerClass == PlayerAbilities.PlayerClass.Nerd;
        if (t.Contains("autoattack"))
            return true;

        // Default: allow both
        return true;
    }
}
