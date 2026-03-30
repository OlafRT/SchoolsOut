using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Full weather controller with:
///  - Particle emission rate fading (single ParticleSystem)
///  - Rain audio fade in/out
///  - Prefab-based jagged lightning bolts (LightningLine style)
///  - Thunder audio delayed by strike distance (speed of sound)
///  - Player damage on lightning hit
///  - Puddle decals that spawn/fade based on wetness
///  - Global wetness shader value + optional per-material override
///  - Post-rain drying delay + configurable dry speed
/// </summary>
[DisallowMultipleComponent]
public class WeatherController : MonoBehaviour
{
    // ═══════════════════════════════════════════
    //  Enums / State
    // ═══════════════════════════════════════════
    public enum WeatherState { Sunny, LightRain, HeavyRain }

    [Header("Weather State")]
    public WeatherState currentWeather = WeatherState.Sunny;

    // ═══════════════════════════════════════════
    //  Rain Particles  (single system, emission-rate driven)
    // ═══════════════════════════════════════════
    [Header("Rain Particles")]
    [Tooltip("A single ParticleSystem used for all rain. Emission rate is lerped between states " +
             "so you only need one system instead of separate light/heavy GameObjects.")]
    public ParticleSystem rainParticleSystem;

    [Tooltip("Emission rate (particles/sec) when it is lightly raining.")]
    public float lightRainEmissionRate  = 150f;

    [Tooltip("Emission rate (particles/sec) when it is heavily raining.")]
    public float heavyRainEmissionRate  = 600f;

    [Tooltip("How fast emission ramps up or down (particles/sec per second).")]
    public float emissionRampSpeed      = 120f;

    // ═══════════════════════════════════════════
    //  Rain Audio  (single AudioSource, volume faded)
    // ═══════════════════════════════════════════
    [Header("Rain Audio")]
    [Tooltip("AudioSource already set up with an ambient rain loop clip. " +
             "Set it to loop and Play On Awake = false; this script drives the volume.")]
    public AudioSource rainAudioSource;

    [Tooltip("Volume when lightly raining (0-1).")]
    public float lightRainVolume    = 0.35f;

    [Tooltip("Volume when heavily raining (0-1).")]
    public float heavyRainVolume    = 0.9f;

    [Tooltip("How quickly the rain audio volume fades in / out.")]
    public float rainAudioFadeSpeed = 1.5f;

    // ═══════════════════════════════════════════
    //  Directional Light
    // ═══════════════════════════════════════════
    [Header("Sun / Directional Light")]
    public Light  directionalLight;
    public float  sunnyLightIntensity       = 1f;
    public float  lightRainLightIntensity   = 0.8f;
    public float  heavyRainLightIntensity   = 0.6f;
    public float  lightChangeSpeed          = 1.5f;

    // ═══════════════════════════════════════════
    //  Weather Timing
    // ═══════════════════════════════════════════
    [Header("Weather Timing")]
    public float minSunnyDuration       = 60f;
    public float maxSunnyDuration       = 180f;
    public float minLightRainDuration   = 20f;
    public float maxLightRainDuration   = 60f;
    public float minHeavyRainDuration   = 15f;
    public float maxHeavyRainDuration   = 45f;

    [Range(0f, 1f)] public float chanceToStartLightRain           = 0.18f;
    [Range(0f, 1f)] public float chanceToStartHeavyRain           = 0.08f;
    [Range(0f, 1f)] public float chanceForLightRainToBecomeHeavy  = 0.35f;

    // ═══════════════════════════════════════════
    //  Thunder / Lightning  (prefab-based bolt, like ChainShockAbility)
    // ═══════════════════════════════════════════
    [Header("Thunder")]
    public bool enableThunder = true;

    [Tooltip("Prefab root with a LightningLine component + child LineRenderers (Core, Glow). " +
             "Same prefab style as ChainShockAbility.linePrefabGO.")]
    public GameObject lightningLinePrefab;

    [Tooltip("Lifetime passed to the bolt prefab's LightningLine component.")]
    public float lightningLifetime        = 0.22f;

    [Tooltip("Max XZ jitter at the top of the bolt (fades to 0 at ground). World units.")]
    public float lightningJitter          = 1.4f;

