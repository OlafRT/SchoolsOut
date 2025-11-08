using UnityEngine;

public class InhalerPickup : MonoBehaviour
{
    public float refillAmount = 35f;
    public float boostMultiplier = 1.25f;
    public float boostSeconds = 2.0f;
    public float bobAmplitude = 0.15f;
    public float bobSpeed = 2f;
    public float spinSpeed = 90f;

    Vector3 startPos;

    void Start() => startPos = transform.position;

    void Update()
    {
        // Mario Kart-style bob & spin
        float y = Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
        transform.position = startPos + new Vector3(0, y, 0);
        transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
    }

    void OnTriggerEnter(Collider other)
    {
        var bike = other.GetComponentInParent<BicycleController>();
        if (!bike) return;

        bike.ApplyInhaler(refillAmount, boostMultiplier, boostSeconds);
        Destroy(gameObject);
    }
}
