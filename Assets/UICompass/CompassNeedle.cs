using UnityEngine;

public class CompassNeedle : MonoBehaviour
{
    public Transform playerTransform;
    public float rotationSpeed = 180f;
    public float wobbleAmount = 1f;
    public float wobbleSpeed = 1f;
    public float noiseFrequency = 1.5f;
    public float noiseSmoothness = 3f;
    public float playerSpeed = 0f;

    private float smoothAngle;
    private float currentNoiseValue = 0f;

    void Update()
    {
        if (!playerTransform) return;

        // ROTATE NEEDLE to always point NORTH (world forward, +Z)
        float targetAngle = playerTransform.eulerAngles.y; // not negated anymore
        smoothAngle = Mathf.LerpAngle(smoothAngle, targetAngle, Time.deltaTime * (rotationSpeed / 90f));

        // Add wobble
        float time = Time.time * wobbleSpeed;
        float rawNoise = (Mathf.PerlinNoise(time * noiseFrequency, 0f) - 0.5f) * 2f;
        currentNoiseValue = Mathf.Lerp(currentNoiseValue, rawNoise, Time.deltaTime * noiseSmoothness);
        float wobble = currentNoiseValue * wobbleAmount * Mathf.Clamp01(playerSpeed / 5f);

        float finalRotation = smoothAngle + wobble;
        transform.localEulerAngles = new Vector3(0f, 0f, finalRotation);
    }
}
