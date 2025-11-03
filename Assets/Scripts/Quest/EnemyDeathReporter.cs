// EnemyDeathReporter.cs
using UnityEngine;

public class EnemyDeathReporter : MonoBehaviour
{
    public string enemyId = "rat";
    public void ReportDeath(){ QuestEvents.EnemyKilled?.Invoke(enemyId); }
    // Call ReportDeath() from your enemy’s death logic.
}
