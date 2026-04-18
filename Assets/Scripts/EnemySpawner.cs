using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("Spawning")]
    [Tooltip("Pool of enemy prefabs to pick from at random.")]
    public GameObject[] enemyPrefabs;

    [Tooltip("Base name given to each spawned enemy (no '(Clone)' suffix).")]
    public string enemyBaseName = "Enemy";

    [Tooltip("How many enemies should always be alive inside the zone.")]
    public int desiredCount = 3;

    [Header("Spawn Zone")]
    [Tooltip("Half-extents of the rectangular spawn area (local space).")]
    public Vector3 zoneSize = new Vector3(10f, 0f, 10f);

    [Header("Respawn Delay")]
    [Tooltip("Minimum seconds to wait before respawning a dead enemy.")]
    public float respawnDelayMin = 15f;

    [Tooltip("Maximum seconds to wait before respawning a dead enemy.")]
    public float respawnDelayMax = 30f;

    [Header("Debug")]
    [Tooltip("Color of the spawn zone gizmo in the Scene view.")]
    public Color gizmoColor = new Color(1f, 0.4f, 0f, 0.25f);

    // -----------------------------------------------------------------
    // Internals
    // -----------------------------------------------------------------

    // Tracks every enemy currently alive inside the zone.
    private readonly List<GameObject> _activeEnemies = new List<GameObject>();

    // Guards against launching multiple respawn coroutines for the same slot.
    private int _pendingSpawns = 0;

    // -----------------------------------------------------------------

    private void Start()
    {
        // Fill the zone immediately on start.
        for (int i = 0; i < desiredCount; i++)
            SpawnEnemy();
    }

    private void Update()
    {
        // Remove null entries — enemies destroyed by external scripts/abilities.
        int before = _activeEnemies.Count;
        _activeEnemies.RemoveAll(e => e == null);
        int destroyed = before - _activeEnemies.Count;

        // For every destroyed enemy that isn't already being respawned, kick off a coroutine.
        int deficit = (desiredCount - _activeEnemies.Count) - _pendingSpawns;
        for (int i = 0; i < deficit; i++)
            StartCoroutine(RespawnAfterDelay());
    }

    // -----------------------------------------------------------------
    // Spawning
    // -----------------------------------------------------------------

    private void SpawnEnemy()
    {
        if (enemyPrefabs == null || enemyPrefabs.Length == 0)
        {
            Debug.LogWarning("[EnemySpawner] No enemy prefabs assigned!", this);
            return;
        }

        GameObject prefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
        if (prefab == null)
        {
            Debug.LogWarning("[EnemySpawner] A null entry exists in the enemyPrefabs array.", this);
            return;
        }

        Vector3 spawnPos = RandomPointInZone();
        GameObject enemy = Instantiate(prefab, spawnPos, Quaternion.identity);

        // Rename BEFORE enabling NameplateSpawner, so the nameplate reads
        // the correct name instead of "Enemy(Clone)".
        enemy.name = enemyBaseName;

        // Now it's safe to let the nameplate component wake up.
        var nameplate = enemy.GetComponent<NameplateSpawner>();
        if (nameplate != null)
            nameplate.enabled = true;

        _activeEnemies.Add(enemy);
    }

    private IEnumerator RespawnAfterDelay()
    {
        _pendingSpawns++;

        float delay = Random.Range(respawnDelayMin, respawnDelayMax);
        yield return new WaitForSeconds(delay);

        _pendingSpawns--;
        SpawnEnemy();
    }

    // -----------------------------------------------------------------
    // Zone helpers
    // -----------------------------------------------------------------

    /// <summary>Returns a random world-space point inside the spawn zone.</summary>
    private Vector3 RandomPointInZone()
    {
        Vector3 local = new Vector3(
            Random.Range(-zoneSize.x, zoneSize.x),
            zoneSize.y,          // keeps enemies on the same Y plane as the spawner
            Random.Range(-zoneSize.z, zoneSize.z)
        );
        return transform.TransformPoint(local);
    }

    // -----------------------------------------------------------------
    // Public API (use from other scripts / player abilities if needed)
    // -----------------------------------------------------------------

    /// <summary>How many enemies are currently alive inside the zone.</summary>
    public int AliveCount => _activeEnemies.Count;

    // -----------------------------------------------------------------
    // Editor gizmos
    // -----------------------------------------------------------------

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // Filled semi-transparent box.
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color  = gizmoColor;
        Gizmos.DrawCube(Vector3.up * zoneSize.y, new Vector3(zoneSize.x * 2f, 0.05f, zoneSize.z * 2f));

        // Solid wireframe outline.
        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1f);
        Gizmos.DrawWireCube(Vector3.up * zoneSize.y, new Vector3(zoneSize.x * 2f, 0.05f, zoneSize.z * 2f));
    }
#endif
}
