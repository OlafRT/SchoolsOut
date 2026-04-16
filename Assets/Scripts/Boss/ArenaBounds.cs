using UnityEngine;

/// <summary>
/// Drop this on an empty GameObject inside the player arena and size its BoxCollider
/// to match the floor area where the cafeteria lady's pot can land.
/// CafeteriaLadyBoss reads its bounds — no coordinates to type in manually.
///
/// SETUP:
///   1. Create an empty GameObject, name it "BossArena" or similar.
///   2. Add BoxCollider — make it a trigger, flatten the Y so it sits on the floor.
///   3. Resize until it covers the blue square area from your top-down view.
///   4. Drag this GameObject into CafeteriaLadyBoss → Arena Bounds field.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class ArenaBounds : MonoBehaviour
{
    [Header("Gizmo")]
    public Color gizmoColor = new Color(0.2f, 0.4f, 1f, 0.18f);
    public Color gizmoWireColor = new Color(0.3f, 0.5f, 1f, 0.9f);

    BoxCollider _col;

    void Awake()
    {
        _col = GetComponent<BoxCollider>();
        _col.isTrigger = true; // never block anything
    }

    /// <summary>Returns a random world-space XZ point inside the arena at floor height.</summary>
    public Vector3 GetRandomPoint()
    {
        if (!_col) _col = GetComponent<BoxCollider>();
        Bounds b = _col.bounds;

        return new Vector3(
            Random.Range(b.min.x, b.max.x),
            b.center.y,
            Random.Range(b.min.z, b.max.z));
    }

    /// <summary>Returns the world-space bounds (for range checks etc.).</summary>
    public Bounds GetBounds()
    {
        if (!_col) _col = GetComponent<BoxCollider>();
        return _col.bounds;
    }

    void OnDrawGizmos()
    {
        var col = GetComponent<BoxCollider>();
        if (!col) return;

        Vector3 center = transform.TransformPoint(col.center);
        Vector3 size   = Vector3.Scale(transform.lossyScale, col.size);

        Gizmos.color = gizmoColor;
        Gizmos.DrawCube(center, size);

        Gizmos.color = gizmoWireColor;
        Gizmos.DrawWireCube(center, size);
    }
}
