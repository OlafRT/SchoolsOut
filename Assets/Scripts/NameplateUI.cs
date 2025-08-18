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

    NPCHealth hp;
    NPCStatusHost statusHost;

    readonly List<Image> iconPool = new();
    readonly Dictionary<string, int> currentShown = new(); // tag->count shown

    void Awake()
    {
        if (!cam) cam = Camera.main;
        // Don’t touch worldOffset here — honor the prefab setting exactly.
        BindTarget(target); // in case it was assigned in the prefab
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
        // Unsubscribe old host
        if (statusHost) statusHost.OnStatusesChanged -= RefreshStatuses;

        target = t;
        hp = target ? target.GetComponent<NPCHealth>() : null;
        statusHost = target ? target.GetComponent<NPCStatusHost>() : null;

        if (statusHost) statusHost.OnStatusesChanged += RefreshStatuses;

        // Parent to target if requested
        if (parentToTarget && target)
        {
            // Reparent with worldPositionStays=false so our localPosition becomes the new offset
            transform.SetParent(target, false);
            if (useLocalPositionWhenParented) transform.localPosition = worldOffset;
        }
    }

    void LateUpdate()
    {
        if (!target)
        {
            // If we somehow lost the target and aren’t parented, self-cleanup
            if (transform.parent == null) Destroy(gameObject);
            return;
        }

        // Positioning
        if (parentToTarget && transform.parent == target && useLocalPositionWhenParented)
        {
            // Keep the local offset exactly as set in the prefab/inspector
            transform.localPosition = worldOffset;
        }
        else
        {
            // Fallback: position in world space relative to target
            transform.position = target.position + worldOffset;
        }

        // Billboard
        if (billboard && cam)
        {
            Vector3 fwd = transform.position - cam.transform.position;
            fwd.y = 0f;
            if (fwd.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
        }

        // Distance-based scale (applies to localScale so it works parented too)
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
    }

    void RefreshAll()
    {
        if (nameText && target) nameText.text = target.gameObject.name;
        RefreshStatuses();
    }

    void RefreshStatuses()
    {
        if (!statusRow || !statusIconPrefab) return;

        // Count how many icons we need
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
