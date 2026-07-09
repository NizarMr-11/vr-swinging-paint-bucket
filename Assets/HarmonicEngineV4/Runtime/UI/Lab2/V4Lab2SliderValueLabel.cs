using UnityEngine;
using UnityEngine.UI;

namespace HarmonicEngineV4.UI.Lab2
{
    /// <summary>Live value readout for a slider.</summary>
    [RequireComponent(typeof(Slider))]
    public sealed class V4Lab2SliderValueLabel : MonoBehaviour
    {
        [SerializeField] private Text valueText;
        [SerializeField] private string format = "0.###";
        [SerializeField] private bool wholeNumbers;

        private Slider _slider;

        private void Awake()
        {
            _slider = GetComponent<Slider>();
            if (valueText == null)
            {
                valueText = V4Lab2UITheme.CreateValueLabel(transform);
            }

            if (_slider != null)
            {
                _slider.onValueChanged.AddListener(OnValueChanged);
                OnValueChanged(_slider.value);
            }

            RefreshLayout();
        }

        public void Bind(Slider slider, Text label, string valueFormat, bool integers)
        {
            _slider = slider;
            valueText = label;
            format = valueFormat;
            wholeNumbers = integers;
            RefreshLayout();
            if (_slider != null)
            {
                _slider.onValueChanged.RemoveListener(OnValueChanged);
                _slider.onValueChanged.AddListener(OnValueChanged);
                OnValueChanged(_slider.value);
            }
        }

        public void RefreshLayout()
        {
            if (valueText == null)
            {
                return;
            }

            RectTransform sliderRect = _slider != null
                ? _slider.transform as RectTransform
                : transform as RectTransform;
            V4Lab2UITheme.AlignValueLabelToSlider(sliderRect, valueText.rectTransform);
        }

        private void OnValueChanged(float value)
        {
            if (valueText == null)
            {
                return;
            }

            valueText.text = wholeNumbers
                ? Mathf.RoundToInt(value).ToString()
                : value.ToString(format);
        }
    }
}
