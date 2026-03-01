using UnityEngine;
using System.Collections;

public class DisableAfterDelay : MonoBehaviour
{
    [Header("Delay Settings")]
    [SerializeField] private float delay = 0.5f;

    [Header("Objects To Disable")]
    [SerializeField] private GameObject[] objectsToDisable;

    private void OnEnable()
    {
        StartCoroutine(DisableRoutine());
    }

    private IEnumerator DisableRoutine()
    {
        yield return new WaitForSeconds(delay);

        foreach (GameObject obj in objectsToDisable)
        {
            if (obj != null)
            {
                obj.SetActive(false);
            }
        }
    }
}