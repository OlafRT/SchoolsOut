// TutorialEvents.cs
using System;
public static class TutorialEvents {
    public static event Action Moved, Aimed, Sprint, Interact, OpenEquipment, PickedUpWeapon,
                               ToggleAutoAttack, OpenInventory, OpenQuestLog, EquippedItem;
    public static void RaiseMoved() => Moved?.Invoke();
    public static void RaiseAimed() => Aimed?.Invoke();
    public static void RaiseSprint() => Sprint?.Invoke();
    public static void RaiseInteract() => Interact?.Invoke();
    public static void RaiseOpenEquipment() => OpenEquipment?.Invoke();
    public static void RaisePickedUpWeapon() => PickedUpWeapon?.Invoke();
    public static void RaiseToggleAutoAttack() => ToggleAutoAttack?.Invoke();
    public static void RaiseOpenInventory() => OpenInventory?.Invoke();
    public static void RaiseOpenQuestLog() => OpenQuestLog?.Invoke();
    public static void RaiseEquippedItem() => EquippedItem?.Invoke();
}
