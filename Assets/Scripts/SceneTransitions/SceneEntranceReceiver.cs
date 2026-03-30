// SceneEntranceReceiver.cs
// ─────────────────────────────────────────────────────────────────────────────
// Place one of these in each scene for each possible entry point.
// When the scene loads, it checks if its SceneEntrance asset is marked as
// pending and teleports the player there if so.
//
// SETUP
// ─────
//  1. Create an empty GameObject in the scene, name it e.g. "EntranceFromHighSchool"
//  2. Add this component
//  3. Assign the matching SceneEntrance asset (same one used by LoadSceneWithEntrance)
//  4. The spawn position is stored in the asset — but you can also just
//     position this GameObject where you want the spawn point to be and
//     tick "use this transform as spawn" below for easy visual placement.
// ─────────────────────────────────────────────────────────────────────────────

using UnityEngine;
using System.Collections;

public class SceneEntranceReceiver : MonoBehaviour
{
    [Tooltip("The SceneEntrance asset this receiver responds to.")]
    public SceneEntrance entrance;

    [Tooltip("If ticked, uses this GameObject's position/rotation instead of " +
             "the values stored in the asset. Easier to place visually in the scene.")]
    public bool useThisTransformAsSpawn = true;

    IEnumerator Start()
    {
        if (!entrance || !entrance.pendingUse) yield break;

        // Wait one frame so the scene is fully ready
        yield return null;

        entrance.pendingUse = false;

        var playerGO = GameObject.FindWithTag("Player");
        if (!playerGO)
        {
            Debug.LogWarning("[SceneEntranceReceiver] No Player tag found — cannot teleport.");
            yield break;
        }

        Vector3 targetPos = useThisTransformAsSpawn
            ? transform.position
            : entrance.spawnPosition;

        float targetRotY = useThisTransformAsSpawn
            ? transform.eulerAngles.y
            : entrance.spawnRotationY;

        playerGO.transform.SetPositionAndRotation(
            targetPos,
            Quaternion.Euler(0f, targetRotY, 0f));

        Debug.Log($"[SceneEntranceReceiver] Teleported player to '{entrance.name}' at {targetPos}");
    }
}
