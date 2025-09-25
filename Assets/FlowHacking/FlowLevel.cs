// FlowLevel.cs
using UnityEngine;

[CreateAssetMenu(menuName = "Flow/Level")]
public class FlowLevel : ScriptableObject
{
    public int width = 5, height = 5;

    [Tooltip("Seconds allowed to solve this level. Set <=0 for no timer.")]
    public float timeLimitSeconds = 60f;

    [System.Serializable]
    public struct Pair
    {
        public string name;
        public Color color;
        public Vector2Int a;
        public Vector2Int b;
    }
    public Pair[] pairs;
}