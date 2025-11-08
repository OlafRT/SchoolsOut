using UnityEngine;
using UnityEngine.UI;

public class BreathUI : MonoBehaviour
{
    public BicycleController bike;
    public Slider slider;

    void Start() { if (slider) slider.minValue = 0f; }
    void Update()
    {
        if (!bike || !slider) return;
        slider.maxValue = bike.maxBreath;
        slider.value = bike.breath;
    }
}
