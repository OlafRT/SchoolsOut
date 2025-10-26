using UnityEngine;

public class DialogueDetector : MonoBehaviour
{
    [Header("Grid")]
    public float tileSize = 1f;
    [Tooltip("How forgiving the tile probe is. 0.5–0.6 is good.")]
    public float detectRadius = 0.55f;

    [Header("Filtering")]
    [Tooltip("Only these layers are considered for dialogue hits.")]
    public LayerMask dialogueLayers;
    [Tooltip("If true and no Dialogue layer is found, logs a warning once.")]
    public bool requireDialogueLayer = true;

    [Header("Refs")]
    public DialogueController controller;
    public PlayerMovement player;

    bool layerWarned = false;

    void Awake()
    {
        // If mask not set in Inspector, default to a layer named "Dialogue"
        if (dialogueLayers.value == 0)
        {
            int dl = LayerMask.NameToLayer("Dialogue");
            if (dl >= 0) dialogueLayers = 1 << dl;
            else if (requireDialogueLayer && !layerWarned)
            {
                layerWarned = true;
                Debug.LogWarning("[DialogueDetector] No 'Dialogue' layer found. " +
                                 "Either create that layer or set DialogueLayers in the Inspector.");
                dialogueLayers = ~0; // fallback: search all layers
            }
        }
    }

    void Reset()
    {
        player = GetComponent<PlayerMovement>();
        controller = FindObjectOfType<DialogueController>();
    }

    void Update()
    {
        if (!player || !controller) return;

        // Advance when open
        if (controller.IsDialogueOpen)
        {
            if (Input.GetKeyDown(KeyCode.Space)) controller.Advance();
            return;
        }

        if (!player.canMove)
        {
            controller.ShowPrompt(false);
            return;
        }

        // Probe the tile directly in front (8-way snap like your movement)
        Vector3 fwd8 = SnapDirTo8(transform.forward);
        if (fwd8 == Vector3.zero) fwd8 = transform.forward.normalized;

        Vector3 tileCenter = RoundToTile(transform.position + fwd8 * tileSize);

        var hits = Physics.OverlapSphere(
            tileCenter + Vector3.up * 0.05f,
            detectRadius,
            dialogueLayers.value == 0 ? ~0 : dialogueLayers, // fallback if unset
            QueryTriggerInteraction.Collide
        );

        DialogueInteractable talk = null;
        foreach (var h in hits)
        {
            // Only consider colliders actually on allowed layers (extra safety)
            if ((dialogueLayers.value != 0) &&
                ((dialogueLayers.value & (1 << h.gameObject.layer)) == 0))
                continue;

            talk = h.GetComponentInParent<DialogueInteractable>();
            if (!talk) talk = h.GetComponentInChildren<DialogueInteractable>();
            if (talk) break;
        }

        controller.ShowPrompt(talk != null);

        if (talk && Input.GetKeyDown(KeyCode.Space))
            controller.Begin(talk);
    }

    // --- helpers ---
    static Vector3 RoundToTile(Vector3 p, float size = 1f)
        => new Vector3(Mathf.Round(p.x / size) * size, p.y, Mathf.Round(p.z / size) * size);

    Vector3 RoundToTile(Vector3 p) => RoundToTile(p, tileSize);

    static Vector3 SnapDirTo8(Vector3 v)
    {
        v.y = 0f;
        if (v.sqrMagnitude < 0.0001f) return Vector3.zero;
        float ang = Mathf.Atan2(v.z, v.x) * Mathf.Rad2Deg; if (ang < 0f) ang += 360f;
        int step = Mathf.RoundToInt(ang / 45f) % 8;
        switch (step)
        {
            case 0:  return new Vector3( 1,0, 0);
            case 1:  return new Vector3( 1,0, 1).normalized;
            case 2:  return new Vector3( 0,0, 1);
            case 3:  return new Vector3(-1,0, 1).normalized;
            case 4:  return new Vector3(-1,0, 0);
            case 5:  return new Vector3(-1,0,-1).normalized;
            case 6:  return new Vector3( 0,0,-1);
            default: return new Vector3( 1,0,-1).normalized;
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!player) return;
        Vector3 fwd8 = SnapDirTo8(transform.forward);
        if (fwd8 == Vector3.zero) fwd8 = transform.forward.normalized;
        Vector3 tileCenter = RoundToTile(transform.position + fwd8 * tileSize);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(tileCenter + Vector3.up * 0.05f, detectRadius);
    }
#endif
}
