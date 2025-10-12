using System.Collections;
using UnityEngine;
using TMPro;

public class ScreenToast : MonoBehaviour {
    public TMP_Text label;
    public float fadeIn = 0.08f, hold = 1.0f, fadeOut = 0.7f;

    CanvasGroup cg;
    Coroutine running;

    void Awake(){
        cg = GetComponent<CanvasGroup>();
        if (!cg) cg = gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        if (!label) label = GetComponentInChildren<TMP_Text>(true);
    }

    public void Show(string msg, Color color){
        if (!label) return;
        label.text = msg;
        label.color = color;
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(Play());
    }

    IEnumerator Play(){
        float t=0; while (t<fadeIn){ t+=Time.unscaledDeltaTime; cg.alpha = Mathf.SmoothStep(0,1,t/fadeIn); yield return null; }
        cg.alpha = 1; yield return new WaitForSecondsRealtime(hold);
        t=0; while (t<fadeOut){ t+=Time.unscaledDeltaTime; cg.alpha = Mathf.SmoothStep(1,0,t/fadeOut); yield return null; }
        cg.alpha = 0;
        running = null;
    }
}
