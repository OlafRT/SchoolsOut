using UnityEngine;

public class SecurityCameraTrigger : MonoBehaviour
{
    public SecurityCameraTracker tracker;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            tracker.SetPlayer(other.transform);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            tracker.ClearPlayer();
        }
    }
}
