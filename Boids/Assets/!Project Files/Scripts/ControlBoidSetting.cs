using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ControlBoidSetting : MonoBehaviour {

    [SerializeField]
    private TMP_Text propName;
    [SerializeField]
    private Slider slider;
    private float defaultValue;


    public void Init(float min, float max, float defaultValue, string propName, Action<float> sliderChanged) {
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = defaultValue;
        this.defaultValue = defaultValue;
        this.propName.text = propName;
        slider.onValueChanged.AddListener(f => sliderChanged(f));
    }

    public void SetToDefault() {
        slider.value = defaultValue;
    }
}