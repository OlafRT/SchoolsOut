using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class NameplateUI : MonoBehaviour
{
    [Header("Bindings")]
    public Transform target;                // NPC to follow
    [Tooltip("Local offset when parented to the target (X/Z = horizontal offset, Y = height above target origin).")]
    public Vector3 worldOffset = Vector3.zero;

    [Header("Attach")]
    [Tooltip("If true, this nameplate will be parented to the target so it is destroyed with it.")]
    public bool parentToTarget = true;
    [Tooltip("If true and parented, the nameplate will be positioned using transform.localPosition = worldOffset.")]
    public bool useLocalPositionWhenParented = true;

    [Header("Billboard & Scale")]
    public Camera cam;                      // auto Camera.main if null
    public bool billboard = true;
    public float minScale = 0.8f;
    public float maxScale = 1.2f;
    public float scaleDistanceRef = 10f;

    [Header("UI")]
    public TextMeshProUGUI nameText;
    public Image healthFill;                // Type=Filled Horizontal
    public Transform statusRow;             // HorizontalLayoutGroup container
    public Image statusIconPrefab;          // disabled prefab child used as pool item

    [Header("Hostility UI")]
    public Image  moodIcon;                 // assign in prefab
    public Sprite friendlyIcon;             // 🙂
    public Sprite neutralIcon;              // 😐
    public Sprite hostileIcon;              // 😡
    public Color  friendlyName = new Color(0.25f, 1f, 0.25f);
    public Color  neutralName  = new Color(1f, 0.9f, 0.2f);
    public Color  hostileName  = new Color(1f, 0.3f, 0.3f);

    [Header("Death UI")]
    public Sprite deadIcon;                 // ☠ / 💀 / 😵‍💫 (your choice)
    public Color  deadName   = Color.white;

    NPCHealth      hp;
    NPCStatusHost  statusHost;
    NPCAI          ai;

    readonly List<Image> iconPool = new();
    readonly Dictionary<string, int> currentShown = new();

    // caches to avoid redundant work
    NPCAI.Hostility _lastHostility = (NPCAI.Hostility)(-1);
    bool _lastDead = false;

    void Awake()
    {
        if (!cam) cam = Camera.main;
        BindTarget(target);
        RefreshAll();
    }

    void OnDestroy()
    {
        if (statusHost) statusHost.OnStatusesChanged -= RefreshStatuses;
    }

    public void SetTarget(Transform t, string displayName = null)
    {
        BindTarget(t);

        if (nameText)
        {
            nameText.text = !string.IsNullOrEmpty(displayName) ? displayName :
                            (t ? t.gameObject.name : "NPC");
        }
        RefreshAll();
    }

    void BindTarget(Transform t)
    {
        if (statusHost) statusHost.OnStatusesChanged -= RefreshStatuses;

        target     = t;
        hp         = target ? target.GetComponent<NPCHealth>()     : null;
        statusHost = target ? target.GetComponent<NPCStatusHost>() : null;
        ai         = target ? target.GetComponent<NPCAI>()         : null;

        if (statusHost) statusHost.OnStatusesChanged += RefreshStatuses;

        if (parentToTarget && target)
        {
            transform.SetParent(target, false);
            if (useLocalPositionWhenParented) transform.localPosition = worldOffset;
        }
    }

    void LateUpdate()
    {
        if (!target)
        {
            if (transform.parent == null) Destroy(gameObject);
            return;
        }

        // Positioning
        if (parentToTarget && transform.parent == target && useLocalPositionWhenParented)
            transform.localPosition = worldOffset;
        else
            transform.position = target.position + worldOffset;

        // Billboard
        if (billboard && cam)
        {
            Vector3 fwd = transform.position - cam.transform.position;
            fwd.y = 0f;
            if (fwd.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
        }

        // Distance-based scale
        if (cam)
        {
            float d = Vector3.Distance(cam.transform.position, transform.position);
            float t = Mathf.Clamp01(d / Mathf.Max(0.01f, scaleDistanceRef));
            float s = Mathf.Lerp(maxScale, minScale, t);
            transform.localScale = Vector3.one * s;
        }

        // HP bar
        if (hp && healthFill)
        {
            float frac = Mathf.Approximately(hp.maxHP, 0f) ? 0f : Mathf.Clamp01(hp.currentHP / (float)hp.maxHP);
            healthFill.fillAmount = frac;
        }

        // --- Death takes priority over hostility visuals ---
        bool isDead = (hp && hp.currentHP <= 0);
        if (isDead != _lastDead)
        {
            ApplyDeathVisuals(isDead);
            _lastDead = isDead;
        }
        if (isDead) return; // keep dead look; don't override with hostility

        // Hostility -> name color + mood icon
        if (ai && nameText)
        {
            var h = ai.CurrentHostility;
            if (h != _lastHostility) ApplyHostility(h);
            _lastHostility = h;
        }
    }

    void RefreshAll()
    {
        if (nameText && target) nameText.text = target.gameObject.name;
        RefreshStatuses();

        // force a refresh of state-dependent visuals on (re)bind
        _lastDead = !_lastDead;
        _lastHostility = (NPCAI.Hostility)(-1);
    }

    void ApplyHostility(NPCAI.Hostility h)
    {
        switch (h)
        {
            case NPCAI.Hostility.Friendly:
                if (nameText) nameText.color = friendlyName;
                if (moodIcon) { moodIcon.sprite = friendlyIcon; moodIcon.enabled = (friendlyIcon != null); }
                break;

            case NPCAI.Hostility.Neutral:
                if (nameText) nameText.color = neutralName;
                if (moodIcon) { moodIcon.sprite = neutralIcon; moodIcon.enabled = (neutralIcon != null); }
                break;

            case NPCAI.Hostility.Hostile:
            default:
                if (nameText) nameText.color = hostileName;
                if (moodIcon) { moodIcon.sprite = hostileIcon; moodIcon.enabled = (hostileIcon != null); }
                break;
        }
    }

    void ApplyDeathVisuals(bool dead)
    {
        if (!nameText) return;

        if (dead)
        {
            nameText.color = deadName;
            if (moodIcon)
            {
                moodIcon.sprite  = deadIcon;
                moodIcon.enabled = (deadIcon != null);
            }
        }
        else
        {
            // On “revive”, immediately re-apply hostility look next frame
            if (moodIcon && neutralIcon) { /* no-op; hostility update will set proper icon */ }
        }
    }

    void RefreshStatuses()
    {
        if (!statusRow || !statusIconPrefab) return;

        int needed = 0;
        currentShown.Clear();

        if (statusHost != null)
        {
            foreach (var kv in statusHost.Active)
            {
                int countThisTag = 0;
                foreach (var s in kv.Value)
                {
                    if (s.icon) countThisTag++;
                }
                if (countThisTag > 0)
                {
                    currentShown[kv.Key] = countThisTag;
                    needed += countThisTag;
                }
            }
        }

        while (iconPool.Count < needed)
        {
            var img = Instantiate(statusIconPrefab, statusRow);
            img.enabled = true;
            iconPool.Add(img);
        }
        for (int i = 0; i < iconPool.Count; i++)
            iconPool[i].gameObject.SetActive(i < needed);

        int idx = 0;
        if (statusHost != null)
        {
            foreach (var kv in statusHost.Active)
            {
                foreach (var s in kv.Value)
                {
                    if (!s.icon) continue;
                    iconPool[idx].sprite = s.icon;
                    iconPool[idx].color = Color.white;
                    idx++;
                }
            }
        }
    }
}
