using UnityEngine;

[DisallowMultipleComponent]
public class ZoneVolume : MonoBehaviour
{
    [Header("Zone")]
    public string zoneName = "Unnamed Zone";
    [Tooltip("Higher wins if zones overlap. Ties resolved by most recently entered.")]
    public int priority = 0;

    [Header("Box (local space, oriented by this Transform)")]
    public Vector3 center = Vector3.zero;
    public Vector3 size   = new Vector3(10, 5, 10);

    [Header("Music (optional)")]
    public AudioClip music;
    [Range(0f,1f)] public float musicVolume = 0.8f;
    [Tooltip("Seconds to fade IN when entering this zone.")]
    public float musicFadeIn = 1.0f;
    [Tooltip("Seconds to fade OUT when leaving this zone.")]
    public float musicFadeOut = 1.0f;
    public bool musicLoop = true;

    // Runtime: tracked by the service
    [HideInInspector] public bool _inside;
    [HideInInspector] public int  _enterSeq;

    public bool Contains(Vector3 worldPos)
    {
        Vector3 p = transform.InverseTransformPoint(worldPos) - center;
        Vector3 half = size * 0.5f;
        return Mathf.Abs(p.x) <= half.x &&
               Mathf.Abs(p.y) <= half.y &&
               Mathf.Abs(p.z) <= half.z;
    }

    void OnEnable()  => AreaZoneService.Instance?.Register(this);
    void OnDisable() => AreaZoneService.Instance?.Unregister(this);

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.25f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(center, size);
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
        Gizmos.DrawWireCube(center, size);
    }
    void Reset()
    {
        var bc = GetComponent<BoxCollider>();
        if (bc) { center = bc.center; size = bc.size; }
    }
#endif
}
