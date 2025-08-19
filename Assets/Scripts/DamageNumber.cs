using UnityEngine;
using TMPro;

[DisallowMultipleComponent]
public class DamageNumber : MonoBehaviour
{
    [Header("Refs")]
    public TextMeshProUGUI tmp;

    [Header("Motion")]
    public float lifetime = 1.0f;
    public float risePixelsPerSec = 60f;
    public Vector2 spawnJitter = new Vector2(12f, 6f);

    [Header("Crit styling")]
    public Color normalColor = Color.white;
    public Color critColor = new Color(1f, 0.55f, 0f); // orange
    public float normalFontSize = 28f;
    public float critFontSize = 48f;

    Camera cam;
    Transform follow;
    Vector3 worldPos;
    Vector2 screenPos;
    float t;

    public void Init(Vector3 worldPos, int amount, bool isCrit, Transform follow = null, float lifetimeOverride = -1f)
    {
        if (!tmp) tmp = GetComponentInChildren<TextMeshProUGUI>(true);
        if (!cam) cam = Camera.main;

        this.worldPos = worldPos;
        this.follow = follow;
        if (lifetimeOverride > 0f) lifetime = lifetimeOverride;

        // seed initial screen pos with a little jitter
        Vector3 baseWp = follow ? follow.position : worldPos;
        Vector3 baseSp = cam ? cam.WorldToScreenPoint(baseWp) : baseWp;
        baseSp.x += Random.Range(-spawnJitter.x, spawnJitter.x);
        baseSp.y += Random.Range(-spawnJitter.y, spawnJitter.y);
        screenPos = baseSp;

        if (tmp)
        {
            tmp.text = Mathf.Max(0, amount).ToString();
            tmp.color = isCrit ? critColor : normalColor;
            tmp.fontSize = isCrit ? critFontSize : normalFontSize;
            tmp.alpha = 1f;
        }

        t = 0f;
    }

    void Update()
    {
        if (!cam) cam = Camera.main;

        t += Time.deltaTime;
        float u = Mathf.Clamp01(t / Mathf.Max(0.01f, lifetime));

        // track target (or fixed world pos), then rise in screen space
        Vector3 baseWp = follow ? follow.position : worldPos;
        Vector3 baseSp = cam ? cam.WorldToScreenPoint(baseWp) : baseWp;

        float rise = risePixelsPerSec * t;
        Vector3 sp = new Vector3(screenPos.x, screenPos.y + rise, baseSp.z);

        ((RectTransform)transform).position = sp;

        if (tmp)
        {
            var c = tmp.color;
            c.a = 1f - u;
            tmp.color = c;
        }

        if (u >= 1f)
        {
            if (CombatTextManager.Instance)
                CombatTextManager.Instance.ReturnToPool(this);
            else
                Destroy(gameObject);
        }
    }
}