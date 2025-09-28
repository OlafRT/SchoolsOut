using UnityEngine;
using TMPro;
using System.Collections;

public class RandomVoiceLineWithSubtitle : MonoBehaviour
{
    [System.Serializable]
    public class Line
    {
        public AudioClip clip;
        [TextArea] public string subtitle;
    }

    [Header("Content")]
    public Line[] lines;

    [Header("Refs")]
    public AudioSource audioSource;     // 2D or 3D is fine
    public TMP_Text subtitleText;       // e.g. a child TMP text

    [Header("Timing")]
    [Tooltip("Keep subtitle on-screen this long after the audio ends.")]
    public float extraHoldSeconds = 1f;
    [Tooltip("Fade-out duration for subtitle.")]
    public float fadeDuration = 0.5f;

    [Header("Behavior")]
    [Tooltip("Pick & play as soon as the object is enabled.")]
    public bool playOnEnable = true;

    void Awake()
    {
        // Light auto-setup if fields are missing
        if (!audioSource) audioSource = GetComponent<AudioSource>();
        if (!subtitleText) subtitleText = GetComponentInChildren<TMP_Text>(true);
        if (subtitleText) subtitleText.gameObject.SetActive(false);
    }

    void OnEnable()
    {
        if (playOnEnable) PlayRandom();
    }

    public void PlayRandom()
    {
        if (lines == null || lines.Length == 0) { Debug.LogWarning($"{name}: No lines assigned."); return; }
        var chosen = lines[Random.Range(0, lines.Length)];
        if (!chosen.clip || !audioSource || !subtitleText)
        {
            Debug.LogWarning($"{name}: Missing clip/audioSource/subtitleText.");
            return;
        }
        StopAllCoroutines();
        StartCoroutine(PlayRoutine(chosen));
    }

    IEnumerator PlayRoutine(Line line)
    {
        // Set subtitle visible & fully opaque
        subtitleText.text = line.subtitle ?? "";
        subtitleText.gameObject.SetActive(true);
        var c = subtitleText.color;
        c.a = 1f; subtitleText.color = c;

        audioSource.PlayOneShot(line.clip);

        // Wait for clip duration + hold
        yield return new WaitForSeconds(line.clip.length + Mathf.Max(0f, extraHoldSeconds));

        // Fade out
        float t = 0f;
        float dur = Mathf.Max(0.01f, fadeDuration);
        float startA = subtitleText.color.a;
        while (t < dur)
        {
            t += Time.deltaTime;
            float a = Mathf.Lerp(startA, 0f, t / dur);
            var cc = subtitleText.color; cc.a = a; subtitleText.color = cc;
            yield return null;
        }

        // Hide
        subtitleText.gameObject.SetActive(false);
    }
}
