using ReactiveSolutions.AttributeSystem.Core.Data;
using UnityEngine;
using UnityEngine.UI;

namespace ReactiveSolutions.AttributeSystem.Unity.UI
{
    /// <summary>
    /// A reactive UI component that drives a Slider based on two attributes (Current/Max).
    /// Designed to be plugged into any UI tool that uses standard Unity Sliders.
    /// </summary>
    [RequireComponent(typeof(Slider))]
    public class AttributeProgressBar : AttributeUIBehaviour
    {
        [Header("Attribute Mapping")]
        [AttributeName]
        [SerializeField] private string _currentAttributeName;

        [AttributeName]
        [SerializeField] private string _maxAttributeName;

        [Header("Settings")]
        [Tooltip("If true, the slider's max value will be set to the max attribute's value.")]
        [SerializeField] private bool _syncSliderMax = true;

        private Slider _slider;
        private float _currentVal;
        private float _maxVal = 1f;

        protected override void Awake()
        {
            base.Awake();

            _slider = GetComponent<Slider>();
            Debug.Assert(_slider != null, $"[AttributeProgressBar] No Slider found on {gameObject.name}");

            // Start monitoring both values. 
            // The MonitorAttribute handles the "triple-wait" race conditions.
            MonitorAttribute(_currentAttributeName, val =>
            {
                _currentVal = val;
                RefreshBar();
            });

            MonitorAttribute(_maxAttributeName, val =>
            {
                _maxVal = val;
                RefreshBar();
            });
        }

        private void RefreshBar()
        {
            if (_slider == null) return;

            // Optional: Sync the Slider's mathematical bounds
            if (_syncSliderMax)
            {
                _slider.minValue = 0;
                _slider.maxValue = _maxVal;
                _slider.value = _currentVal;
            }
            else
            {
                // Otherwise, treat slider as 0-1 normalized
                float ratio = _maxVal > 0 ? _currentVal / _maxVal : 0f;
                _slider.value = Mathf.Clamp01(ratio);
            }
        }
    }
}