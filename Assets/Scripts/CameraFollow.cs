using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform player;
    public Vector3 offset = new Vector3(0, 10, -10);

    // Shaker (and other systems) can add to this safely.
    [HideInInspector] public Vector3 extraOffset = Vector3.zero;

    void LateUpdate()
    {
        if (!player) return;
        transform.position = player.position + offset + extraOffset;
    }
}