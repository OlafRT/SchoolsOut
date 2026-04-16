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
    bool _spawned;

    // OnEnable instead of Start so this fires correctly when SlimeVentEmerge
    // re-enables this component after the slime has landed.
    // The _spawned guard ensures only one nameplate is ever created even if
    // the component is toggled multiple times.
    void OnEnable()
    {
        if (_spawned || !nameplatePrefab) return;
        _spawned = true;

        instance = Instantiate(nameplatePrefab);

        instance.SetTarget(transform, string.IsNullOrEmpty(displayNameOverride)
            ? gameObject.name
            : displayNameOverride);

        if (overrideOffset)
        {
            instance.worldOffset = offsetOverride;
            if (instance.parentToTarget
                && instance.transform.parent == transform
                && instance.useLocalPositionWhenParented)
                instance.transform.localPosition = instance.worldOffset;
        }
    }
}
