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

        // Delegate to GameSession so the loading screen and random tip are shown.
        // Falls back to a direct scene load if GameSession isn't present
        // (e.g. playing a scene standalone in the editor).
        if (GameSession.Instance != null)
        {
            GameSession.Instance.LoadScene(sceneToLoad);
        }
        else
        {
            SceneManager.LoadSceneAsync(sceneToLoad, LoadSceneMode.Single);
        }

        s_isLoading = false;
    }
}
