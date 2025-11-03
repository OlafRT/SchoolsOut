// QuestEvents.cs
using System;

public static class QuestEvents
{
    public static Action<string> EnemyKilled;          // enemyId
    public static Action<string,int> ItemLooted;       // itemId, amount
    public static Action<string> PlaceReached;         // placeId/triggerId
    public static Action<string> NpcTalked;            // npcId (after dialogue)
}
