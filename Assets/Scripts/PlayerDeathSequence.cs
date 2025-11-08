using UnityEngine;
using System.Collections;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PlayerDeathSequence : MonoBehaviour
{
    [Header("Animator")]
    public string deathBoolName = "IsDead";
    public int animatorLayer = 0;           // base layer
    public string deathStateTag = "Death";  // tag your death state(s) "Death"
    public float fallbackDeathTime = 1.2f;  // if no tagged state found

    [Header("Panel Fade")]
    public float delayBeforeFade = 0.1f;    // small settle before UI
    public float fadeDuration = 1.0f;       // unscaled seconds

    PlayerHealth health;
    PlayerBootstrap bootstrap;
    Animator anim;

    void Awake()
    {
        health = GetComponent<PlayerHealth>();
        bootstrap = GetComponent<PlayerBootstrap>();
        anim = bootstrap ? bootstrap.ActiveAnimator : GetComponentInChildren<Animator>(true);
        if (health) health.OnDied += HandleDied;
    }

    void OnDestroy()
    {
        if (health) health.OnDied -= HandleDied;
    }

    void HandleDied()
    {
        if (!anim) anim = bootstrap ? bootstrap.ActiveAnimator : GetComponentInChildren<Animator>(true);
        if (anim && !string.IsNullOrEmpty(deathBoolName))
            anim.SetBool(deathBoolName, true);

        StartCoroutine(Sequence());
    }

    IEnumerator Sequence()
    {
        // Wait for a tagged "Death" state, else a fallback time
        bool playedDeath = false;
        float waited = 0f;

        if (anim)
        {
            // give Animator a moment to transition
            for (float t = 0; t < 0.15f; t += Time.unscaledDeltaTime) yield return null;

            // try to find & wait the death state
            float safety = 3f;
            while (safety > 0f)
            {
                var st = anim.GetCurrentAnimatorStateInfo(animatorLayer);
                if (st.IsTag(deathStateTag))
                {
                    playedDeath = true;
                    float target = Mathf.Max(0.1f, st.length * 0.95f);
                    while (waited < target)
                    {
                        waited += Time.unscaledDeltaTime;
                        yield return null;
                    }
                    break;
                }
                safety -= Time.unscaledDeltaTime;
                yield return null;
            }
        }

        if (!playedDeath)
        {
            float t = 0f;
            while (t < fallbackDeathTime)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        if (delayBeforeFade > 0f)
            yield return new WaitForSecondsRealtime(delayBeforeFade);

        // Fade the panel in, on TOP of everything (independent canvas)
        var hud = PlayerHUD.Instance;
        if (hud && hud.deathPanel)
        {
            var panelGO = hud.deathPanel;

            // Ensure it has its own Canvas on top (so parent CanvasGroup/Animators can't hide it)
            var cv = panelGO.GetComponent<Canvas>();
            if (!cv) cv = panelGO.AddComponent<Canvas>();
            cv.overrideSorting = true;
            cv.sortingOrder = short.MaxValue - 1;

            // Ensure raycaster for clicks (optional)
            if (!panelGO.GetComponent<GraphicRaycaster>()) panelGO.AddComponent<GraphicRaycaster>();

            // Ensure CanvasGroup at 0
            var cg = panelGO.GetComponent<CanvasGroup>();
            if (!cg) cg = panelGO.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.blocksRaycasts = true;
            cg.interactable = true;

            // Make it active and last sibling (also helps when sharing a Canvas)
            panelGO.SetActive(true);
            panelGO.transform.SetAsLastSibling();

            // One frame so the new Canvas/CG register with the UI system
            yield return null;

            // Smooth fade unscaled
            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, fadeDuration));
                cg.alpha = k;
                yield return null;
            }
            cg.alpha = 1f;
        }
    }
}