    [Tooltip("Number of points in the generated bolt path. More = more detailed jagged look.")]
    [Range(4, 32)]
    public int   lightningSegments        = 12;

    [Tooltip("Height above the ground point where the bolt starts.")]
    public float thunderHeight            = 32f;

    [Tooltip("Y-offset for the bolt's ground endpoint (raise if it clips into the terrain).")]
    public float thunderGroundOffset      = 0f;

    [Tooltip("Horizontal radius around the player within which lightning can strike. World units.")]
    public float thunderRangeAroundPlayer = 26f;

    public float thunderMinInterval = 5f;
    public float thunderMaxInterval = 14f;

    [Range(0f, 1f)]
    public float thunderStrikeChance = 0.35f;

    [Tooltip("Optional point or spot light for a brief full-scene flash on each strike.")]
    public Light  thunderFlashLight;
    public float  thunderFlashDuration = 0.12f;

    // ── Thunder audio ──
    [Tooltip("AudioSource used to play thunder SFX. A separate source from rain is recommended.")]
    public AudioSource  thunderAudioSource;
    public AudioClip[]  thunderClips;

    [Tooltip("Approximate speed of sound in m/s used to delay thunder after the lightning flash. " +
             "343 m/s is real-world; lower values exaggerate the delay for dramatic effect.")]
    public float speedOfSound   = 343f;

    [Tooltip("Maximum seconds the thunder can be delayed regardless of distance.")]
    public float maxThunderDelay = 4f;

    // ═══════════════════════════════════════════
    //  Player Damage from Lightning
    // ═══════════════════════════════════════════
    [Header("Player Damage from Lightning")]
    [Tooltip("World-unit sphere radius around the strike point that counts as a direct hit.")]
    public float  lightningDamageRadius = 2.5f;

    [Tooltip("Raw damage dealt to the player on a direct lightning hit. " +
             "Adapt DamagePlayer() to your own health system.")]
    public int    lightningDamage       = 35;

    [Tooltip("Layer mask containing the player collider.")]
    public LayerMask playerLayer;

    [Tooltip("Tag on the player GameObject used as a secondary check.")]
    public string playerTag = "Player";

    // ═══════════════════════════════════════════
    //  Puddle Decals
    // ═══════════════════════════════════════════
    [Header("Puddle Decals")]
    [Tooltip("Prefab for a puddle decal (flat quad / projector). " +
             "The prefab's Renderer color alpha is driven by wetness automatically.")]
    public GameObject puddleDecalPrefab;

    [Tooltip("Maximum number of puddles that can exist at once.")]
    public int   maxPuddles              = 14;

    [Tooltip("Horizontal radius around the player within which puddles can spawn. World units.")]
    public float puddleSpawnRadius       = 22f;

    [Tooltip("Wetness level (0-1) that must be reached before puddles start appearing.")]
    [Range(0f, 1f)]
    public float puddleWetnessThreshold  = 0.45f;

    [Tooltip("How often (seconds) a new puddle is tried while above the wetness threshold.")]
    public float puddleSpawnInterval     = 7f;

    [Tooltip("Wetness level below which all puddles begin fading out and are destroyed.")]
    [Range(0f, 1f)]
    public float puddleFadeOutThreshold  = 0.15f;

    [Tooltip("How quickly the puddle material alpha changes (fade-in and fade-out).")]
    public float puddleFadeSpeed         = 1.2f;

    [Tooltip("Layer mask used when raycasting to place puddles on the terrain surface.")]
    public LayerMask puddleRaycastLayer;

    // ═══════════════════════════════════════════
    //  Wet Ground — Shader + Terrain Smoothness
    // ═══════════════════════════════════════════
    [Header("Wet Ground")]
    [Range(0f, 1f)] public float wetness = 0f;

    public float wetnessIncreaseLightRain   = 0.08f;
    public float wetnessIncreaseHeavyRain   = 0.18f;

    [Tooltip("Seconds after rain stops before the ground starts to dry. " +
             "Simulates the ground staying wet even after the rain ends.")]
    public float postRainDryDelay   = 12f;

    [Tooltip("Rate at which wetness decreases per second once the dry delay has elapsed.")]
    public float wetnessDrySpeed    = 0.025f;

    [Tooltip("Sends _GlobalWetness to all shaders that read it each frame.")]
    public bool  sendWetnessToShader = true;

