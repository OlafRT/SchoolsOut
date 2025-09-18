using UnityEngine;

[DefaultExecutionOrder(-100)] // run very early
public class PlayerBootstrap : MonoBehaviour
{
    [Header("Class-driven models")]
    [Tooltip("Parent under which the model lives/spawns. Defaults to this transform.")]
    [SerializeField] private Transform modelParent;

    [Tooltip("If you already have both models as children, assign them here.")]
    [SerializeField] private GameObject nerdModelChild;
    [SerializeField] private GameObject jockModelChild;

    [Tooltip("Optional alternative: spawn one of these prefabs instead of using child objects.")]
    [SerializeField] private GameObject nerdModelPrefab;
    [SerializeField] private GameObject jockModelPrefab;

    [Header("Animator (optional)")]
    [Tooltip("If you want different controllers per class, assign them here.")]
    [SerializeField] private Animator animatorOnRoot;
    [SerializeField] private RuntimeAnimatorController nerdController;
    [SerializeField] private RuntimeAnimatorController jockController;

    void Awake()
    {
        var sess = GameSession.Instance;
        var pick = (sess != null) ? sess.SelectedClass : GameSession.ClassType.Nerd;

        if (!modelParent) modelParent = transform;

        // Swap model first (so components on the model are present if something reads them in Awake)
        SwapModel(pick);

        // Apply class to PlayerStats / PlayerAbilities on the root
        var stats = GetComponent<PlayerStats>();
        if (stats)
        {
            stats.playerClass = (pick == GameSession.ClassType.Nerd)
                ? PlayerStats.PlayerClass.Nerd
                : PlayerStats.PlayerClass.Jock;
        }

        var abilities = GetComponent<PlayerAbilities>();
        if (abilities)
        {
            abilities.playerClass = (pick == GameSession.ClassType.Nerd)
                ? PlayerAbilities.PlayerClass.Nerd
                : PlayerAbilities.PlayerClass.Jock;

            // keep level in sync with stats if desired
            if (stats) abilities.playerLevel = stats.level;
        }

        // Optional animator controller swap on the root
        if (animatorOnRoot)
        {
            var ctrl = (pick == GameSession.ClassType.Nerd) ? nerdController : jockController;
            if (ctrl) animatorOnRoot.runtimeAnimatorController = ctrl;
        }
    }

    void SwapModel(GameSession.ClassType pick)
    {
        GameObject active = null, inactive = null;

        // Prefer existing child models if assigned
        if (nerdModelChild || jockModelChild)
        {
            active   = (pick == GameSession.ClassType.Nerd) ? nerdModelChild : jockModelChild;
            inactive = (pick == GameSession.ClassType.Nerd) ? jockModelChild : nerdModelChild;

            if (inactive) inactive.SetActive(false);
            if (active)   active.SetActive(true);
        }
        // Otherwise, instantiate prefabs if provided
        else if (nerdModelPrefab || jockModelPrefab)
        {
            var prefab = (pick == GameSession.ClassType.Nerd) ? nerdModelPrefab : jockModelPrefab;
            if (prefab)
            {
                // optionally clear any previous children
                for (int i = modelParent.childCount - 1; i >= 0; i--)
                    Destroy(modelParent.GetChild(i).gameObject);

                var instance = Instantiate(prefab, modelParent);
                instance.name = prefab.name;
            }
        }
        // else: nothing to swap—assume your base player is already the correct model
    }
}
