using UnityEngine;

public class StaplyPrefsResetter : MonoBehaviour {
    [Tooltip("Clear Staply_Seen at scene start (turn this off after one run).")]
    public bool resetOnStart = false;

    [Tooltip("Press this during Play Mode to clear the flag.")]
    public KeyCode hotkey = KeyCode.F10;

    public void ResetSeen(){
        PlayerPrefs.DeleteKey("Staply_Seen");
        PlayerPrefs.Save();
        Debug.Log("Staply: cleared PlayerPrefs key 'Staply_Seen'.");
    }

    void Start(){ if (resetOnStart) ResetSeen(); }
    void Update(){ if (Input.GetKeyDown(hotkey)) ResetSeen(); }
}