    [Header("Optional Extra Material")]
    [Tooltip("A material with a float wetness property (e.g. a custom wet ground shader).")]
    public Material terrainMaterialInstance;
    public string   terrainWetnessProperty = "_Wetness";

    [Header("Terrain (optional)")]
    [Tooltip("Used only to sample terrain height so lightning bolts land flush with the ground " +
             "on hilly terrain. Does not modify the terrain or its layers in any way.")]
    public Terrain targetTerrain;

    // ═══════════════════════════════════════════
    //  Private Runtime State
    // ═══════════════════════════════════════════

    // Lighting
    private float targetLightIntensity;

    // Coroutines
    private Coroutine weatherLoopRoutine;
    private Coroutine thunderRoutine;

    // Rain particles
    private float targetEmissionRate;

    // Rain audio
    private float targetRainVolume;

    // Puddles
    private readonly List<GameObject> activePuddles = new List<GameObject>();
    private float nextPuddleSpawnTime;

    // Wetness drying
    private float dryDelayTimer;

    // Cached shader property IDs (avoid string lookups per frame)
    private static readonly int ColorPropID     = Shader.PropertyToID("_Color");
    private static readonly int BaseColorPropID = Shader.PropertyToID("_BaseColor"); // URP/HDRP

    // ═══════════════════════════════════════════
    //  Unity Lifecycle
    // ═══════════════════════════════════════════
    private void Start()
    {
        InitRainParticles();
        InitRainAudio();
        ApplyWeatherVisualsImmediately(currentWeather);
        weatherLoopRoutine = StartCoroutine(WeatherLoop());
    }

    private void Update()
    {
        UpdateLighting();
        UpdateWetness();
        UpdateWetnessOutputs();
        UpdateRainParticles();
        UpdateRainAudio();
        UpdatePuddles();
    }

    // ═══════════════════════════════════════════
    //  Initialisation Helpers
    // ═══════════════════════════════════════════

    private void InitRainParticles()
    {
        if (!rainParticleSystem) return;

        // Stop and wipe any particles that were baked into the component in the Editor.
        // Without this, the system briefly emits at its configured rate for one frame
        // before the script zeroes it out, causing a visible burst on scene start.
        rainParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var em = rainParticleSystem.emission;
        em.rateOverTime    = 0f;
        targetEmissionRate = 0f;

        // Play at zero emission so it is instantly ready to ramp up when rain starts
        rainParticleSystem.Play();
    }

    private void InitRainAudio()
    {
        if (!rainAudioSource) return;
        rainAudioSource.volume = 0f;
        targetRainVolume = 0f;
        if (!rainAudioSource.isPlaying) rainAudioSource.Play();
    }

    // ═══════════════════════════════════════════
    //  Weather Loop
    // ═══════════════════════════════════════════
    private IEnumerator WeatherLoop()
    {
        while (true)
        {
            switch (currentWeather)
            {
                case WeatherState.Sunny:
                {
                    yield return new WaitForSeconds(Random.Range(minSunnyDuration, maxSunnyDuration));
                    float roll = Random.value;
                    if      (roll <= chanceToStartHeavyRain)                             SetWeather(WeatherState.HeavyRain);
                    else if (roll <= chanceToStartHeavyRain + chanceToStartLightRain)    SetWeather(WeatherState.LightRain);
                    else                                                                 SetWeather(WeatherState.Sunny);
                    break;
                }
                case WeatherState.LightRain:
                {
                    yield return new WaitForSeconds(Random.Range(minLightRainDuration, maxLightRainDuration));
                    SetWeather(Random.value <= chanceForLightRainToBecomeHeavy
                        ? WeatherState.HeavyRain : WeatherState.Sunny);
                    break;
                }
                case WeatherState.HeavyRain:
                {
                    yield return new WaitForSeconds(Random.Range(minHeavyRainDuration, maxHeavyRainDuration));
                    SetWeather(Random.value < 0.4f ? WeatherState.LightRain : WeatherState.Sunny);
                    break;
                }
            }
        }
    }

    // ═══════════════════════════════════════════
    //  Public Weather API
    // ═══════════════════════════════════════════
    public void SetWeather(WeatherState newWeather)
    {
        if (currentWeather == newWeather) return;
        currentWeather = newWeather;
        ApplyWeatherVisualsImmediately(currentWeather);
    }

