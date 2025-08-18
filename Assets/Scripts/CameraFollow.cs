using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform player; // Reference to the player's transform
    public Vector3 offset;   // Offset position relative to the player

    void Start()
    {
        // Optional: Set an initial offset if none is specified
        if (offset == Vector3.zero)
            offset = new Vector3(0, 10, -10);
    }

    void LateUpdate()
    {
        // Update camera position to follow the player
        transform.position = player.position + offset;
    }
}
