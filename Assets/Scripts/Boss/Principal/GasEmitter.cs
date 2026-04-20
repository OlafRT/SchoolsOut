using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A hidden floor emitter that rises, telegraphs 4-direction gas zones,
/// then applies MindControlEffect to the player if they stand in the gas
/// for more than contactThreshold seconds.
///
/// SETUP:
///   1. Place in the floor with localPosition.y = 0 (flush with floor).
///   2. Assign telegraphPrefab (a flat tile marker).
///   3. PrincipalBoss calls Activate() when a health milestone is hit.
///   4. Assign playerTag = "Player".
/// </summary>
public class GasEmitter : MonoBehaviour
{
    [Header("Rise")]
    [Tooltip("Y position when hidden (flush with floor).")]
    public float hiddenY    = 0f;
    [Tooltip("Y position when fully extended.")]
    public float extendedY  = 1f;
    [Tooltip("Seconds to rise from hidden to extended.")]
    public float riseSeconds = 1.8f;

    [Header("Gas Zone")]
    [Tooltip("How many tiles in each of the 4 directions the gas cloud extends.")]
    public int gasTiles = 2;
    public float tileSize = 1f;
    [Tooltip("Seconds the gas stays active before retracting.")]
    public float gasDuration = 5f;
    [Tooltip("Seconds the player must be inside before mind control triggers.")]
    public float contactThreshold = 0.3f;

    [Header("Telegraph")]
    public GameObject telegraphPrefab;
    public float markerYOffset = 0.02f;
    public Color telegraphColor = new Color(0.5f, 0f, 0.8f, 0.85f);
    public LayerMask groundMask = ~0;

    [Header("Targeting")]
    public string playerTag = "Player";
    [Tooltip("Layer the player collider is on — used for gas zone scanning.")]
    public LayerMask playerLayer;

    // Internal
    bool _active;
    bool _gasReleased;          // true while gas is actually active (after rise)
    float _playerContactTime;
    bool _mindControlApplied;
    Transform _player;
    PrincipalBoss _boss;

    // Pre-computed gas tile centers (world space), set when gas is released
    readonly List<Vector3> _gasTileCenters = new();
    readonly List<GameObject> _markers = new();
    readonly Collider[] _overlapBuf = new Collider[8];

    // ──────────────────────────────────────────
    public void Activate(PrincipalBoss boss)
    {
        if (_active) return;
        _boss   = boss;
        _player = GameObject.FindGameObjectWithTag(playerTag)?.transform;
        StartCoroutine(EmitterSequence());
    }

    // ──────────────────────────────────────────
    IEnumerator EmitterSequence()
    {
        _active             = true;
        _gasReleased        = false;
        _playerContactTime  = 0f;
        _mindControlApplied = false;
        _gasTileCenters.Clear();

        // ── Rise ──────────────────────────────
        Vector3 startPos = transform.position;
        Vector3 endPos   = new Vector3(startPos.x, startPos.y + (extendedY - hiddenY), startPos.z);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, riseSeconds);
            transform.position = Vector3.Lerp(startPos, endPos, Mathf.Clamp01(t));
            yield return null;
        }
        transform.position = endPos;

        // ── Build gas tile centers + show telegraph ──
        BuildGasTileCenters();
        ShowTelegraph();
        _gasReleased = true;

        yield return new WaitForSeconds(gasDuration);

        _gasReleased = false;
        ClearTelegraph();
        _gasTileCenters.Clear();

        // ── Retract ───────────────────────────
        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, riseSeconds);
            transform.position = Vector3.Lerp(endPos, startPos, Mathf.Clamp01(t));
            yield return null;
        }
        transform.position = startPos;
        _active = false;
    }

    void BuildGasTileCenters()
    {
        _gasTileCenters.Clear();
        Vector3 center = transform.position;
        Vector3[] dirs = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
        foreach (var dir in dirs)
            for (int i = 1; i <= gasTiles; i++)
                _gasTileCenters.Add(center + dir * (i * tileSize));
    }

    // ──────────────────────────────────────────
    void ShowTelegraph()
    {
        if (!telegraphPrefab) return;

        foreach (var tileCenter in _gasTileCenters)
        {
            Vector3 pos = tileCenter;
            float gy = SampleGroundY(pos);
            pos.y = gy + markerYOffset;

            var m = Instantiate(telegraphPrefab, pos, Quaternion.identity);
            TintMarker(m, telegraphColor);
            if (!m.TryGetComponent<TileMarker>(out var tm)) tm = m.AddComponent<TileMarker>();
            tm.Init(gasDuration, tileSize);
            _markers.Add(m);
        }
    }

    void ClearTelegraph()
    {
        foreach (var m in _markers) if (m) Destroy(m);
        _markers.Clear();
    }

    // ──────────────────────────────────────────
    //   DETECTION — scan tiles directly, no trigger collider needed
    // ──────────────────────────────────────────
    void Update()
    {
        if (!_gasReleased || _mindControlApplied || !_player) return;

        bool playerInGas = IsPlayerInGasTiles();

        if (playerInGas)
        {
            _playerContactTime += Time.deltaTime;
            if (_playerContactTime >= contactThreshold)
            {
                _mindControlApplied = true;
                ApplyMindControl();
            }
        }
        else
        {
            _playerContactTime = 0f;
        }
    }

    bool IsPlayerInGasTiles()
    {
        // Use OverlapSphere on each tile center — same pattern as BombAoEFieldHostile
        float radius = tileSize * 0.55f;

        foreach (var center in _gasTileCenters)
        {
            int count = Physics.OverlapSphereNonAlloc(
                center + Vector3.up * 0.4f, radius,
                _overlapBuf, playerLayer, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                var col = _overlapBuf[i];
                if (col && col.CompareTag(playerTag)) return true;
                // Also walk up in case the tagged root owns a child collider
                if (col && col.GetComponentInParent<Transform>() is Transform pt
                    && pt.CompareTag(playerTag)) return true;
            }
        }
        return false;
    }

    void ApplyMindControl()
    {
        if (!_player || !_boss) return;
        var mc = _player.GetComponent<MindControlEffect>();
        if (!mc) mc = _player.gameObject.AddComponent<MindControlEffect>();
        mc.Activate(_boss);
    }

    // ──────────────────────────────────────────
    float SampleGroundY(Vector3 at)
    {
        if (Physics.Raycast(at + Vector3.up * 5f, Vector3.down, out var hit, 20f, groundMask, QueryTriggerInteraction.Ignore))
            return hit.point.y;
        return at.y;
    }

    static void TintMarker(GameObject m, Color col)
    {
        if (!m) return;
        if (m.TryGetComponent<Renderer>(out var r) && r.material) { r.material.color = col; return; }
        if (m.TryGetComponent<SpriteRenderer>(out var sr)) { sr.color = col; return; }
        var cr = m.GetComponentInChildren<Renderer>();
        if (cr && cr.material) cr.material.color = col;
    }
}
