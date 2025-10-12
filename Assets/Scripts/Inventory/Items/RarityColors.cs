using System.Collections.Generic;
using UnityEngine;


public static class RarityColors {
    static readonly Dictionary<Rarity, Color> map = new(){
        { Rarity.Poor,      Hex("#9d9d9d") }, // grey
        { Rarity.Common,    Hex("#ffffff") }, // white
        { Rarity.Uncommon,  Hex("#1eff00") }, // green
        { Rarity.Rare,      Hex("#0070dd") }, // blue
        { Rarity.Epic,      Hex("#a335ee") }, // purple
        { Rarity.Legendary, Hex("#ff8000") }, // orange
    };
    static Color Hex(string hex){ ColorUtility.TryParseHtmlString(hex, out var c); return c; }
    public static Color Get(Rarity r) => map.TryGetValue(r, out var c) ? c : Color.white;
}