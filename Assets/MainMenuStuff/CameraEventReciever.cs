using UnityEngine;

public class CameraEventReceiver : MonoBehaviour
{
    [SerializeField] private MenuController menu;
    [SerializeField] private ClassSelectController classCtrl;

    public void OnCameraArrived_ShowClassSelect()
    {
        if (menu) menu.OnCameraArrived_ShowClassSelect();
    }

    public void OnArrivedAtPick_ShowBack()
    {
        if (classCtrl) classCtrl.OnArrivedAtPick_ShowBack();
    }

    // NEW: call this at the very end of BackFromNerd / BackFromJock clips
    public void OnBackFinished_ShowSelect()
    {
        if (classCtrl) classCtrl.OnBackFinished_ShowSelect();
    }
}