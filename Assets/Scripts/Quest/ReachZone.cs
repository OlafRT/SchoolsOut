// ReachZone.cs
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ReachZone : MonoBehaviour
{
    public string placeId = "cafeteria";
    void OnTriggerEnter(Collider other){
        if (other.CompareTag("Player")) QuestEvents.PlaceReached?.Invoke(placeId);
    }
}
