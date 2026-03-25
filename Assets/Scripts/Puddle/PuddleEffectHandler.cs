using UnityEngine;

/// <summary>
/// Attach this to the player (or any child of the player).
/// Handles spawning the splash particle effect and playing the splash sound
/// whenever the player steps into a PuddleTile trigger.
///
/// SETUP:
///   1. Attach this script to your player GameObject (or a child).
///   2. Assign a ParticleSystem prefab to Splash Particle Prefab.
///      - The particle will be spawned at the puddle position facing upward.
///   3. Assign an AudioClip to Splash Sound Clip.
///   4. (Optional) Tune Volume, Pitch Variance, and Particle Y Offset.
///
/// HOW IT WORKS:
///   - PuddleTile.OnTriggerEnter calls PlaySplash(puddleWorldPos) on this component.
///   - A particle system is instantiated at the puddle position and auto-destroyed.
///   - An AudioSource on this GameObject plays the splash clip with slight random pitch.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class PuddleEffectHandler : MonoBehaviour
{
    [Header("Particle")]
    [Tooltip("A ParticleSystem prefab for the water splash. Spawned at the puddle position.")]
    [SerializeField] private ParticleSystem splashParticlePrefab;

    [Tooltip("Extra upward offset so the particle spawns just above the ground plane.")]
    [SerializeField] private float particleYOffset = 0.05f;

    [Header("Sound")]
    [Tooltip("The splash AudioClip to play on each puddle step.")]
    [SerializeField] private AudioClip splashSoundClip;

    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.8f;

    [Tooltip("Random pitch range applied each splash for natural variation. 0 = no variation.")]
    [SerializeField] private float pitchVariance = 0.1f;

    [Tooltip("Base pitch for the splash sound.")]
    [SerializeField] private float basePitch = 1f;

    // Cached AudioSource — RequireComponent guarantees it exists
    private AudioSource _audioSource;

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();

        // Configure the AudioSource for one-shot style playback
        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 0f; // 2D by default; set to 1 for 3D positional audio
    }

    /// <summary>
    /// Called by PuddleTile when the player enters the puddle trigger.
    /// Spawns the splash particle and plays the splash sound.
    /// </summary>
    /// <param name="puddleWorldPosition">World position of the puddle centre.</param>
    public void PlaySplash(Vector3 puddleWorldPosition)
    {
        SpawnParticle(puddleWorldPosition);
        PlaySound();
    }

    private void SpawnParticle(Vector3 worldPos)
    {
        if (splashParticlePrefab == null)
        {
            Debug.LogWarning("[PuddleEffectHandler] No splash particle prefab assigned.", this);
            return;
        }

        Vector3 spawnPos = new Vector3(worldPos.x, worldPos.y + particleYOffset, worldPos.z);

        // Instantiate upright so the splash fires upward
        ParticleSystem ps = Instantiate(splashParticlePrefab, spawnPos, Quaternion.identity);
        ps.Play();

        // Auto-destroy after the particle finishes
        float lifetime = ps.main.duration + ps.main.startLifetime.constantMax;
        Destroy(ps.gameObject, lifetime + 0.5f); // small buffer
    }

    private void PlaySound()
    {
        if (splashSoundClip == null)
        {
            Debug.LogWarning("[PuddleEffectHandler] No splash sound clip assigned.", this);
            return;
        }

        _audioSource.pitch = basePitch + Random.Range(-pitchVariance, pitchVariance);
        _audioSource.PlayOneShot(splashSoundClip, volume);
    }
}