    private void ApplyWeatherVisualsImmediately(WeatherState state)
    {
        switch (state)
        {
            case WeatherState.Sunny:
                targetLightIntensity = sunnyLightIntensity;
                targetEmissionRate   = 0f;
                targetRainVolume     = 0f;
                dryDelayTimer        = postRainDryDelay; // start the post-rain delay countdown
                StopThunderRoutine();
                HideFlashLight();
                break;

            case WeatherState.LightRain:
                targetLightIntensity = lightRainLightIntensity;
                targetEmissionRate   = lightRainEmissionRate;
                targetRainVolume     = lightRainVolume;
                dryDelayTimer        = 0f;               // raining again — reset dry delay
                StopThunderRoutine();
                HideFlashLight();
                break;

            case WeatherState.HeavyRain:
                targetLightIntensity = heavyRainLightIntensity;
                targetEmissionRate   = heavyRainEmissionRate;
                targetRainVolume     = heavyRainVolume;
                dryDelayTimer        = 0f;
                StartThunderRoutine();
                break;
        }
    }

    // ═══════════════════════════════════════════
    //  Per-Frame Updates
    // ═══════════════════════════════════════════

    private void UpdateLighting()
    {
        if (!directionalLight) return;
        directionalLight.intensity = Mathf.Lerp(
            directionalLight.intensity, targetLightIntensity,
            Time.deltaTime * lightChangeSpeed);
    }

    // Smoothly ramp the single particle system's emission rate between states
    private void UpdateRainParticles()
    {
        if (!rainParticleSystem) return;
        var   em   = rainParticleSystem.emission;
        float next = Mathf.MoveTowards(em.rateOverTime.constant, targetEmissionRate,
                                       emissionRampSpeed * Time.deltaTime);
        em.rateOverTime = next;
    }

    // Fade rain audio volume toward the per-state target
    private void UpdateRainAudio()
    {
        if (!rainAudioSource) return;
        rainAudioSource.volume = Mathf.Lerp(
            rainAudioSource.volume, targetRainVolume,
            Time.deltaTime * rainAudioFadeSpeed);
    }

    // ── Wetness ─────────────────────────────────────────────────────────────

    private void UpdateWetness()
    {
        switch (currentWeather)
        {
            case WeatherState.LightRain:
                wetness += wetnessIncreaseLightRain * Time.deltaTime;
                break;

            case WeatherState.HeavyRain:
                wetness += wetnessIncreaseHeavyRain * Time.deltaTime;
                break;

            case WeatherState.Sunny:
                // Tick the post-rain delay down before allowing ground to dry.
                // This simulates the ground staying soaked for a while after rain.
                if (dryDelayTimer > 0f)
                    dryDelayTimer -= Time.deltaTime;
                else
                    wetness -= wetnessDrySpeed * Time.deltaTime;
                break;
        }

        wetness = Mathf.Clamp01(wetness);
    }

    private void UpdateWetnessOutputs()
    {
        // Global shader float (any material reading _GlobalWetness)
        if (sendWetnessToShader)
            Shader.SetGlobalFloat("_GlobalWetness", wetness);

        // Optional per-material override
        if (terrainMaterialInstance && terrainMaterialInstance.HasProperty(terrainWetnessProperty))
            terrainMaterialInstance.SetFloat(terrainWetnessProperty, wetness);
    }

    // ── Puddle Decals ────────────────────────────────────────────────────────

