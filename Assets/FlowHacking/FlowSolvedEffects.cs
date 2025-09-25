// FlowSolvedEffects.cs
using System.Collections;
using UnityEngine;

public class FlowSolvedEffects : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip solvedClip;
    [SerializeField] private RectTransform shakeTarget;   // the board root RectTransform
    [SerializeField] private GameObject uiRootToHide;     // hide this after shake

    [Header("Shake")]
    [SerializeField] private float shakeDuration = 0.2f;
    [SerializeField] private float shakeStrength = 6f;

    public void OnSolved()  // hook this to FlowBoard.onSolved
    {
        if (solvedClip && sfxSource) sfxSource.PlayOneShot(solvedClip);
        StartCoroutine(DoShakeThenHide());
    }

    private IEnumerator DoShakeThenHide()
    {
        if (shakeTarget)
        {
            Vector3 basePos = shakeTarget.anchoredPosition;
            float t = 0f;
            while (t < shakeDuration)
            {
                t += Time.deltaTime;
                float s = (1f - t / shakeDuration) * shakeStrength;
                shakeTarget.anchoredPosition = basePos + (Vector3)(Random.insideUnitCircle * s);
                yield return null;
            }
            shakeTarget.anchoredPosition = basePos;
        }
        if (uiRootToHide) uiRootToHide.SetActive(false);
    }
}
