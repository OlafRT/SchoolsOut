using UnityEngine;

public static class RarityFX {
    public static Color Glow(Rarity r){ var c = RarityColors.Get(r); c.a = (r==Rarity.Epic? 0.45f : r==Rarity.Legendary? 0.6f : 0.25f); return c; }
}