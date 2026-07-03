using UnityEngine;

namespace KLibU.Synthesizers
{
    // References: 
    // https://www.w3.org/TR/audio-eq-cookbook/
    //

    public enum Shape { LowPass}

    public class BiquadFilter
    {
        public bool Active { get; set; }    
        public Shape Shape {  get; set; }
        public float Cutoff { get; set; }
        public float Resonance { get; set; }
        public float Gain { get; set; }
        public bool Cascade { get; set; }

        private float _Fs;
        private int _bufferSize;

        private float _a0, _a1, _a2;
        private float _b0, _b1, _b2;

        private float _target_a0, _target_a1, _target_a2;
        private float _target_b0, _target_b1, _target_b2;

        private float _xlast1, _xlast2;
        private float _ylast1, _ylast2;

        private float _x2last1, _x2last2;
        private float _y2last1, _y2last2;

        public BiquadFilter()
        {
            Active = false;
            Shape = Shape.LowPass;
            Cutoff = 1000;
            Resonance = 1;
            Gain = 1;
            Cascade = false;
        }

        public void Initialize(float Fs, int bufferSize)
        {
            _Fs = Fs;
            _bufferSize = bufferSize;

            ComputeCoefficients();
            Reset();
        }

        public void SetProperties(bool active, Shape shape, float cutoff, float resonance = 1,  float gain = 1)
        {
            Active = active;
            Shape = shape;
            Cutoff = cutoff;
            Resonance = resonance;
            Gain = gain;
        }

        private void Reset()
        {
            _xlast1 = 0;
            _xlast2 = 0;
            _ylast1 = 0;
            _ylast2 = 0;

            _x2last1 = 0;
            _x2last2 = 0;
            _y2last1 = 0;
            _y2last2 = 0;

            _a0 = _target_a0;
            _a1 = _target_a1;
            _a2 = _target_a2;

            _b0 = _target_b0;
            _b1 = _target_b1;
            _b2 = _target_b2;
        }

        public void Process(float[] data)
        {
            if (!Active) return;

            ComputeCoefficients();

            float delta_b0 = (_target_b0 - _b0) / _bufferSize;
            float delta_b1 = (_target_b1 - _b1) / _bufferSize;
            float delta_b2 = (_target_b2 - _b2) / _bufferSize;
            float delta_a1 = (_target_a1 - _a1) / _bufferSize;
            float delta_a2 = (_target_a2 - _a2) / _bufferSize;

            for (int k=0; k<data.Length; k++)
            {
                _b0 += delta_b0;
                _b1 += delta_b1;
                _b2 += delta_b2;
                _a1 += delta_a1;
                _a2 += delta_a2;

                float x = data[k];
                float y = x * _b0 +
                    _xlast1 * _b1 +
                    _xlast2 * _b2 -
                    _ylast1 * _a1 -
                    _ylast2 * _a2;

                _xlast2 = _xlast1;
                _xlast1 = x;
                _ylast2 = _ylast1;
                _ylast1 = y;

                if (Cascade)
                {
                    x = y;
                    y = x * _b0 +
                        _x2last1 * _b1 +
                        _x2last2 * _b2 -
                        _y2last1 * _a1 -
                        _y2last2 * _a2;

                    _x2last2 = _x2last1;
                    _x2last1 = x;
                    _y2last2 = _y2last1;
                    _y2last1 = y;
                }

                data[k] = y;
            }
        }

        private void ComputeCoefficients()
        {
            float w0 = 2 * Mathf.PI * Cutoff / _Fs;

            float cosw0 = Mathf.Cos(w0);
            float alpha = Mathf.Sin(w0) / (2 * Resonance);
            float normFactor = 1 + alpha;

            switch (Shape)
            {
                case Shape.LowPass:
                    _target_b0 = 0.5f * (1 - cosw0) / normFactor;
                    _target_b1 = (1 - cosw0) / normFactor;
                    _target_b2 = _target_b0;

                    _target_a0 = 1;
                    _target_a1 = -2 * cosw0 / normFactor;
                    _target_a2 = (1 - alpha) / normFactor;
                    break;
            }
        }
    }
}