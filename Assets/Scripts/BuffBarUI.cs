using UnityEngine;

/// <summary>
/// Drives a row of up to 5 BuffIconUI slots from PlayerTimedBuffs.
/// Point it at the player GameObject and assign your 5 BuffIconUI slots.
///
/// Setup:
///   1. Create a panel in your UI Canvas (e.g. "BuffBar").
///   2. Add 5 child slot GameObjects, each with the BuffIconUI prefab structure
///      (icon Image, overlay Image set to Filled/Vertical, optional TMP timer text).
///   3. Add this component to the panel and wire up the 5 slots + playerRoot.
/// </summary>
public class BuffBarUI : MonoBehaviour
{
    [Tooltip("The player GameObject that has PlayerTimedBuffs on it.")]
    public GameObject playerRoot;

    [Tooltip("Assign exactly 5 BuffIconUI slots in order.")]
    public BuffIconUI[] slots = new BuffIconUI[5];

    /// <summary>
    /// Optional: one icon sprite per BuffStat so the slot shows the right image.
    /// Leave entries null and the slot will show a blank icon.
    /// Order matches BuffStat enum: Muscles, IQ, Toughness, CritChance.
    /// </summary>
    [Header("Buff Icons (match BuffStat enum order)")]
    public Sprite musclesIcon;
    public Sprite iqIcon;
    public Sprite toughnessIcon;
    public Sprite critChanceIcon;

    PlayerTimedBuffs timedBuffs;

    void Awake()
    {
        if (playerRoot) timedBuffs = playerRoot.GetComponent<PlayerTimedBuffs>();

        // Hide all slots at start
        foreach (var s in slots) if (s) s.gameObject.SetActive(false);
    }

    void Update()
    {
        // Lazy-find if player spawns after this UI
        if (!timedBuffs && playerRoot)
            timedBuffs = playerRoot.GetComponent<PlayerTimedBuffs>();

        int activeCount = timedBuffs != null ? timedBuffs.ActiveCount : 0;

        for (int i = 0; i < slots.Length; i++)
        {
            if (!slots[i]) continue;

            if (i < activeCount)
            {
                var info = timedBuffs.GetInfo(i);

                slots[i].gameObject.SetActive(true);
                slots[i].Set(IconForStat(info.stat));
                slots[i].SetTime(info.remaining, info.duration);
            }
            else
            {
                slots[i].gameObject.SetActive(false);
            }
        }
    }

    Sprite IconForStat(BuffStat stat) => stat switch
    {
        BuffStat.Muscles    => musclesIcon,
        BuffStat.IQ         => iqIcon,
        BuffStat.Toughness  => toughnessIcon,
        BuffStat.CritChance => critChanceIcon,
        _                   => null
    };
}