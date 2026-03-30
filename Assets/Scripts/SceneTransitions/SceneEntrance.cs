// SceneEntrance.cs
// ─────────────────────────────────────────────────────────────────────────────
// ScriptableObject that describes where the player should appear when
// transitioning into a scene. One asset per "door" or entry point.
//
// SETUP
// ─────
//  1. Right-click → Create → RPG → Scene Entrance for each entry point.
//     E.g. "ForestEntrance_FromHighSchool", "Forest_DefaultEntrance"
//  2. Fill in sceneName and spawnPosition/spawnRotationY.
//  3. On the trigger that sends the player away, assign this asset to
//     LoadSceneWithEntrance.destination.
//  4. Place a SceneEntranceReceiver in the target scene and assign the
//     same asset — it will teleport the player there on load.
// ─────────────────────────────────────────────────────────────────────────────

using UnityEngine;

[CreateAssetMenu(menuName = "RPG/Scene Entrance", fileName = "NewEntrance")]
public class SceneEntrance : ScriptableObject
{
    [Tooltip("Exact scene name as it appears in Build Settings.")]
    public string sceneName;

    [Tooltip("World position the player should appear at.")]
    public Vector3 spawnPosition;

    [Tooltip("Y rotation the player should face on arrival.")]
    public float spawnRotationY = 0f;

    // ── Runtime flag ──────────────────────────────────────────────────────────
    // Set by LoadSceneWithEntrance before loading, cleared by SceneEntranceReceiver.
    [System.NonSerialized] public bool pendingUse = false;
}
