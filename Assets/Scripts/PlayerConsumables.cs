using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerConsumables : MonoBehaviour
{
    public PlayerHealth health;
    public PlayerStats stats;
    public PlayerAbilities abilities;
    public ActionBarAutoBinder actionBarBinder; // optional (drag in)

    [Header("Food")]
    public Animator animator;
    public string sitBool = "IsSitting";
    public float cancelMoveDistance = 0.15f;
    [Header("Hand Prop (Enable existing)")]
    PlayerPropRegistry propRegistry;
    GameObject activeFoodProp;

    Coroutine eatingRoutine;

    void Awake()
    {
        if (!health) health = GetComponent<PlayerHealth>();
        if (!stats) stats = GetComponent<PlayerStats>();
        if (!abilities) abilities = GetComponent<PlayerAbilities>();
        propRegistry = GetComponent<PlayerPropRegistry>();
    }

    public bool TryUseFromInventory(Inventory inv, int bagIndex)
    {
        if (inv == null) return false;
        if (bagIndex < 0 || bagIndex >= inv.Slots.Count) return false;

        var stack = inv.Slots[bagIndex];
        if (stack.IsEmpty || stack.item?.template == null) return false;

        var inst = stack.item;
        var tpl = inst.template;

        if (!tpl.isConsumable || tpl.consumableKind == ConsumableKind.None)
            return false;

        // optional SFX
        if (tpl.useSfx)
            AudioSource.PlayClipAtPoint(tpl.useSfx, transform.position, tpl.useSfxVolume);

        switch (tpl.consumableKind)
        {
            case ConsumableKind.Healing:
                return UseHealing(inv, bagIndex, tpl);

            case ConsumableKind.Food:
                return UseFood(inv, bagIndex, tpl);

            case ConsumableKind.Buff:
                return UseBuff(inv, bagIndex, tpl);

            case ConsumableKind.TeachSpell:
                return UseTeachSpell(inv, bagIndex, tpl);

            case ConsumableKind.Utility:
                // reserve for later
                return false;
        }

        return false;
    }

    bool UseHealing(Inventory inv, int bagIndex, ItemTemplate tpl)
    {
        if (!health) return false;

        health.Heal(Mathf.Max(0, tpl.healAmount));
        inv.RemoveAt(bagIndex, 1);
        return true;
    }

    bool UseFood(Inventory inv, int bagIndex, ItemTemplate tpl)
    {
        if (!health) return false;

        // cancel previous
        if (eatingRoutine != null)
        {
            StopCoroutine(eatingRoutine);
            eatingRoutine = null;
            SetSitting(false);
        }

        if (tpl.foodConsumeOnStart)
            inv.RemoveAt(bagIndex, 1);

        eatingRoutine = StartCoroutine(EatRoutine(inv, bagIndex, tpl));
        return true;
    }

    IEnumerator EatRoutine(Inventory inv, int bagIndex, ItemTemplate tpl)
    {
        SetSitting(true);
        ShowFoodProp(tpl);

        var pm = GetComponent<PlayerMovement>();   // <-- added

        Vector3 startPos = transform.position;
        float dur = Mathf.Max(0.1f, tpl.foodDurationSeconds);
        int totalHeal = Mathf.Max(0, tpl.foodTotalHeal);

        float t = 0f;
        float healPerSecond = totalHeal / dur;

        while (t < dur)
        {
            // cancel if player is trying to move (WoW-style)
            if (pm && pm.HasMoveInput)
            {
                SetSitting(false);
                HideFoodProp();
                eatingRoutine = null;
                yield break;
            }

            // cancel if actually moved (backup safety)
            if ((transform.position - startPos).sqrMagnitude > cancelMoveDistance * cancelMoveDistance)
            {
                SetSitting(false);
                eatingRoutine = null;
                HideFoodProp();
                yield break;
            }

            float dt = Time.deltaTime;
            t += dt;

            float f = healPerSecond * dt;
            int whole = Mathf.FloorToInt(f);
            float frac = f - whole;

            if (whole > 0) health.Heal(whole);
            if (frac > 0f && Random.value < frac) health.Heal(1);

            yield return null;
        }

        if (!tpl.foodConsumeOnStart)
            inv.RemoveAt(bagIndex, 1);

        SetSitting(false);
        HideFoodProp();
        eatingRoutine = null;
    }

    bool UseBuff(Inventory inv, int bagIndex, ItemTemplate tpl)
    {
        var buffs = GetComponent<PlayerTimedBuffs>();
        if (!buffs) buffs = gameObject.AddComponent<PlayerTimedBuffs>();

        buffs.ApplyBuff(tpl.buffStat, tpl.buffAmount, tpl.buffDurationSeconds);
        inv.RemoveAt(bagIndex, 1);
        return true;
    }

    bool UseTeachSpell(Inventory inv, int bagIndex, ItemTemplate tpl)
    {
        if (abilities == null) return false;

        string abil = tpl.teachesAbilityName;
        if (string.IsNullOrEmpty(abil)) return false;

        if (!abilities.learnedAbilities.Contains(abil))
            abilities.learnedAbilities.Add(abil);

        // refresh action bar (if present)
        if (!actionBarBinder) actionBarBinder = FindObjectOfType<ActionBarAutoBinder>(true);
        if (actionBarBinder) actionBarBinder.Refresh();

        inv.RemoveAt(bagIndex, 1);
        return true;
    }

    void ShowFoodProp(ItemTemplate tpl)
    {
        HideFoodProp();

        if (!propRegistry || tpl == null) return;
        if (string.IsNullOrEmpty(tpl.foodPropId)) return;

        if (propRegistry.TryGet(tpl.foodPropId, out var go))
        {
            activeFoodProp = go;
            activeFoodProp.SetActive(true);
        }
    }

    void HideFoodProp()
    {
        if (activeFoodProp)
        {
            activeFoodProp.SetActive(false);
            activeFoodProp = null;
        }
    }

    void SetSitting(bool on)
    {
        if (!animator || string.IsNullOrEmpty(sitBool)) return;
        animator.SetBool(sitBool, on);
    }
}