using System.Collections.Generic;
using UnityEngine;

namespace KLibU.Synthesizers
{
    public class Envelope
    {
        private enum Phase { Delay, Attack, Decay, Sustain, Release, Finished}

        public float Attack {  get; set; }
        public float Decay { get; set; }
        public float Sustain { get; set; }
        public float Release { get; set; }
        public float Delay { get; set; }

        private float _fs;
        private int _bufferSize;

        private int _numVoices;
        private float _gamma = 1.5f;

        private int _delaySamples;
        private float[] _attack;
        private float[] _decay;
        private float[] _release;

        private float[,] _data;
        public float[,] Data { get { return _data; } }

        private SynthLog _log;

        private class VoiceData
        {
            public int index;
            public int noteNumber;
            public Phase phase;
            public float lastValue;
            public float releaseStart;
            public float startTime;
            public bool startPending;
            public float releaseTime;
            public bool releasePending;
            public float velocity;
            public int durationSamples;
            public int samplesPlayed;

            public VoiceData() 
            {
                phase = Phase.Finished;
            }

            public void Reset()
            {
                lastValue = 0;
                phase = Phase.Finished;
                index = 0;
                startPending = false;
                releasePending = false;
                velocity = 1;
                durationSamples = 0;
                samplesPlayed = 0;
            }
        }

        private List<VoiceData> _voices;

        public Envelope()
        {
            Attack = 2;
            Decay = 10;
            Sustain = 1;
            Release = 20;
            Delay = 0;
        }

        public SynthLog GetLog(string name)
        {
            _log.Trim();
            _log.SetName(name);
            return _log;
        }

        public void Initialize(float fs, int bufferSize, int numVoices)
        {
            _fs = fs;
            _bufferSize = bufferSize;
            _numVoices = numVoices;

            _delaySamples = Mathf.RoundToInt(Delay * fs / 1000);    
            _attack = CreateSegment(_fs, Attack, _gamma);
            _decay = CreateSegment(_fs, Decay, _gamma);
            _release = CreateSegment(_fs, Release, _gamma);

            _voices = new List<VoiceData>();
            for (int k = 0; k < _numVoices; k++)
            {
                _voices.Add(new VoiceData());
            }

            _data = new float[_numVoices, bufferSize];
            _log = new SynthLog("Envelope", "dspTime", "noteNumber", "duration");
        }

        public int FindAvailableVoice()
        {
            return _voices.FindIndex(x => x.phase == Phase.Finished && !x.startPending);
        }

        public void StartNote(int voiceNum, float time, int noteNumber, float velocity, float duration)
        {
            _voices[voiceNum].Reset();
            _voices[voiceNum].noteNumber = noteNumber;
            _voices[voiceNum].startPending = true;
            _voices[voiceNum].startTime = time;
            _voices[voiceNum].velocity = velocity;
            _voices[voiceNum].durationSamples = Mathf.RoundToInt(duration * _fs);
            _voices[voiceNum].samplesPlayed = 0;
        }

        public void StopNote(int voiceNum, float time)
        {
            _voices[voiceNum].releasePending = true;
            _voices[voiceNum].releaseTime = time;
        }

        public void StopAllNotes()
        {
            foreach (var voice in  _voices)
            {
                if (voice.phase != Phase.Finished)
                {
                    voice.releasePending = true;
                    voice.releaseTime = 0;
                }
            }
        }

        public void Process(float[] beat)
        {
            for (int kv = 0; kv < _numVoices; kv++)
            {
                ProcessVoice(beat, kv);
            }
        }

        public void ProcessVoice(float[] beat, int voiceNum)
        {
            var voice = _voices[voiceNum];
            float value = 0;

            for (int k = 0; k < _bufferSize; k++)
            {
                if (voice.startPending && beat[k] >= voice.startTime)
                {
                    voice.startPending = false;
                    voice.index = 0;
                    voice.phase = _delaySamples > 0 ? Phase.Delay : Phase.Attack;
                    if (voice.phase == Phase.Attack)
                    {
                        _log.Add((float)AudioSettings.dspTime + k/_fs, voice.noteNumber, voice.durationSamples / _fs);
                    }   
                }

                if (voice.phase == Phase.Delay)
                {
                    value = 0;
                    voice.samplesPlayed++;
                    if (voice.samplesPlayed >= _delaySamples)
                    {
                        voice.samplesPlayed = 0;
                        voice.phase = Phase.Attack;
                        _log.Add((float)AudioSettings.dspTime + k/_fs, voice.noteNumber, voice.durationSamples / _fs);
                    }
                }

                if (voice.releasePending && beat[k] >= voice.releaseTime)
                {
                    voice.releasePending = false;
                    voice.releaseStart = voice.lastValue;
                    voice.index = 0;
                    voice.phase = Phase.Release;
                }

                if (voice.phase != Phase.Release && voice.durationSamples > 0 && voice.samplesPlayed >= voice.durationSamples)
                {
                    voice.releaseStart = voice.lastValue;
                    voice.index = 0;
                    voice.phase = Phase.Release;
                }

                if (voice.phase == Phase.Finished) { }
                else if (voice.phase == Phase.Attack)
                {
                    value = _attack[voice.index++];
                    voice.samplesPlayed++;

                    if (voice.index >= _attack.Length)
                    {
                        voice.index = 0;
                        voice.phase = (Sustain < 1) ? Phase.Decay : Phase.Sustain;
                    }
                }
                else if (voice.phase == Phase.Decay)
                {
                    value = 1 + (Sustain - 1) * _decay[voice.index++];
                    voice.samplesPlayed++;

                    if (voice.index >= _decay.Length)
                    {
                        voice.index = 0;
                        voice.phase = Phase.Sustain;
                    }
                }
                else if (voice.phase == Phase.Sustain)
                {
                    value = Sustain;
                    voice.samplesPlayed++;
                }
                else if (voice.phase == Phase.Release)
                {
                    value = voice.releaseStart - voice.releaseStart * _release[voice.index++];

                    if (voice.index >= _release.Length)
                    {
                        voice.index = 0;
                        voice.phase = Phase.Finished;
                        voice.durationSamples = 0;
                    }
                }
                _data[voiceNum, k] = voice.velocity * value;
                voice.lastValue = value;
            }
        }

        private float[] CreateSegment(float Fs, float duration, float gamma = 1.5f)
        {
            float dt = 1 / Fs;
            int npts = Mathf.RoundToInt(Fs * duration / 1000);
            float[] segment = new float[npts];

            float tauScale = 0.001f / Mathf.Log(gamma / (gamma - 1));

            float tau = duration * tauScale;

            float y0 = 0;
            float y1 = 1;
            float delta_y = y1 - y0;

            for (int k = 0; k < npts; k++)
            {
                segment[k] = y0 + gamma * delta_y * (1 - Mathf.Exp(-k * dt / tau));
            }

            return segment;
        }

    }
}