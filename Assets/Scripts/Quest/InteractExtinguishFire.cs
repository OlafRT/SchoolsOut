using UnityEngine;

public class InteractExtinguishFire : MonoBehaviour
{
    [Header("Refs")]
    public Inventory playerInventory;
    public ScreenToast toast;         // optional
    public AudioSource sfx;           // optional
    public ParticleSystem extinguishVfx;   // play once
    public ParticleSystem fireLoop;        // the fire to stop/disable

    [Header("Items")]
    public ItemTemplate filledBucket;
    public ItemTemplate emptyBucket;

    [Header("Quest")]
    public string questCollectId = "fire_extinguished"; // “Collect” 1 per fire

    bool _done;

    // Call from dialogue line “Use water” or an interact key
    public void TryExtinguish()
    {
        if (_done) return;
        if (!playerInventory || !filledBucket || !emptyBucket) return;

        if (!playerInventory.HasAtLeast(filledBucket, 1))
        {
            toast?.Show("I need a filled bucket.", Color.yellow);
            return;
        }

        // Consume the water
        playerInventory.RemoveItems(filledBucket, 1);

        // Return an empty bucket (stackable or not as per template)
        playerInventory.Add(new ItemInstance {
            template      = emptyBucket,
            itemLevel     = 1,
            requiredLevel = 1,
            rarity        = emptyBucket.rarity,
            value         = emptyBucket.fixedValue
        }, 1);

        // FX
        if (extinguishVfx) { extinguishVfx.gameObject.SetActive(true); extinguishVfx.Play(); }
        if (fireLoop) { fireLoop.Stop(true, ParticleSystemStopBehavior.StopEmitting); fireLoop.gameObject.SetActive(false); }
        if (sfx) sfx.Play();

        // Quest progress: reuse collect channel for “fires extinguished”
        QuestEvents.ItemLooted?.Invoke(questCollectId, 1); // bumps matching Collect objectives. :contentReference[oaicite:3]{index=3}

        _done = true; // prevent double-use on this fire
    }
}
