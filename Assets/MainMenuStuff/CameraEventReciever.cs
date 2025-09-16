using UnityEngine;

public class CameraEventReceiver : MonoBehaviour
{
    [SerializeField] private MenuController menu;
    [SerializeField] private ClassSelectController classCtrl;
    [SerializeField] private RewindFX rewindFX;

    public void OnRewindStart() { if (rewindFX) rewindFX.Begin(); }
    public void OnRewindEnd()   { if (rewindFX) rewindFX.End();   }

    public void OnCameraArrived_ShowClassSelect()
    {
        if (menu) menu.OnCameraArrived_ShowClassSelect();
    }

    public void OnArrivedAtPick_ShowBack()
    {
        if (classCtrl) classCtrl.OnArrivedAtPick_ShowBack();
    }

    public void OnBackFinished_ShowSelect()
    {
        if (classCtrl) classCtrl.OnBackFinished_ShowSelect();
    }

    public void OnCreditsFinished_ShowMenu()
    {
        if (menu) menu.OnCreditsFinished_ShowMenu();
    }

    public void OnStartBackFinished_ShowMenu()
    {
        if (classCtrl) classCtrl.OnStartBackFinished_ShowMenu();
        // Or call menu.OnStartBackFinished_ShowMenu() if that is  addedto MenuController.
    }
}