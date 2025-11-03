// QuestDefinition.cs
using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName="RPG/Quests/Quest", fileName="Q_NewQuest")]
public class QuestDefinition : ScriptableObject
{
    [Header("Basics")]
    public string questId;                  // unique
    public string title;
    [TextArea(2,5)] public string description;

    [Header("Turn-in")]
    public string giverNpcId;               // who offers it
    public string turnInNpcId;              // who completes it (can be same)

    [Header("Rewards")]
    public int xpReward = 50;
    public int moneyReward = 0;

    [Header("Objectives (AND)")]
    public List<ObjectiveSpec> objectives = new();

    [Serializable]
    public class ObjectiveSpec
    {
        public enum Type { Kill, Collect, Reach, Talk }
        public Type type = Type.Kill;

        [Tooltip("Identifier used by reporters (enemyId, itemId, placeId, npcId).")]
        public string targetId;

        [Tooltip("How many are needed (ignored for Reach/Talk).")]
        public int requiredCount = 1;
    }
}
