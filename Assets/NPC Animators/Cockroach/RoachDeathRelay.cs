using UnityEngine;

[DisallowMultipleComponent]
public class RoachDeathRelay : MonoBehaviour
{
    public RoachSkitter roach;   // assign in Inspector (child roach)
    NPCHealth hp;
    bool fired;

    void Awake()
    {
        if (!roach) roach = GetComponentInChildren<RoachSkitter>();
        hp = GetComponentInParent<NPCHealth>(); // read-only
    }

    void Update()
    {
        if (fired || !hp || !roach) return;
        // NPCHealth exposes public currentHP; 0 means dead
        if (hp.currentHP <= 0)
        {
            fired = true;
            roach.Die();
        }
    }
}
