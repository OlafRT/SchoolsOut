
// using UnityEngine;
// using System.IO;

// public class GameSaveController : MonoBehaviour {
//     public ItemDatabase database; public Inventory inventory; public EquipmentState equipment; public PlayerWallet wallet;
//     string InvPath => Path.Combine(Application.persistentDataPath, "inventory.json");
//     string EqPath  => Path.Combine(Application.persistentDataPath, "equipment.json");
//     string WalPath => Path.Combine(Application.persistentDataPath, "wallet.json");
//     void Start(){ InventoryPersistence.LoadInventory(inventory, database, InvPath); InventoryPersistence.LoadEquipment(equipment, database, EqPath); InventoryPersistence.LoadWallet(wallet, WalPath);}    
//     void OnApplicationQuit(){ InventoryPersistence.SaveInventory(inventory, InvPath); InventoryPersistence.SaveEquipment(equipment, EqPath); InventoryPersistence.SaveWallet(wallet, WalPath);} }