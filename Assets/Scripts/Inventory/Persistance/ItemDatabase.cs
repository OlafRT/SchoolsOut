using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "RPG/Item Database", fileName = "ItemDatabase")]
public class ItemDatabase : ScriptableObject {
    public List<ItemTemplate> templates = new();
    private Dictionary<string, ItemTemplate> map;
    private void OnEnable(){ map = new Dictionary<string, ItemTemplate>(); foreach(var t in templates) if(t!=null && !string.IsNullOrEmpty(t.id)) map[t.id]=t; }
    public ItemTemplate Get(string id){ return (map!=null && map.TryGetValue(id, out var t)) ? t : null; }
}