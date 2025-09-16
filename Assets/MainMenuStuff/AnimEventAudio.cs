using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class AnimEventAudio : MonoBehaviour
{
    [System.Serializable]
    public class Cue
    {
        [Tooltip("Name used from Animation Event, e.g. PlayCue(\"Event1\")")]
        public string key = "Event1";

        [Tooltip("One or more clips; if multiple, a random variant is chosen.")]
        public AudioClip[] clips;

        [Range(0f, 1f)]
        public float volume = 1f;

        [Tooltip("± pitch variation (e.g., 0.05 = ±5%)")]
        [Range(0f, 0.3f)]
        public float pitchJitter = 0.05f;
    }

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;    // 2D source (on camera/canvas)
    [SerializeField, Range(0f, 1f)] private float globalVolume = 1f;

    [Header("Cues (named)")]
    [SerializeField] private Cue[] cues;                 // Use with PlayCue(string)

    [Header("Indexed Clips (optional)")]
    [SerializeField] private AudioClip[] indexedClips;   // Use with PlayCueIndex(int)

    // cache
    readonly Dictionary<string, Cue> _map = new Dictionary<string, Cue>();
    float _basePitch = 1f;

    void Awake()
    {
        if (!audioSource) audioSource = GetComponent<AudioSource>();
        if (audioSource) _basePitch = audioSource.pitch;
        RebuildMap();
    }

    void OnValidate()
    {
        RebuildMap();
        if (audioSource) _basePitch = audioSource.pitch;
    }

    void RebuildMap()
    {
        _map.Clear();
        if (cues == null) return;
        foreach (var c in cues)
        {
            if (c == null || string.IsNullOrEmpty(c.key)) continue;
            if (!_map.ContainsKey(c.key))
                _map.Add(c.key, c);
        }
    }

    // ========= Animation Event entry points =========

    // Call this from Animation Events that pass a STRING parameter (key)
    public void PlayCue(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            Debug.LogWarning("[AnimEventAudio] Empty key passed.");
            return;
        }
        if (!_map.TryGetValue(key, out var cue))
        {
            Debug.LogWarning($"[AnimEventAudio] No cue named '{key}'.");
            return;
        }
        PlayCueInternal(cue);
    }

    // Call this from Animation Events that pass an INT parameter (index)
    public void PlayCueIndex(int index)
    {
        if (indexedClips == null || index < 0 || index >= indexedClips.Length)
        {
            Debug.LogWarning($"[AnimEventAudio] Index {index} out of range.");
            return;
        }
        var clip = indexedClips[index];
        if (!clip) return;

        // Use defaults for indexed plays
        PlayClip(clip, 1f, 0.05f);
    }

    // ========= Internals =========

    void PlayCueInternal(Cue cue)
    {
        if (cue.clips == null || cue.clips.Length == 0) return;
        var clip = cue.clips[cue.clips.Length == 1 ? 0 : Random.Range(0, cue.clips.Length)];
        if (!clip) return;

        PlayClip(clip, cue.volume, cue.pitchJitter);
    }

    void PlayClip(AudioClip clip, float vol, float jitter)
    {
        if (!audioSource || !clip) return;

        float j = Random.Range(-jitter, jitter);
        float newPitch = Mathf.Clamp(_basePitch * (1f + j), 0.5f, 2f);

        audioSource.pitch = newPitch;
        audioSource.PlayOneShot(clip, vol * globalVolume);
        audioSource.pitch = _basePitch; // restore immediately after scheduling
    }
}