    private void UpdatePuddles()
    {
        if (!puddleDecalPrefab) return;

        bool aboveSpawnThreshold = wetness >= puddleWetnessThreshold;
        bool belowFadeThreshold  = wetness <  puddleFadeOutThreshold;

        // Attempt to spawn a new puddle when wet enough and under the cap
        if (aboveSpawnThreshold && activePuddles.Count < maxPuddles
            && Time.time >= nextPuddleSpawnTime)
        {
            nextPuddleSpawnTime = Time.time + puddleSpawnInterval;
            TrySpawnPuddle();
        }

        // Target alpha: fade in above threshold, fade out below
        float targetAlpha = belowFadeThreshold
            ? 0f
            : Mathf.InverseLerp(puddleWetnessThreshold * 0.5f, puddleWetnessThreshold, wetness);

        // Reuse one MPB per frame — allocate outside the loop
        var mpb = new MaterialPropertyBlock();

        for (int i = activePuddles.Count - 1; i >= 0; i--)
        {
            var puddle = activePuddles[i];
            if (!puddle) { activePuddles.RemoveAt(i); continue; }

            var rend = puddle.GetComponentInChildren<Renderer>();
            if (!rend) continue;

            // Read current alpha via property block (no material instance created)
            rend.GetPropertyBlock(mpb);
            Color c     = mpb.GetColor(ColorPropID);
            float alpha = Mathf.MoveTowards(c.a, targetAlpha, puddleFadeSpeed * Time.deltaTime);
            c.a         = alpha;

            mpb.SetColor(ColorPropID,     c);
            mpb.SetColor(BaseColorPropID, c); // URP / HDRP
            rend.SetPropertyBlock(mpb);

            // Destroy once fully invisible and wetness is low
            if (alpha <= 0f && belowFadeThreshold)
            {
                Destroy(puddle);
                activePuddles.RemoveAt(i);
            }
        }
    }

    private void TrySpawnPuddle()
    {
        // Random point in a circle around the player, cast downward onto the ground
        Vector2 circle   = Random.insideUnitCircle * puddleSpawnRadius;
        Vector3 probe    = transform.position + new Vector3(circle.x, 200f, circle.y);

        if (!Physics.Raycast(probe, Vector3.down, out RaycastHit hit, 400f, puddleRaycastLayer))
            return;

        // Nudge slightly above surface to avoid z-fighting
        Vector3    spawnPos = hit.point + hit.normal * 0.02f;
        Quaternion spawnRot = Quaternion.LookRotation(hit.normal)
                            * Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);

        var puddle = Instantiate(puddleDecalPrefab, spawnPos, spawnRot);

        // Start fully transparent — UpdatePuddles fades it in each frame
        var rend = puddle.GetComponentInChildren<Renderer>();
        if (rend)
        {
            var mpb = new MaterialPropertyBlock();
            rend.GetPropertyBlock(mpb);
            Color c = Color.white; c.a = 0f;
            mpb.SetColor(ColorPropID,     c);
            mpb.SetColor(BaseColorPropID, c);
            rend.SetPropertyBlock(mpb);
        }

