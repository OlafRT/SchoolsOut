using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class ItemLevelRules {
    public static int RequiredLevelForItemLevel(int ilvl){
        if (ilvl <= 4) return 1;
        if (ilvl <= 9) return 5;
        if (ilvl <= 14) return 10;
        if (ilvl <= 19) return 15;
        return 30; // 20–30
    }
}