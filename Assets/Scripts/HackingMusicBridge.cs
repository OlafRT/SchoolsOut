using System.Linq;
using UnityEngine;

[RequireComponent(typeof(HackingStation))]
public class HackingMusicBridge : MonoBehaviour
{
    [Header("Hacking Music")]
    [SerializeField] private AudioSource hackingMusic;      // child music for the minigame
    [SerializeField] private bool loopHackingMusic = true;

    [Header("Zone Music (optional)")]
    [Tooltip("Leave empty to auto-detect the zone music AudioSource at runtime.")]
    [SerializeField] private AudioSource zoneMusicSource;

    private HackingStation _station;
    private bool _zoneWasPlaying;
    private float _zonePrevVolume;

    void Awake()
    {
        _station = GetComponent<HackingStation>();
        if (hackingMusic) hackingMusic.loop = loopHackingMusic;

        _station.onHackStart.AddListener(HandleHackStart);
        _station.onHackEnd.AddListener(HandleHackEnd);
    }

    void OnDestroy()
    {
        if (_station != null)
        {
            _station.onHackStart.RemoveListener(HandleHackStart);
            _station.onHackEnd.RemoveListener(HandleHackEnd);
        }
    }

    void HandleHackStart()
    {
        if (!zoneMusicSource) zoneMusicSource = AutoFindZoneMusic();

        // Pause zone music (more robust than volume-ducking vs. external fades)
        if (zoneMusicSource)
        {
            _zonePrevVolume = zoneMusicSource.volume;
            _zoneWasPlaying = zoneMusicSource.isPlaying;
            if (_zoneWasPlaying) zoneMusicSource.Pause();
        }

        if (hackingMusic)
        {
            hackingMusic.time = 0f;
            hackingMusic.Play();
        }
    }

    void HandleHackEnd()
    {
        if (hackingMusic) hackingMusic.Stop();

        // Resume zone music exactly as it was
        if (zoneMusicSource && _zoneWasPlaying)
        {
            // UnPause resumes where it left off, preserving external fades/logic
            zoneMusicSource.UnPause();
            zoneMusicSource.volume = _zonePrevVolume;
        }
    }

    // ---- Heuristic finder for the zone music AudioSource ----
    AudioSource AutoFindZoneMusic()
    {
        // 1) Prefer a looping, playing AudioSource whose clip matches any ZoneVolume.music
        var allSources = FindObjectsOfType<AudioSource>(true);
        var zoneClips = FindObjectsOfType<ZoneVolume>(true)
                        .Where(z => z.enabled && z.music != null)
                        .Select(z => z.music)
                        .Distinct()
                        .ToHashSet();

        AudioSource best = null;

        foreach (var src in allSources)
        {
            if (!src) continue;
            if (!src.loop) continue;
            if (!src.isPlaying) continue;
            if (src.clip == null) continue;

            if (zoneClips.Contains(src.clip))
            {
                best = src;
                break;
            }
        }

        if (best) return best;

        // 2) Fallback: any looping, currently playing AudioSource (likely the zone music)
        best = allSources.FirstOrDefault(s => s && s.loop && s.isPlaying && s.clip != null);
        if (best) return best;

        // 3) Last resort: any looping AudioSource with a clip
        best = allSources.FirstOrDefault(s => s && s.loop && s.clip != null);
        return best;
    }
}
