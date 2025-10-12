using System.Collections.Generic;
using UnityEngine;

public static class RarityDropTable {
    // Min item level for each rarity
    static readonly Dictionary<Rarity,int> gate = new(){
        { Rarity.Poor, 1 },
        { Rarity.Common, 1 },
        { Rarity.Uncommon, 3 },
        { Rarity.Rare, 7 },
        { Rarity.Epic, 12 },
        { Rarity.Legendary, 20 },
    };

    public static Rarity RollRarity(int itemLevel){
        float t = Mathf.Clamp01(itemLevel / 30f);
        var weights = new Dictionary<Rarity,float>{
            { Rarity.Poor, 25f * (1f - 0.6f * t) },     // falls with level
            { Rarity.Common, 55f * (1f - 0.3f * t) },   // falls slightly
            { Rarity.Uncommon, 15f * (0.5f + 0.5f*t) }, // rises
            { Rarity.Rare, 4f * (0.3f + 1.7f*t) },      // rises more
            { Rarity.Epic, 0.9f * (0.1f + 3.0f*t) },    // rises a lot
            { Rarity.Legendary, 0.1f * (0.05f + 4.0f*t) } // extremely rare, but scales
        };
        foreach(var r in new List<Rarity>(weights.Keys)) if(itemLevel < gate[r]) weights[r] = 0f; // gate
        float sum=0f; foreach(var v in weights.Values) sum+=v; if(sum<=0f) return Rarity.Common;
        float pick = UnityEngine.Random.value * sum; float run=0f;
        foreach(var kv in weights){ run += kv.Value; if(pick <= run) return kv.Key; }
        return Rarity.Common;
    }
}