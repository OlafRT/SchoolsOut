using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class IrisCloseEffect : MonoBehaviour
{
    [SerializeField] private Image overlayImage;
    [SerializeField] private float closeDuration = 2f;
    [SerializeField] private float startRadius = 1.2f;
    [SerializeField] private float endRadius = 0f;
    [SerializeField] private Vector2 center = new Vector2(0.5f, 0.5f);

    private Material runtimeMaterial;

    private void Awake()
    {
        runtimeMaterial = Instantiate(overlayImage.material);
        overlayImage.material = runtimeMaterial;

        runtimeMaterial.SetFloat("_Radius", startRadius);
        runtimeMaterial.SetVector("_Center", new Vector4(center.x, center.y, 0f, 0f));
    }

    public void StartIrisClose()
    {
        StartCoroutine(IrisCloseRoutine());
    }

    private IEnumerator IrisCloseRoutine()
    {
        float time = 0f;

        while (time < closeDuration)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / closeDuration);

            float radius = Mathf.Lerp(startRadius, endRadius, t);
            runtimeMaterial.SetFloat("_Radius", radius);

            yield return null;
        }

        runtimeMaterial.SetFloat("_Radius", -1f); // force fully closed
    }
}