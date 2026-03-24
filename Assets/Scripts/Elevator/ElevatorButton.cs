using UnityEngine;

/// <summary>
/// Place on both the exterior call button and the interior panel button.
/// Uses the same proximity-detection pattern as DialogueInteractable — your
/// existing PlayerInteractionController should pick this up automatically if
/// it scans for MonoBehaviours in range (the same way it finds DialogueInteractable).
///
/// If your interaction controller uses an IInteractable interface, add it here:
///   public class ElevatorButton : MonoBehaviour, IInteractable
/// and implement whatever members the interface requires.
/// </summary>
public class ElevatorButton : MonoBehaviour
{
    public enum ButtonRole
    {
        CallButton,   // outside — opens exterior doors
        PanelButton,  // inside  — starts the ride
    }

    [Header("Role")]
    public ButtonRole role = ButtonRole.CallButton;

    [Header("Refs")]
    public ElevatorController elevator;

    [Header("Interaction Prompt")]
    [Tooltip("Label shown in your Space-prompt UI. Leave blank to use the default.")]
    public string promptLabel = "Call Elevator";

    // ── Called by your PlayerInteractionController when the player presses Space ──

    /// <summary>
    /// Rename / adjust the signature to match whatever your interaction
    /// controller calls on interactables (e.g. Interact(), OnInteract(), etc.).
    /// </summary>
    public void Interact()
    {
        if (!elevator) return;

        switch (role)
        {
            case ButtonRole.CallButton:
                elevator.OpenExteriorDoors();
                break;

            case ButtonRole.PanelButton:
                elevator.StartRide();
                break;
        }
    }

    // ── Gizmo so you can see the button's role in the Scene view ─────────────

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 0.3f,
            $"[{role}]",
            new GUIStyle { normal = { textColor = Color.cyan }, fontSize = 11 }
        );
    }
#endif
}
