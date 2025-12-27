using UnityEngine;
using UniRx;
using System;
using ReactiveSolutions.AttributeSystem.Core;
using TMPro;

namespace ReactiveSolutions.AttributeSystem.Unity.UI
{
    /// <summary>
    /// Simple implementation using the MonitorAttribute helper.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class AttributeDisplayText : AttributeUIBehaviour
    {
        //[AttributeName]
        [SerializeField] private string _attributeName;

        [Header("Formatting")]
        [SerializeField] private string _prefix;
        [SerializeField] private string _format = "{0:0}";
        [SerializeField] private string _postfix;

        private TMP_Text _textComponent;

        protected override void Awake()
        {
            base.Awake();

            _textComponent = GetComponent<TMP_Text>();
            Debug.Assert(_textComponent != null, $"No TMP_Text on {gameObject.name}");

            // Sugar: Clean one-liner that handles all race conditions
            MonitorAttribute(_attributeName, UpdateText);
        }

        private void UpdateText(float value)
        {
            if (_textComponent == null) return;
            string formattedValue = string.Format(_format, value);
            _textComponent.text = $"{_prefix}{formattedValue}{_postfix}";
        }
    }
}