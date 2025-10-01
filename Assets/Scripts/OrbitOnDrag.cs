using UnityEngine;
using UnityEngine.EventSystems;

public class OrbitOnDrag : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    [SerializeField] Camera orbitCam;
    [SerializeField] Transform target;     // pivot on your character
    [SerializeField] float distance = 3f;
    [SerializeField] float sensitivity = 0.25f;
    [SerializeField] float minPitch = -10f, maxPitch = 80f;

    float yaw, pitch;
    bool leftIsDown;
    int activePointerId = -1;

    void Start()
    {
        if (!orbitCam || !target) return;
        Vector3 dir = (orbitCam.transform.position - target.position).normalized;
        distance = Vector3.Distance(orbitCam.transform.position, target.position);
        pitch = Mathf.Asin(dir.y) * Mathf.Rad2Deg;
        yaw   = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        UpdateCamera();
    }

    public void OnPointerDown(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left) return; // left only
        leftIsDown = true;
        activePointerId = e.pointerId;
        e.Use();
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (e.pointerId == activePointerId) { leftIsDown = false; activePointerId = -1; }
    }

    public void OnDrag(PointerEventData e)
    {
        if (!leftIsDown || e.pointerId != activePointerId || !orbitCam || !target) return;
        yaw   += e.delta.x * sensitivity;
        pitch -= e.delta.y * sensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        UpdateCamera();
    }

    void UpdateCamera()
    {
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 pos = target.position + rot * (Vector3.back * distance);
        orbitCam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(target.position - pos, Vector3.up));
    }
}
