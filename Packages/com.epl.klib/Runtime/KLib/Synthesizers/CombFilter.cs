using UnityEngine;

namespace KLibU.Synthesizers
{
    public class CombFilter
    {
        private float[] _delayBuffer;
        private int _delayBufferIndex;
        private int _delayedSampleIndex;
        private int _delaySamples;

        private float _filteredSample;
        private const float DC_OFFSET = 1e-25f;

        public CombFilter(float Fs, float delay)
        {
            _delaySamples = Mathf.RoundToInt(Fs * delay);
            _delayBuffer = new float[_delaySamples];

            _delayBufferIndex = 0;
            _delayedSampleIndex = 1;

            _filteredSample = 0;
        }

        public void Process(float[] data, float[] wet, float gain, float damping)
        {
            for (int k=0; k<data.Length; k++)
            {
                float read = _delayBuffer[_delayedSampleIndex];
                _filteredSample = damping * (_filteredSample - read) + read + DC_OFFSET;

                float value = data[k] + _filteredSample * gain;
                _delayBuffer[_delayBufferIndex] = value;
                
                _delayBufferIndex = (_delayBufferIndex + 1) % _delayBuffer.Length;
                _delayedSampleIndex = (_delayedSampleIndex + 1) % _delayBuffer.Length;

                wet[k] += value;
            }
        }
    }
}