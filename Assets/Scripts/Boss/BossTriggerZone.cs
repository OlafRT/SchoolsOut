using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class BossTriggerZone : MonoBehaviour
{
    [Header("Refs")]
    public CafeteriaLadyBoss boss;
    public BossHazardZone hazardZone;

    [Header("Settings")]
    public string triggerTag = "Player";
    [Tooltip("Optionally block the entrance once the fight starts (drag a door/gate GO here).")]
    public GameObject arenaGate;

    bool triggered = false;

    void Awake()
    {
        var col = GetComponent<BoxCollider>();
        col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (triggered) return;
        if (!other.CompareTag(triggerTag)) return;
        triggered = true;

        if (boss)       boss.ActivateFight();
        if (hazardZone) hazardZone.Activate();
        if (arenaGate)  arenaGate.SetActive(true);

        gameObject.SetActive(false);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 1f, 0f, 0.25f);
        var col = GetComponent<BoxCollider>();
        if (col) Gizmos.DrawCube(
            transform.TransformPoint(col.center),
            Vector3.Scale(transform.lossyScale, col.size));
    }
}
