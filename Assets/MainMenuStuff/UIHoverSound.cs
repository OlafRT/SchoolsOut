using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class UIHoverSound : MonoBehaviour, IPointerEnterHandler, ISelectHandler
{
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;   // Put one on your Canvas and reference it here
    [SerializeField] private AudioClip hoverClip;
    [SerializeField, Range(0f, 1f)] private float volume = 1f;
    [SerializeField, Range(0f, 0.2f)] private float pitchJitter = 0.03f; // ±3%

    [Header("Behavior")]
    [SerializeField] private bool playOnPointerEnter = true;
    [SerializeField] private bool playOnSelect = true; // for keyboard/controller nav
    [SerializeField] private bool onlyIfInteractable = true;
    [SerializeField, Range(0f, 0.5f)] private float minInterval = 0.08f; // anti-spam

    float _lastPlayTime;
    float _basePitch = 1f;
    Selectable _selectable;

    void Awake()
    {
        _selectable = GetComponent<Selectable>();
        if (!audioSource)
        {
            // Try to find an AudioSource on a parent Canvas
            var canvas = GetComponentInParent<Canvas>();
            if (canvas) audioSource = canvas.GetComponent<AudioSource>();
        }
        if (audioSource) _basePitch = audioSource.pitch;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (playOnPointerEnter) TryPlay();
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (playOnSelect) TryPlay();
    }

    void TryPlay()
    {
        if (!audioSource || !hoverClip) return;
        if (onlyIfInteractable && _selectable && !_selectable.interactable) return;

        float now = Time.unscaledTime;
        if (now - _lastPlayTime < minInterval) return; // throttle

        // slight pitch variation for feel
        float jitter = Random.Range(-pitchJitter, pitchJitter);
        audioSource.pitch = Mathf.Clamp(_basePitch * (1f + jitter), 0.5f, 2f);

        audioSource.PlayOneShot(hoverClip, volume);
        audioSource.pitch = _basePitch; // restore

        _lastPlayTime = now;
    }
}
