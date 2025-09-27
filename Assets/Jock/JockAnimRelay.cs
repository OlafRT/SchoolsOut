using UnityEngine;

/// <summary>
/// Relay for Jock animation events during "smash the box" flow.
/// Hook animation events to these public methods.
/// </summary>
public class JockAnimRelay : MonoBehaviour
{
    // Animation Events (call these from your Jock animation timeline)
    // 1) When the weapon/fist hits the box
    public void AnimEvent_JockSmashImpact()
    {
        HackingStation.NotifyJockSmashImpact();
    }

    // 2) When electricity should start (spark FX on box)
    public void AnimEvent_JockEnableShockFX()
    {
        HackingStation.NotifyJockEnableShockFX();
    }

    // 3) When the Jock should enter "shocked" state (play shock anim trigger)
    public void AnimEvent_JockShock()
    {
        HackingStation.NotifyJockShock();
    }

    // 4) When the Jock should fall down (play fall anim trigger)
    public void AnimEvent_JockFall()
    {
        HackingStation.NotifyJockFall();
    }

    // Optional: when to disable FX at the end
    public void AnimEvent_JockDisableShockFX()
    {
        HackingStation.NotifyJockDisableShockFX();
    }
}
