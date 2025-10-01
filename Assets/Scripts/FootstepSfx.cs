using UnityEngine;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class FootstepSfx : MonoBehaviour
{
    [System.Serializable]
    public class SurfaceClips
    {
        public SurfaceKind surface = SurfaceKind.Default;
        [Tooltip("Candidates; one will be chosen at random.")]
        public AudioClip[] clips;
        [Range(0f, 2f)] public float volume = 1f;
    }

    [Header("Audio")]
    [Tooltip("Optional 3D AudioSource on the character. If omitted, we spawn a one-shot at the hit point.")]
    public AudioSource audioSource;
    [Range(0f, 2f)] public float baseVolume = 1f;
    [Tooltip("Pitch = 1 + random[-variance, +variance] each step.")]
    [Range(0f, 1f)] public float pitchVariance = 0.08f;

    [Header("Surfaces")]
    [Tooltip("Map surface types to their clip sets. Provide at least a 'Default' entry.")]
    public SurfaceClips[] surfaces;
    [Tooltip("If no FootstepSurface is found, use this surface.")]
    public SurfaceKind fallbackSurface = SurfaceKind.Default;

    [Header("Ray / Layers")]
    [Tooltip("Layers considered 'ground'.")]
    public LayerMask groundLayers = ~0;
    [Tooltip("How far down we check for ground.")]
    public float rayDistance = 2.0f;
    [Tooltip("Offset upward from character position to start the ray.")]
    public float raycastUpOffset = 0.2f;

    [Header("Behavior")]
    [Tooltip("Minimum time between step sounds (seconds), to prevent rapid spam.")]
    public float minInterval = 0.07f;
    [Tooltip("If true, spawn one-shot source at the contact point for better spatial feel when no AudioSource is assigned.")]
    public bool oneShotAtContactWhenNoSource = true;

    // --- runtime ---
    Dictionary<SurfaceKind, SurfaceClips> map;
    Dictionary<SurfaceKind, int> lastIdxPerSurface = new();
    float lastStepTime = -999f;

    void Awake()
    {
        if (!audioSource) audioSource = GetComponent<AudioSource>(); // optional
        map = new Dictionary<SurfaceKind, SurfaceClips>();
        if (surfaces != null)
        {
            foreach (var s in surfaces)
            {
                if (s == null) continue;
                map[s.surface] = s;
            }
        }
    }

    // Call this from an animation event on foot-plant frames
    public void AnimEvent_OnStep()
    {
        if (Time.time - lastStepTime < minInterval) return;

        // Find ground
        Vector3 origin = transform.position + Vector3.up * raycastUpOffset;
        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, rayDistance, groundLayers, QueryTriggerInteraction.Ignore))
            return;

        // Identify surface
        var surf = hit.collider.GetComponentInParent<FootstepSurface>();
        SurfaceKind kind = surf ? surf.surface : fallbackSurface;

        // Pick clip
        if (!map.TryGetValue(kind, out var bank) || bank.clips == null || bank.clips.Length == 0)
        {
            // try default as final fallback
            if (!map.TryGetValue(SurfaceKind.Default, out bank) || bank.clips == null || bank.clips.Length == 0)
                return;
            kind = SurfaceKind.Default;
        }

        int idx = RandomIdxNoImmediateRepeat(bank.clips.Length, kind);
        var clip = bank.clips[idx];
        if (!clip) return;

        float vol = baseVolume * bank.volume;
        float pitch = 1f + Random.Range(-pitchVariance, +pitchVariance);

        // Play
        if (audioSource)
        {
            audioSource.pitch = pitch;
            audioSource.spatialBlend = 1f;
            audioSource.PlayOneShot(clip, vol);
        }
        else if (oneShotAtContactWhenNoSource)
        {
            PlayClipAtPointPitched(clip, hit.point, vol, pitch);
        }
        else
        {
            AudioSource.PlayClipAtPoint(clip, transform.position, vol); // no pitch control
        }

        lastStepTime = Time.time;
    }

    int RandomIdxNoImmediateRepeat(int length, SurfaceKind kind)
    {
        if (length <= 1) return 0;
        int last = lastIdxPerSurface.TryGetValue(kind, out var li) ? li : -1;
        int idx = Random.Range(0, length);
        if (idx == last) idx = (idx + 1) % length;
        lastIdxPerSurface[kind] = idx;
        return idx;
    }

    static void PlayClipAtPointPitched(AudioClip clip, Vector3 pos, float volume, float pitch)
    {
        var go = new GameObject("OneShot_Footstep");
        go.transform.position = pos;
        var a = go.AddComponent<AudioSource>();
        a.clip = clip;
        a.volume = Mathf.Clamp01(volume);
        a.pitch  = Mathf.Clamp(pitch, 0.5f, 2f);
        a.spatialBlend = 1f;
        a.rolloffMode = AudioRolloffMode.Logarithmic;
        a.minDistance = 1.5f;
        a.maxDistance = 18f;
        a.Play();
        Object.Destroy(go, clip.length / Mathf.Max(0.01f, a.pitch) + 0.1f);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0, 1, 0, 0.25f);
        Vector3 origin = transform.position + Vector3.up * raycastUpOffset;
        Gizmos.DrawLine(origin, origin + Vector3.down * rayDistance);
    }
#endif

    // Optional convenience if you have separate L/R events:
    public void AnimEvent_OnStepLeft()  => AnimEvent_OnStep();
    public void AnimEvent_OnStepRight() => AnimEvent_OnStep();
}
