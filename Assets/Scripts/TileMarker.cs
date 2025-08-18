using UnityEngine;

public class TileMarker : MonoBehaviour
{
    private float life;
    private float t;

    public void Init(float duration, float tileSize)
    {
        life = duration;
        transform.localScale = new Vector3(tileSize, 0.01f, tileSize);
    }

    void Update()
    {
        t += Time.deltaTime;
        if (t >= life) Destroy(gameObject);
        // Optional: simple fade if there's a renderer
        var r = GetComponentInChildren<Renderer>();
        if (r && r.material.HasProperty("_Color"))
        {
            var c = r.material.color;
            c.a = Mathf.Lerp(1f, 0f, t / life);
            r.material.color = c;
        }
    }
}
