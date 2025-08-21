using UnityEngine;
using System.Collections;

[DisallowMultipleComponent]
public class CameraShaker : MonoBehaviour
{
    public static CameraShaker Instance;

    [Header("Defaults")]
    public float defaultDuration  = 0.12f;
    public float defaultAmplitude = 0.25f;

    CameraFollow follow;
    Coroutine co;

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Prefer the component on the same GO; otherwise find one.
        follow = GetComponent<CameraFollow>();
        if (!follow) follow = FindAnyObjectByType<CameraFollow>();
    }

    public void Shake(float duration, float amplitude)
    {
        if (!follow) return;
        if (co != null) StopCoroutine(co);
        co = StartCoroutine(DoShake(
            duration  > 0f ? duration  : defaultDuration,
            amplitude > 0f ? amplitude : defaultAmplitude));
    }

    IEnumerator DoShake(float duration, float amplitude)
    {
        Vector3 original = follow.extraOffset;
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float damper = 1f - Mathf.Clamp01(t / duration); // fade out
            follow.extraOffset = new Vector3(
                (Random.value * 2f - 1f) * amplitude * damper,
                (Random.value * 2f - 1f) * amplitude * damper,
                0f);
            yield return null;
        }

        follow.extraOffset = original;
        co = null;
    }
}
