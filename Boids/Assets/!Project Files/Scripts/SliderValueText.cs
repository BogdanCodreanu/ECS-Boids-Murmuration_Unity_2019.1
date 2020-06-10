using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(TMP_Text))]
public class SliderValueText : MonoBehaviour {
    [SerializeField]
    private Slider slider;
    private TMP_Text text;

    private void Start() {
        text = GetComponent<TMP_Text>();
        slider.onValueChanged.AddListener(f => text.text = 
            Mathf.Lerp(0, 10, (f - slider.minValue) / (slider.maxValue - slider.minValue)).ToString("0.00"));

        text.text = 
            Mathf.Lerp(0, 10, (slider.value - slider.minValue) / (slider.maxValue - slider.minValue)).ToString("0.00");
    }
}