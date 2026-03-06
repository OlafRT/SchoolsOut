using UnityEngine;

public class EnableObjectOnTriggerEnter : MonoBehaviour
{
    [Header("What gets enabled")]
    [SerializeField] private GameObject targetToEnable;

    [Header("Who can trigger it")]
    [SerializeField] private string playerTag = "Player";

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        if (targetToEnable != null) targetToEnable.SetActive(true);
    }
}