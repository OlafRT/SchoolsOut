using UnityEngine;
using System.Linq;
using System.Collections.Generic;

public class ActionBarAutoBinder : MonoBehaviour
{
    [Tooltip("The player that has the ability components (StrikeAbility, BombAbility, etc).")]
    public GameObject player;

    [Tooltip("Your 5 UI slots in the bar, left-to-right.")]
    public AbilitySlotUI[] slots = new AbilitySlotUI[5];

    [Tooltip("Optional explicit order by ability name. Leave empty to use component order.")]
    public string[] abilityNames = new string[0]; // e.g. {"Strike","Charge","Bomb"}

    void Start()
    {
        if (!player) { Debug.LogWarning("ActionBarAutoBinder: No player assigned."); return; }

        var ctx = player.GetComponent<PlayerAbilities>();
        if (!ctx)
        {
            Debug.LogWarning("ActionBarAutoBinder: PlayerAbilities not found on player.");
            return;
        }

        // discover all ability components
        var all = player.GetComponents<MonoBehaviour>().OfType<IAbilityUI>().ToList();
        if (all.Count == 0) { Debug.LogWarning("ActionBarAutoBinder: No abilities found on player."); return; }

        // filter by: learned & class restriction
        var filtered = all.Where(a => a.IsLearned)
                          .Where(a => AllowedForPlayer(a, ctx.playerClass))
                          .ToList();

        // optional ordering by names
        List<IAbilityUI> ordered = new List<IAbilityUI>();
        if (abilityNames != null && abilityNames.Length > 0)
        {
            foreach (var name in abilityNames)
            {
                var match = filtered.FirstOrDefault(a => a.AbilityName == name);
                if (match != null) ordered.Add(match);
            }
            // add any remaining filtered abilities not listed
            ordered.AddRange(filtered.Where(a => !ordered.Contains(a)));
        }
        else
        {
            ordered = filtered;
        }

        // bind up to 5
        for (int i = 0; i < slots.Length; i++)
        {
            if (i < ordered.Count)
                slots[i].abilityComponent = (MonoBehaviour)ordered[i];
            else
                slots[i].abilityComponent = null; // ensure slot clears
        }
    }

    bool AllowedForPlayer(IAbilityUI a, PlayerAbilities.PlayerClass playerClass)
    {
        // If the ability declares its restriction, honor it.
        if (a is IClassRestrictedAbility cra)
        {
            return cra.AllowedFor switch
            {
                AbilityClassRestriction.Both => true,
                AbilityClassRestriction.Jock => playerClass == PlayerAbilities.PlayerClass.Jock,
                AbilityClassRestriction.Nerd => playerClass == PlayerAbilities.PlayerClass.Nerd,
                _ => true
            };
        }

        // Fallback by common names/types so you don't have to modify every ability immediately.
        string n = a.AbilityName?.ToLowerInvariant() ?? "";
        if (string.IsNullOrEmpty(n))
            n = a.GetType().Name.ToLowerInvariant();

        // map known abilities
        if (n.Contains("charge") || n.Contains("kick") || n.Contains("strike"))
            return playerClass == PlayerAbilities.PlayerClass.Jock;

        if (n.Contains("bomb"))
            return playerClass == PlayerAbilities.PlayerClass.Nerd;

        if (n.Contains("autoattack") || n == "auto attack" || n == "auto-attack")
            return true; // both

        // default: allow both
        return true;
    }
}
