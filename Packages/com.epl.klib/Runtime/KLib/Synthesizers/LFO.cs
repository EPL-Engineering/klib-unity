using System;
using System.Collections.Generic;
using UnityEngine;

namespace KLibU.Synthesizers
{
    public class LFO
    {
        public Waveform Waveform { get; set; }
        public float Frequency { get; set; }

        private List<ParameterControl> _controls;

        private float _Fs;
        private float _deltaTime;
        private float _time;

        public LFO()
        {
            Waveform = Waveform.Sine;
            Frequency = 0.5f;

            _controls = new List<ParameterControl>();
        }

        public void Initialize(float Fs, int bufferSize)
        {
            _Fs = Fs;
            _deltaTime = (float)bufferSize / Fs;
            _time = 0;
        }

        public void AddControl(Action<float> action, float minValue, float maxValue)
        {
            _controls.Add(new ParameterControl(action, minValue, maxValue));
        }

        public void Process()
        {
            if (_controls.Count > 0)
            {
                float value = Mathf.Sin(2 * Mathf.PI * Frequency * _time);
                _time += _deltaTime;

                foreach (var control in _controls)
                {
                    control.Apply(value);
                }
            }
        }

    }
}