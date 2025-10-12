using System;
using UnityEngine;

[CreateAssetMenu(menuName = "RPG/Player Wallet", fileName = "PlayerWallet")]
public class PlayerWallet : ScriptableObject {
    public int dollars;
    public event Action OnChanged;
    public void NotifyChanged() { OnChanged?.Invoke(); }
    public void Add(int amount){ dollars = Mathf.Max(0, dollars + amount); OnChanged?.Invoke(); }
    public bool Spend(int amount){ if(amount<=0) return true; if(dollars < amount) return false; dollars -= amount; OnChanged?.Invoke(); return true; }
}