using UnityEngine;

public class Compass : MonoBehaviour
{
    public RectTransform compassBackground; // The image with N/E/S/W markings
    public Transform playerTransform;       // Your player or camera transform

    void Update()
    {
        if (compassBackground == null || playerTransform == null) return;

        // Get the player's rotation around the Y-axis
        float playerYaw = playerTransform.eulerAngles.y;

        // Rotate the background to simulate world rotation
        compassBackground.localEulerAngles = new Vector3(0, 0, playerYaw);
    }
}