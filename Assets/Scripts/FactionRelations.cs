using System.Collections.Generic;
using UnityEngine;

public static class FactionRelations
{
    private static readonly Dictionary<(NPCFaction, NPCFaction), NPCAI.Hostility> relations =
        new Dictionary<(NPCFaction, NPCFaction), NPCAI.Hostility>
    {
        // --- Jock’s perspective ---
        { (NPCFaction.Jock, NPCFaction.Nerd), NPCAI.Hostility.Hostile },
        { (NPCFaction.Jock, NPCFaction.Girl), NPCAI.Hostility.Friendly },
        { (NPCFaction.Jock, NPCFaction.Teacher), NPCAI.Hostility.Neutral },
        { (NPCFaction.Jock, NPCFaction.Parent), NPCAI.Hostility.Neutral },
        { (NPCFaction.Jock, NPCFaction.Boss), NPCAI.Hostility.Hostile },

        // --- Nerd’s perspective ---
        { (NPCFaction.Nerd, NPCFaction.Jock), NPCAI.Hostility.Neutral },
        { (NPCFaction.Nerd, NPCFaction.Goth), NPCAI.Hostility.Neutral },
        { (NPCFaction.Nerd, NPCFaction.Girl), NPCAI.Hostility.Neutral },
        { (NPCFaction.Nerd, NPCFaction.Teacher), NPCAI.Hostility.Friendly },
        { (NPCFaction.Nerd, NPCFaction.Parent), NPCAI.Hostility.Friendly },
        { (NPCFaction.Nerd, NPCFaction.Boss), NPCAI.Hostility.Hostile },

        // --- Goth’s perspective ---
        { (NPCFaction.Goth, NPCFaction.Jock), NPCAI.Hostility.Hostile },
        { (NPCFaction.Goth, NPCFaction.Nerd), NPCAI.Hostility.Neutral },
        { (NPCFaction.Goth, NPCFaction.Boss), NPCAI.Hostility.Hostile },

        // --- Girl’s perspective ---
        { (NPCFaction.Girl, NPCFaction.Jock), NPCAI.Hostility.Friendly },
        { (NPCFaction.Girl, NPCFaction.Nerd), NPCAI.Hostility.Neutral },
        { (NPCFaction.Girl, NPCFaction.Boss), NPCAI.Hostility.Hostile },

        // --- Teacher’s perspective ---
        { (NPCFaction.Teacher, NPCFaction.Nerd), NPCAI.Hostility.Friendly },
        { (NPCFaction.Teacher, NPCFaction.Jock), NPCAI.Hostility.Neutral },
        { (NPCFaction.Teacher, NPCFaction.Boss), NPCAI.Hostility.Hostile },

        // --- Parent’s perspective ---
        { (NPCFaction.Parent, NPCFaction.Nerd), NPCAI.Hostility.Friendly },
        { (NPCFaction.Parent, NPCFaction.Jock), NPCAI.Hostility.Neutral },
        { (NPCFaction.Parent, NPCFaction.Boss), NPCAI.Hostility.Hostile },

        // --- Boss’s perspective ---
        { (NPCFaction.Boss, NPCFaction.Jock), NPCAI.Hostility.Hostile },
        { (NPCFaction.Boss, NPCFaction.Nerd), NPCAI.Hostility.Hostile },
        { (NPCFaction.Boss, NPCFaction.Girl), NPCAI.Hostility.Hostile },
        { (NPCFaction.Boss, NPCFaction.Teacher), NPCAI.Hostility.Hostile },
        { (NPCFaction.Boss, NPCFaction.Parent), NPCAI.Hostility.Hostile },
    };

    /// <summary>
    /// Get how "a" feels about "b". Defaults to Neutral if not listed.
    /// </summary>
    public static NPCAI.Hostility GetRelation(NPCFaction a, NPCFaction b)
    {
        if (a == b) return NPCAI.Hostility.Friendly; // same faction default
        if (relations.TryGetValue((a, b), out var rel)) return rel;
        return NPCAI.Hostility.Neutral;
    }
}