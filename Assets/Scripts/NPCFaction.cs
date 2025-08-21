using UnityEngine;

public enum NPCFaction
{
    Jock,
    Nerd,
    Girl,
    Goth,
    Teacher,
    Parent,
    Boss
}

public static class NPCFactionUtil
{
    /// <summary>
    /// Maps an NPC faction to the player’s "class school" used by your damage/ability scaling,
    /// if it has an obvious match. Others return null (we'll treat them as their own thing).
    /// </summary>
    public static PlayerStats.AbilitySchool? ToAbilitySchool(this NPCFaction f)
    {
        switch (f)
        {
            case NPCFaction.Jock: return PlayerStats.AbilitySchool.Jock;
            case NPCFaction.Nerd: return PlayerStats.AbilitySchool.Nerd;
            default: return null; // Girl/Goth/Teacher/Parent/Boss can have their own schools later
        }
    }

    /// <summary>
    /// True if this faction is exactly one of the two player schools.
    /// </summary>
    public static bool IsPlayerSchool(this NPCFaction f) =>
        f == NPCFaction.Jock || f == NPCFaction.Nerd;

    /// <summary>
    /// Convenience: does this faction match the given school (Jock/Nerd only)?
    /// </summary>
    public static bool MatchesSchool(this NPCFaction f, PlayerStats.AbilitySchool school)
    {
        var s = f.ToAbilitySchool();
        return s.HasValue && s.Value == school;
    }
}