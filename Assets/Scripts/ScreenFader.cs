using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class ScreenFader : MonoBehaviour
{
    public static ScreenFader I;

    [Tooltip("Seconds for fade-out and fade-in each.")]
    public float fadeDuration = 0.25f;

    CanvasGroup group;

    void Awake()
    {
        I = this;
        group = GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
        gameObject.SetActive(true);
    }

    public IEnumerator FadeOutIn(Action duringBlack)
    {
        yield return FadeTo(1f);
        duringBlack?.Invoke();
        // wait a frame so the teleport position is fully applied
        yield return null;
        yield return FadeTo(0f);
    }

    IEnumerator FadeTo(float target)
    {
        group.blocksRaycasts = true;
        while (!Mathf.Approximately(group.alpha, target))
        {
            group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime / Mathf.Max(0.0001f, fadeDuration));
            yield return null;
        }
        if (Mathf.Approximately(target, 0f))
            group.blocksRaycasts = false;
    }
}
