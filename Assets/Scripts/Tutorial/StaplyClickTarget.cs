using UnityEngine;

public class StaplyClickTarget : MonoBehaviour {
    public StaplyManager manager;
    void OnMouseDown(){ manager?.OnStaplyClicked(); }
}