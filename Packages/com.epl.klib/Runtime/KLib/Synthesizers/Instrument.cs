using System;
using UnityEngine;

namespace KLibU.Synthesizers
{
    public class Instrument
    {
        public Oscillator Osc1 { get; private set; }
        public float Noise { get; set; }
        public Envelope AmplitudeEnvelope { get; private set; }
        public BiquadFilter Filter { get; private set; }
        public LFO LFO { get; private set; }
        public Reverb Reverb { get; private set; }
        public int NumVoices { get; set; }

        private float[] _data;
        public float[] Data { get { return _data; } }

        private int _noiseIndex;

        private float _clipDuration;
        private float[] _clipTime;

        public Instrument()
        {
            NumVoices = 1;
            Osc1 = new Oscillator();
            AmplitudeEnvelope = new Envelope();
            Filter = new BiquadFilter();
            LFO = new LFO();
            Reverb = new Reverb();

            Noise = 0;
        }

        public void Initialize(float Fs, int bufferSize)
        {
            _data = new float[bufferSize];
            Osc1.Initialize(Fs, bufferSize, NumVoices);
            AmplitudeEnvelope.Initialize(Fs, bufferSize, NumVoices);
            Filter.Initialize(Fs, bufferSize);
            LFO.Initialize(Fs, bufferSize);
            Reverb.Initialize(Fs, bufferSize);
        }

        public void AddLFOControl(string name, float minValue, float maxValue)
        {
            Action<float> action = null;
            switch (name)
            {
                case "Filter.Cutoff":
                    action = x => Filter.Cutoff = x;
                    break;
            }

            if (action != null)
            {
                LFO.AddControl(action, minValue, maxValue);
            }
        }

        public void ProcessMIDIMessage(MIDIMessage msg)
        {
            if (msg.messageType == MIDIMessage.MessageType.NoteOn)
            {
                StartNote(msg.beat, msg.note, msg.velocity, msg.duration);
            }
            else if (msg.messageType == MIDIMessage.MessageType.NoteOff)
            {
                StopNote(msg.beat,msg.note);
            }
        }

        public void StopAllNotes()
        {
            AmplitudeEnvelope.StopAllNotes();
        }

        private void StartNote(float time, int midiNum, float velocity, float duration)
        {
            int voiceNum = AmplitudeEnvelope.FindAvailableVoice();
            if (voiceNum > -1)
            {
                Osc1.SetFrequency(voiceNum, midiNum);
                AmplitudeEnvelope.StartNote(voiceNum, time, midiNum, velocity, duration);
            }
        }

        private void StopNote(float time, int midiNum)
        {
            int voiceNum = Osc1.FindVoiceNumber(midiNum);
            if (voiceNum > -1)
            {
                AmplitudeEnvelope.StopNote(voiceNum, time);
            }
        }

        public void Process(float[] time)
        {
            LFO.Process();
            Osc1.Process();
            AmplitudeEnvelope.Process(time);

            for (int k = 0; k < _data.Length; k++)
            {
                _data[k] = 0;
                for (int kv = 0; kv < NumVoices; kv++)
                {
                    float value = Osc1.Data[kv, k];

                    value += Noise * WaveTables.Noise[_noiseIndex++];
                    if (_noiseIndex >= WaveTables.Length) _noiseIndex = 0;

                    value *= AmplitudeEnvelope.Data[kv, k];

                    _data[k] += value;
                }
            }

            Filter.Process(_data);
            Reverb.Process(_data);
        }

        public int InitializeClip(float fs, float duration, float offtime)
        {
            _clipDuration = duration;

            int npts = Mathf.RoundToInt(fs * (duration + AmplitudeEnvelope.Release / 1000 + offtime/1000));
            Initialize(fs, npts);

            _clipTime = new float[npts];
            for (int k=0; k< npts; k++)
            {
                _clipTime[k] = k / fs;
            }

            return npts;
        }

        public float[] CreateClip(int midiNote)
        {
            _data = new float[_clipTime.Length];
            StartNote(0, midiNote, 1f, _clipDuration);
            Process(_clipTime);
            return _data;
        }
    }
}