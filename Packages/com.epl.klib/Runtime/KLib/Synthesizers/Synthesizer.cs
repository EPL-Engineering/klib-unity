using System;
using System.Collections.Generic;
using UnityEngine;

namespace KLibU.Synthesizers
{
    /// <summary>
    /// Represents a synthesizer that manages multiple tracks, processes audio buffers,
    /// and handles playback state and master level control.
    /// </summary>
    public class Synthesizer
    {
        /// <summary>
        /// Gets or sets the tempo in beats per minute.
        /// </summary>
        public float BPM { get; set; }

        /// <summary>
        /// Gets or sets the list of tracks managed by the synthesizer.
        /// </summary>
        public List<Track> Tracks { get; set; }

        /// <summary>
        /// Gets or sets the master output level in dB.
        /// </summary>
        public float MasterLevel { get; set; }

        private float _fs;
        private int _bufferSize;

        private float _dt;
        private double _beatsPerSample;

        private double _lastBeat;

        private int _channelOffsetL = 0;
        private int _channelOffsetR = 1;

        /// <summary>
        /// Gets the current beat position.
        /// </summary>
        public float Beat { get; private set; }

        private float[] _beat;

        private float _lastLevel;

        private bool _isPlaying;

        /// <summary>
        /// Gets a value indicating whether the synthesizer is currently playing.
        /// </summary>
        public bool IsPlaying { get { return _isPlaying; } }

        /// <summary>
        /// Initializes a new instance of the <see cref="Synthesizer"/> class with default settings.
        /// </summary>
        public Synthesizer()
        {
            BPM = 90;
            Tracks = new List<Track>();
            MasterLevel = -3;
        }

        /// <summary>
        /// Initializes the synthesizer with the specified sample rate and buffer size.
        /// </summary>
        /// <param name="Fs">The sample rate in Hz.</param>
        /// <param name="bufferSize">The size of the audio buffer.</param>
        public void Initialize(float Fs, int bufferSize, int channelOffsetL, int channelOffsetR = 1)
        {
            _fs = Fs;
            _bufferSize = bufferSize;
            _channelOffsetL = channelOffsetL;
            _channelOffsetR = channelOffsetR;

            _dt = 1f / Fs;
            _beat = new float[bufferSize];
            _beatsPerSample = BPM / 60f / _fs;

            _lastLevel = MasterLevel;

            WaveTables.Initialize(Fs, 10);

            for (int k = 0; k < Tracks.Count; k++)
            {
                Tracks[k].Initialize(Fs, bufferSize);
            }

            _isPlaying = false;
        }

        /// <summary>
        /// Starts playback of the synthesizer and all tracks.
        /// </summary>
        public void StartPlay()
        {
            _lastBeat = 0f;
            _beatsPerSample = BPM / 60f / _fs;

            _lastLevel = MasterLevel;

            for (int k=0; k<Tracks.Count; k++)
            {
                Tracks[k].StartPlay();
            }
            _isPlaying = true;
        }

        /// <summary>
        /// Stops playback of the synthesizer and all tracks.
        /// </summary>
        public void StopPlay()
        {
            _isPlaying = false;
            for (int k = 0; k < Tracks.Count; k++)
            {
                Tracks[k].StopPlay();
            }
        }

        /// <summary>
        /// Processes the audio buffer for the current frame, applying beat timing and master level.
        /// </summary>
        /// <param name="data">The audio buffer to process.</param>
        /// <param name="channels">The number of audio channels.</param>
        public void Process(float[] data, int channels)
        {
            double targetBeatsPerSample = BPM / 60f / _fs;
            double deltaBeatsPerSample = (targetBeatsPerSample - _beatsPerSample) / _bufferSize;

            for (int k = 0; k < _bufferSize; k++)
            {
                _beat[k] = (float)_lastBeat;
                _beatsPerSample += deltaBeatsPerSample;
                _lastBeat += _beatsPerSample;
            }

            if (_isPlaying)
            {
                Beat = (float)_lastBeat;
            }

            for (int k=0; k < Tracks.Count; k++)
            {
                Tracks[k].BPM = BPM;
                Tracks[k].Process(_beat, data, channels);
            }

            int index = 0;

            float deltaLevel = (MasterLevel - _lastLevel) / _bufferSize;

            for (int k = 0; k < _bufferSize; k++)
            {
                float amplitude = Mathf.Pow(10, _lastLevel / 20);

                data[index + _channelOffsetL] *= amplitude;
                
                if (_channelOffsetR > -1)
                    data[index + _channelOffsetR] *= amplitude;
    
                index += channels;

                _lastLevel += deltaLevel;
            }
        }

        public Action<float> GetParamSetter(string paramName)
        {
            Action<float> setter = null;

            switch (paramName)
            {
                case "BPM":
                    setter = x => BPM = x;
                    break;
            }
            return setter;
        }

    }
}