        activePuddles.Add(puddle);
    }

    // ═══════════════════════════════════════════
    //  Thunder Orchestration
    // ═══════════════════════════════════════════

    private void StartThunderRoutine()
    {
        if (!enableThunder) return;
        StopThunderRoutine();
        thunderRoutine = StartCoroutine(ThunderLoop());
    }

    private void StopThunderRoutine()
    {
        if (thunderRoutine == null) return;
        StopCoroutine(thunderRoutine);
        thunderRoutine = null;
    }

    private IEnumerator ThunderLoop()
    {
        while (currentWeather == WeatherState.HeavyRain)
        {
            yield return new WaitForSeconds(Random.Range(thunderMinInterval, thunderMaxInterval));
            if (currentWeather != WeatherState.HeavyRain) yield break;
            if (Random.value <= thunderStrikeChance)
                yield return StartCoroutine(DoThunderStrike());
        }
    }

    // ═══════════════════════════════════════════
    //  Thunder Strike
    // ═══════════════════════════════════════════

    private IEnumerator DoThunderStrike()
    {
        // ── Pick strike position ──────────────────────────────────────────
        Vector3 strikeXZ = transform.position + new Vector3(
            Random.Range(-thunderRangeAroundPlayer, thunderRangeAroundPlayer),
            0f,
            Random.Range(-thunderRangeAroundPlayer, thunderRangeAroundPlayer));

        float groundY = thunderGroundOffset;
        if (targetTerrain)
            groundY = targetTerrain.SampleHeight(strikeXZ) + targetTerrain.transform.position.y;

        Vector3 groundPos = new Vector3(strikeXZ.x, groundY + thunderGroundOffset, strikeXZ.z);
        Vector3 skyPos    = new Vector3(strikeXZ.x, groundY + thunderHeight,       strikeXZ.z);

        // ── Spawn lightning bolt prefab ───────────────────────────────────
        if (lightningLinePrefab)
        {
            List<Vector3> boltPath = BuildLightningBolt(skyPos, groundPos);
            GameObject fx = Instantiate(lightningLinePrefab);

            var ll = fx.GetComponent<LightningLine>();
            if (ll) ll.lifetime = lightningLifetime;

            foreach (var lr in fx.GetComponentsInChildren<LineRenderer>(true))
            {
                lr.positionCount = boltPath.Count;
                lr.SetPositions(boltPath.ToArray());
            }
        }

        // ── Scene flash ───────────────────────────────────────────────────
        if (thunderFlashLight)
        {
            thunderFlashLight.transform.position = Vector3.Lerp(skyPos, groundPos, 0.5f);
            thunderFlashLight.enabled = true;
        }
        if (directionalLight) directionalLight.intensity += 0.8f;

        // ── Player damage check ───────────────────────────────────────────
        CheckPlayerHit(groundPos);

        // ── Thunder audio — delayed by distance ──────────────────────────
        // The flash is instant; the boom arrives later based on speed of sound.
        // distance / speedOfSound gives seconds of delay, clamped to maxThunderDelay.
        if (thunderClips != null && thunderClips.Length > 0)
        {
            float dist  = Vector3.Distance(transform.position, groundPos);
            float delay = Mathf.Clamp(dist / Mathf.Max(1f, speedOfSound), 0f, maxThunderDelay);
            StartCoroutine(PlayThunderAfterDelay(delay));
        }

        // ── Wait for flash to end, then hide ─────────────────────────────
        yield return new WaitForSeconds(thunderFlashDuration);
        HideFlashLight();
    }

    private IEnumerator PlayThunderAfterDelay(float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        if (!thunderAudioSource || thunderClips == null || thunderClips.Length == 0) yield break;
        thunderAudioSource.PlayOneShot(thunderClips[Random.Range(0, thunderClips.Length)]);
    }

    // ═══════════════════════════════════════════
    //  Lightning Bolt Path Builder
    //  Vertical adaptation of ChainShockAbility's BuildTilePathSawtooth.
    //  Jitter fades toward 0 at ground level so the bolt converges cleanly.
    // ═══════════════════════════════════════════
    private List<Vector3> BuildLightningBolt(Vector3 top, Vector3 bottom)
    {
        var path = new List<Vector3>();
        int segs = Mathf.Max(2, lightningSegments);

        for (int i = 0; i <= segs; i++)
        {
            float   t  = i / (float)segs;
            Vector3 pt = Vector3.Lerp(top, bottom, t);

            if (i > 0 && i < segs)
            {
                float jScale = (1f - t) * lightningJitter;
                float sign   = (i % 2 == 0) ? 1f : -1f;
                if (i % 2 == 0) pt.x += Random.Range(-jScale, jScale);
                else             pt.z += sign * Random.Range(0f, jScale);
            }

            path.Add(pt);
        }

        return path;
    }

    // ═══════════════════════════════════════════
    //  Player Damage
    // ═══════════════════════════════════════════

    private void CheckPlayerHit(Vector3 strikePos)
    {
        var hits = Physics.OverlapSphere(strikePos, lightningDamageRadius,
                                         playerLayer, QueryTriggerInteraction.Ignore);
        foreach (var c in hits)
        {
            if (c && c.CompareTag(playerTag))
            {
                DamagePlayer(c.gameObject, lightningDamage);
                break;
            }
        }
    }

    /// <summary>
    /// Adapt this to your own health / damage system.
    /// Currently uses SendMessage as a flexible default.
    /// </summary>
    private void DamagePlayer(GameObject player, int damage)
    {
        player.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);

        // Direct component example:
        // var hp = player.GetComponent<PlayerHealth>();
        // if (hp) hp.TakeDamage(damage);
    }

    // ═══════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════

    private void HideFlashLight()
    {
        if (thunderFlashLight) thunderFlashLight.enabled = false;
    }

    // ═══════════════════════════════════════════
    //  Editor Context Menus
    // ═══════════════════════════════════════════
    [ContextMenu("Force Sunny")]      public void ForceSunny()     => SetWeather(WeatherState.Sunny);
    [ContextMenu("Force Light Rain")] public void ForceLightRain() => SetWeather(WeatherState.LightRain);
    [ContextMenu("Force Heavy Rain")] public void ForceHeavyRain() => SetWeather(WeatherState.HeavyRain);
}