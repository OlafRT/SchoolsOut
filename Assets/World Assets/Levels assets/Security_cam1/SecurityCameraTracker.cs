using UnityEngine;

public class SecurityCameraTracker : MonoBehaviour
{
    [Header("Tracking Settings")]
    public float rotationSpeed = 5f;
    public float maxVerticalAngle = 45f;
    public float maxHorizontalAngle = 90f;

    private Transform player;
    private bool isTracking;

    private float currentY;
    private float currentZ;

    void Start()
    {
        Vector3 angles = transform.localEulerAngles;
        currentY = angles.y;
        currentZ = angles.z;
    }

    void Update()
    {
        if (!isTracking || player == null)
            return;

        Vector3 direction = player.position - transform.position;

        // Convert to parent space
        Vector3 localDir = transform.parent.InverseTransformDirection(direction);

        float targetY = Mathf.Atan2(localDir.z, -localDir.x) * Mathf.Rad2Deg;
        float targetZ = -Mathf.Atan2(localDir.y, -localDir.x) * Mathf.Rad2Deg; // FIXED SIGN

        targetY = Mathf.Clamp(targetY, -maxHorizontalAngle, maxHorizontalAngle);
        targetZ = Mathf.Clamp(targetZ, -maxVerticalAngle, maxVerticalAngle);

        currentY = Mathf.LerpAngle(currentY, targetY, Time.deltaTime * rotationSpeed);
        currentZ = Mathf.LerpAngle(currentZ, targetZ, Time.deltaTime * rotationSpeed);

        transform.localRotation = Quaternion.Euler(0f, currentY, currentZ);
    }

    public void SetPlayer(Transform p)
    {
        player = p;
        isTracking = true;
    }

    public void ClearPlayer()
    {
        player = null;
        isTracking = false;
    }
}
