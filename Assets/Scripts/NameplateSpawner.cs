using UnityEngine;

[DisallowMultipleComponent]
public class NameplateSpawner : MonoBehaviour
{
    [Header("Prefab")]
    public NameplateUI nameplatePrefab;

    [Header("Optional")]
    public bool overrideOffset = false;
    public Vector3 offsetOverride = new Vector3(0f, 1.0f, 0f);
    public string displayNameOverride;

    NameplateUI instance;

    void Start()
    {
        if (!nameplatePrefab) return;

        instance = Instantiate(nameplatePrefab);

        // Parent to this NPC so it dies with it
        instance.SetTarget(transform, string.IsNullOrEmpty(displayNameOverride) ? gameObject.name : displayNameOverride);

        if (overrideOffset)
        {
            instance.worldOffset = offsetOverride;
            if (instance.parentToTarget && instance.transform.parent == transform && instance.useLocalPositionWhenParented)
                instance.transform.localPosition = instance.worldOffset;
        }
    }
}
