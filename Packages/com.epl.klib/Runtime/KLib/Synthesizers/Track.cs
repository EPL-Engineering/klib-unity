using System;
using UnityEngine;

namespace KLibU.Synthesizers
{
    public class Track
    {
        public string Name { get; set; }
        public float Attenuation { get; set; }
        public float Pan { get; set; }
        public bool Loop { get; set; }

        protected float _fs;
        protected int _bufferSize;

        protected int _channelOffsetL = 0;
        protected int _channelOffsetR = 0;

        protected float _lastPan;
        protected float _lastAtten;

        public float BPM { get; set; }

        public Track()
        {
            Attenuation = 0f;
            Pan = 0f;
            Loop = false;

            _channelOffsetL = 0;
            _channelOffsetR = 1;
        }

        public Track(int channelOffsetL, int channelOffsetR)
        {
            Attenuation = 0f;
            Pan = 0f;
            Loop = false;
            _channelOffsetL = channelOffsetL;
            _channelOffsetR = channelOffsetR;
        }

        public virtual SynthLog GetLog() { return null; }

        public virtual void Initialize(float Fs, int bufferSize)
        {
            _fs = Fs;
            _bufferSize = bufferSize;
        }

        public virtual void StartPlay()
        {
            _lastAtten = Attenuation;
            _lastPan = Pan;
        }

        public virtual void StopPlay() { }

        public virtual void Process(float[] beat, float[] data, int channels)
        {
            int index = 0;
            
            float deltaAtten = (Attenuation - _lastAtten) / _bufferSize;
            float deltaPan = (Pan - _lastPan) / _bufferSize;

            for (int k = 0; k < _bufferSize; k++)
            {
                float amplitude = Mathf.Pow(10, _lastAtten / 20);

                float pan_0to1 = (_lastPan + 1) / 2;
                float aright = Mathf.Sqrt(pan_0to1);
                float aleft = Mathf.Sqrt(1 - pan_0to1);

                data[index + _channelOffsetL] *= amplitude * aleft;
                if (_channelOffsetR > 0)
                {
                    data[index + _channelOffsetR] *= amplitude * aright;
                }
                index += channels;

                _lastAtten += deltaAtten;
                _lastPan += deltaPan;
            }
        }

        public virtual Action<float> GetParamSetter(string paramName)
        {
            switch (paramName)
            {
                case "Level":
                    return x => Attenuation = x;
            }
            return null;
        }

    }
}