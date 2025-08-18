using UnityEngine;

public class PlayerTileRegistrar : MonoBehaviour
{
    public float tileSize = 1f;
    Vector2Int currentTile;

    void Start()
    {
        Vector3 snap = Snap(transform.position);
        currentTile = WorldToTile(snap);
        NPCTileRegistry.Register(currentTile);
    }

    void Update()
    {
        Vector2Int tile = WorldToTile(Snap(transform.position));
        if (tile != currentTile)
        {
            NPCTileRegistry.Unregister(currentTile);
            NPCTileRegistry.Register(tile);
            currentTile = tile;
        }
    }

    void OnDestroy() => NPCTileRegistry.Unregister(currentTile);

    Vector3 Snap(Vector3 p) =>
        new Vector3(Mathf.Round(p.x / tileSize) * tileSize, p.y, Mathf.Round(p.z / tileSize) * tileSize);

    Vector2Int WorldToTile(Vector3 w) =>
        new Vector2Int(Mathf.RoundToInt(w.x / tileSize), Mathf.RoundToInt(w.z / tileSize));
}