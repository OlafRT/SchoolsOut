using UnityEngine;
using System.Collections.Generic;

public class CombatTextManager : MonoBehaviour
{
    public static CombatTextManager Instance;

    [Header("Damage Number Prefab")]
    public DamageNumber damageNumberPrefab;

    [Header("Pool Settings")]
    public int initialPoolSize = 16;

    private readonly Queue<DamageNumber> pool = new();

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (damageNumberPrefab)
        {
            for (int i = 0; i < initialPoolSize; i++)
                pool.Enqueue(CreateNew());
        }
    }

    DamageNumber CreateNew()
    {
        var obj = Instantiate(damageNumberPrefab, transform);
        obj.gameObject.SetActive(false);
        return obj;
    }

    /// <summary>
    /// Show a damage number at worldPos. If follow != null, it tracks that transform (e.g., NPC).
    /// </summary>
    public void ShowDamage(Vector3 worldPos, int amount, bool crit, Transform follow = null, float lifetimeOverride = -1f)
    {
        if (!damageNumberPrefab) return;

        DamageNumber dn = pool.Count > 0 ? pool.Dequeue() : CreateNew();
        dn.transform.SetParent(transform, worldPositionStays: true);
        dn.gameObject.SetActive(true);
        dn.Init(worldPos, amount, crit, follow, lifetimeOverride);
    }

    /// <summary>
    /// NEW: Show arbitrary text (e.g., "BLOCK") with color and optional size.
    /// Keeps pooling identical to damage numbers.
    /// </summary>
    public void ShowText(Vector3 worldPos, string text, Color color, Transform follow = null, float lifetimeOverride = -1f, float fontSize = 32f)
    {
        if (!damageNumberPrefab) return;

        DamageNumber dn = pool.Count > 0 ? pool.Dequeue() : CreateNew();
        dn.transform.SetParent(transform, worldPositionStays: true);
        dn.gameObject.SetActive(true);
        dn.InitText(worldPos, text, color, follow, lifetimeOverride, fontSize);
    }

    public void ReturnToPool(DamageNumber dn)
    {
        dn.gameObject.SetActive(false);
        dn.transform.SetParent(transform, worldPositionStays: false);
        pool.Enqueue(dn);
    }
}
