// LoadSceneWithEntrance.cs
// ─────────────────────────────────────────────────────────────────────────────
// Drop on any trigger collider. When the player walks in, loads the scene
// described by the assigned SceneEntrance asset and spawns the player at
// the position stored in that asset.
//
// Also works as a direct replacement for LoadSceneOnEnable — just enable
// this GameObject to trigger the load (or use the trigger collider).
// ─────────────────────────────────────────────────────────────────────────────

using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

[DisallowMultipleComponent]
public class LoadSceneWithEntrance : MonoBehaviour
{
    [Tooltip("Where to go and where to spawn. Create via Right-click → RPG → Scene Entrance.")]
    public SceneEntrance destination;

    [Tooltip("If true, fires on OnEnable (like LoadSceneOnEnable). " +
             "If false, fires only when the player walks into the trigger collider.")]
    public bool triggerOnEnable = false;

    [Tooltip("Optional delay before the scene loads (seconds).")]
    public float delay = 0f;

    // Guards
    private static bool s_isLoading = false;
    private bool _triggered = false;

    void OnEnable()
    {
        if (!triggerOnEnable) return;
        TryLoad();
    }

    void OnDisable()
    {
        _triggered = false;
    }

    void OnTriggerEnter(Collider other)
    {
        if (triggerOnEnable) return;  // not in trigger mode
        if (!other.CompareTag("Player")) return;
        TryLoad();
    }

    void TryLoad()
    {
        if (s_isLoading || _triggered) return;
        if (!destination)
        {
            Debug.LogError($"[LoadSceneWithEntrance] No destination assigned on '{name}'.");
            return;
        }

        _triggered  = true;
        s_isLoading = true;
        StartCoroutine(LoadRoutine());
    }

    IEnumerator LoadRoutine()
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        // Mark the entrance as pending so SceneEntranceReceiver knows to use it
        destination.pendingUse = true;

        gameObject.SetActive(false); // prevent re-entry

        // Reset the lock BEFORE loading. LoadSceneMode.Single destroys every
        // GameObject in the current scene — including this one — which kills
        // the coroutine before the line after yield return ever executes.
        // If we don't reset here, s_isLoading stays true forever and all
        // subsequent scene transitions silently fail.
        s_isLoading = false;

        SceneManager.LoadSceneAsync(destination.sceneName, LoadSceneMode.Single);
    }
}
