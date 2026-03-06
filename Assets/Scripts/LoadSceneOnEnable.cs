using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class LoadSceneOnEnable : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private string sceneToLoad = "Bicycle";
    [SerializeField] private bool deactivateSelfAfterKickoff = true; // optional
    [SerializeField] private float optionalDelay = 0f;               // optional delay

    // Guards to prevent double-loads (e.g., if multiple objects enable in the same frame)
    private static bool s_isLoading = false;
    private bool _triggered = false;

    private void OnEnable()
    {
        // Reset the static flag — it persists across scene loads so we must
        // clear it at the start of each new trigger, not rely on the scene
        // change to do it (Unity statics survive scene transitions).
        s_isLoading = false;
        _triggered = false;

        StartCoroutine(LoadNext());
    }

    private System.Collections.IEnumerator LoadNext()
    {
        s_isLoading = true;

        if (optionalDelay > 0f)
            yield return new WaitForSeconds(optionalDelay);

        if (deactivateSelfAfterKickoff)
            gameObject.SetActive(false); // prevents any re-entry loops

        // Load the Bicycle scene
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneToLoad, LoadSceneMode.Single);
        // (Let it auto-activate; no need to set allowSceneActivation manually here.)
        yield return op;

        // After this point we’re in the new scene.
        // No further action needed.
        s_isLoading = false; // harmless; scene change resets most contexts anyway
    }
}