using UnityEngine;

/// Prevents immediately re-triggering portals while you're still standing on them.
/// The portal will be ignored until you EXIT that trigger once.
public class PlayerPortalGuard : MonoBehaviour
{
    Collider ignoreUntilExit;

    public void IgnoreThisPortal(Collider c) => ignoreUntilExit = c;
    public bool IsIgnoring(Collider c) => ignoreUntilExit == c;

    void OnTriggerExit(Collider other)
    {
        if (other == ignoreUntilExit)
            ignoreUntilExit = null;
    }
}
