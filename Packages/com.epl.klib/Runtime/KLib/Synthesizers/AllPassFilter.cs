using UnityEngine;

namespace KLibU.Synthesizers
{
    /// <summary>
    /// Implements an all-pass filter for audio signal processing.
    /// An all-pass filter passes all frequencies equally in gain, but changes the phase relationship among various frequencies.
    /// It's use here is primarily to create a more diffuse reverb effect by adding additional reflections without altering the frequency response of the signal.
    /// </summary>
    public class AllPassFilter
    {
        private float[] _delayBuffer;
        private int _delayBufferIndex;
        private int _delayedSampleIndex;
        private int _delaySamples;

        private const float DC_OFFSET = 1e-25f;

        /// <summary>
        /// Initializes a new instance of the <see cref="AllPassFilter"/> class.
        /// </summary>
        /// <param name="Fs">The sampling frequency in Hz.</param>
        /// <param name="delay">The delay time in seconds.</param>
        public AllPassFilter(float Fs, float delay)
        {
            _delaySamples = Mathf.RoundToInt(Fs * delay);
            _delayBuffer = new float[_delaySamples];

            _delayBufferIndex = 0;
            _delayedSampleIndex = 1;
        }

        /// <summary>
        /// Processes the input audio data in-place using the all-pass filter.
        /// </summary>
        /// <param name="data">The audio data buffer to process.</param>
        public void Process(float[] data)
        {
            for (int k = 0; k < data.Length; k++)
            {
                float read = _delayBuffer[_delayedSampleIndex];
                _delayBuffer[_delayBufferIndex] = data[k] + read * 0.5f + DC_OFFSET;
                data[k] = read - data[k];

                _delayBufferIndex = (_delayBufferIndex + 1) % _delayBuffer.Length;
                _delayedSampleIndex = (_delayedSampleIndex + 1) % _delayBuffer.Length;
            }
        }

    }
}