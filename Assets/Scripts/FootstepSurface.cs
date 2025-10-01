using UnityEngine;

public enum SurfaceKind
{
    Default, Stone, Wood, Dirt, Grass, Metal, Snow, Water
}

[DisallowMultipleComponent]
public class FootstepSurface : MonoBehaviour
{
    [Tooltip("How this surface should sound when stepped on.")]
    public SurfaceKind surface = SurfaceKind.Default;
}
