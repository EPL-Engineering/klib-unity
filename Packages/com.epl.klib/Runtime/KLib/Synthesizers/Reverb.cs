using System.Collections.Generic;

using UnityEngine;

namespace KLibU.Synthesizers
{
    public class Reverb
    {
        public bool Active { get; set; }
        public float Feedback { get; set; }
        public float Damping { get; set; }
        public float Mix { get; set; }

        private List<CombFilter> _combFilters;
        private List<AllPassFilter> _allPassFilters;

        private float _Fs;

        private float[] _wet;

        private float _lastMix;

        private float _fixedGain = 0.14f;
        private readonly float[] _combTunings = new float[] {
            0.02530612244f,
            0.02693877551f,
            0.02895691609f,
            0.03074829931f,
            0.03224489795f,
            0.0338095238f,
            0.03530612244f,
            0.03666666666f
          };

        private readonly float[] _allPassTunings = new float[] {
            0.01260770975f,
            0.01f,
            0.0077324263f,
            0.00510204081f
          };

        public Reverb()
        {
            Active = false;
            Feedback = 0.9f;
            Damping = 0.5f;
            Mix = 0.5f;

            _combFilters = new List<CombFilter>();
            _allPassFilters = new List<AllPassFilter>();
        }

        public void Initialize(float Fs, int bufferSize)
        {
            _Fs = Fs;
            _wet = new float[bufferSize];

            _combFilters.Clear();
            foreach (float tuning in _combTunings)
            {
                _combFilters.Add(new CombFilter(Fs, tuning));
            }

            _allPassFilters.Clear();
            foreach (var tuning in _allPassTunings)
            { 
                _allPassFilters.Add(new AllPassFilter(Fs, tuning));
            }

            _lastMix = Mix;
        }

        public void Process(float[] data)
        {
            if (!Active) return;

            for (int k = 0; k < _wet.Length; k++) _wet[k] = 0;

            for (int k = 0; k < _combFilters.Count; k++)
            {
                _combFilters[k].Process(data, _wet, Feedback, Damping);
            }

            for (int k = 0; k < _allPassFilters.Count; k++)
            {
                _allPassFilters[k].Process(_wet);
            }

            float deltaMix = (Mix - _lastMix) / data.Length;

            for (int k=0; k < data.Length; k++)
            {
                _lastMix = Mathf.Clamp(_lastMix + deltaMix, 0, 1);

                float dry = Mathf.Sqrt(1 - _lastMix);
                float wet = Mathf.Sqrt(_lastMix);

                data[k] = dry * data[k] + _fixedGain * wet * _wet[k];
            }
        }

    }
}