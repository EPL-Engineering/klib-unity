using System;

using UnityEngine;

namespace KLibU.Synthesizers
{
    internal class ParameterControl
    {
        private Action<float> _action { get; set; }
        private float _minValue { get; set; }
        private float _maxValue { get; set; }

        public ParameterControl()
        {
            _action = null;
            _minValue = 0f;
            _maxValue = 1f;
        }

        public ParameterControl(Action<float> action, float minValue, float maxValue)
        {
            _action = action;
            _minValue = minValue;
            _maxValue = maxValue;
        }

        public void Apply(float value)
        {
            float paramValue = _minValue + 0.5f * (value + 1) * (_maxValue - _minValue);
            _action(paramValue);
        }
    }
}