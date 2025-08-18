using UnityEngine;

public class ActionBarUI : MonoBehaviour
{
    [Tooltip("Assign 5 slots in the order they should appear.")]
    public AbilitySlotUI[] slots = new AbilitySlotUI[5];

    // Optional: helper to inject abilities at runtime
    public void SetSlot(int index, MonoBehaviour abilityComponent)
    {
        if (index < 0 || index >= slots.Length) return;
        slots[index].abilityComponent = abilityComponent;
    }
}
