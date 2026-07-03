using System;
using System.Collections.Generic;
using UnityEngine;

namespace KLibU.Synthesizers
{
    public class ClipManager
    {
        public float Delay { get; set; }
        public Reverb Reverb { get; private set; }
        public int NumVoices { get; set; }
        public float CalibratedLevel { get; set; }

        private float[] _data;
        public float[] Data { get { return _data; } }

        private float[] _pan;
        public float[] Pan { get { return _pan; } }

        private float _fs;
        private int _bufferSize;
        private int _delaySamples;

        private class ClipData
        {
            public float[] data;
            public float level_at_fullscale;
        }
        private List<ClipData> _clips;

        private enum Phase { Delay, Sustain, Finished }

        private class VoiceData
        {
            public int noteNumber;
            public Phase phase;
            public float startTime;
            public bool startPending;
            public bool releasePending;
            public float velocity;
            public int durationSamples;
            public int samplesPlayed;
            public float calibratedLevelScaleFactor;
            public float pan;

            public VoiceData()
            {
                phase = Phase.Finished;
            }

            public void Reset()
            {
                phase = Phase.Finished;
                startPending = false;
                releasePending = false;
                velocity = 1;
                durationSamples = 0;
                samplesPlayed = 0;
                pan = 0;
            }
        }
        private List<VoiceData> _voices;

        private SynthLog _log;

        public ClipManager()
        {
            _clips = new List<ClipData>();
            Delay = 0;
            NumVoices = 1;
            Reverb = new Reverb();
            CalibratedLevel = -6f;
        }

        public void AddClip(float[] data, float levelAtFullScale)
        {
            ClipData clip = new ClipData();
            clip.data = data;
            clip.level_at_fullscale = levelAtFullScale;
            _clips.Add(clip);
        }

        public SynthLog GetLog(string name)
        {
            _log.Trim();
            _log.SetName(name);
            return _log;
        }

        public void Initialize(float Fs, int bufferSize)
        {
            _fs = Fs;
            _bufferSize = bufferSize;

            _delaySamples = Mathf.RoundToInt(Delay * Fs / 1000);

            _data = new float[bufferSize];
            _pan = new float[bufferSize];
            Reverb.Initialize(Fs, bufferSize);

            _voices = new List<VoiceData>();
            for (int k = 0; k < NumVoices; k++)
            {
                _voices.Add(new VoiceData());
            }

            _log = new SynthLog("ClipManager", "dspTime", "noteNumber", "duration", "pan");
        }

        public int FindAvailableVoice()
        {
            return _voices.FindIndex(x => x.phase == Phase.Finished && !x.startPending);
        }
        public int FindVoiceNumber(int midiNum)
        {
            return _voices.FindIndex(x => x.noteNumber == midiNum);
        }

        public void ProcessMIDIMessage(MIDIMessage msg)
        {
            if (msg.messageType == MIDIMessage.MessageType.NoteOn)
            {
                StartNote(msg.beat, msg.note, msg.velocity, msg.duration, msg.pan);
            }
            else if (msg.messageType == MIDIMessage.MessageType.NoteOff)
            {
                StopNote(msg.beat, msg.note);
            }
        }

        public void StopAllNotes()
        {
            foreach (var voice in _voices)
            {
                if (voice.phase != Phase.Finished)
                {
                    voice.releasePending = true;
                }
            }
        }

        private void StartNote(float time, int midiNum, float velocity, float duration, float pan)
        {
            int voiceNum = FindAvailableVoice();
            if (voiceNum > -1)
            {
                _voices[voiceNum].Reset();
                _voices[voiceNum].noteNumber = midiNum;
                _voices[voiceNum].startPending = true;
                _voices[voiceNum].startTime = time;
                _voices[voiceNum].velocity = velocity;
                _voices[voiceNum].durationSamples = _clips[midiNum].data.Length;
                _voices[voiceNum].samplesPlayed = 0;
                _voices[voiceNum].pan = pan;

                float atten = CalibratedLevel - _clips[midiNum].level_at_fullscale;
                _voices[voiceNum].calibratedLevelScaleFactor = Mathf.Pow(10, atten / 20);
            }
        }
        private void StopNote(float time, int midiNum)
        {
            int voiceNum = FindVoiceNumber(midiNum);
            if (voiceNum > -1)
            {
                _voices[voiceNum].releasePending = true;
            }
        }

        public void Process(float[] beat)
        {
            for (int k = 0; k < _data.Length; k++)
            {
                _data[k] = 0;
                _pan[k] = 0;
            }

            for (int kv = 0; kv < NumVoices; kv++)
            {
                ProcessVoice(beat, kv);
            }

            Reverb.Process(_data);
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
                    voice.phase = _delaySamples > 0 ? Phase.Delay : Phase.Sustain;
                    if (voice.phase == Phase.Sustain)
                    {
                        _log.Add((float)AudioSettings.dspTime + k / _fs, voice.noteNumber, voice.durationSamples / _fs, voice.pan);
                    }
                }

                if (voice.phase == Phase.Delay)
                {
                    value = 0;
                    voice.samplesPlayed++;
                    if (voice.samplesPlayed >= _delaySamples)
                    {
                        voice.samplesPlayed = 0;
                        voice.phase = Phase.Sustain;
                        _log.Add((float)AudioSettings.dspTime + k / _fs, voice.noteNumber, voice.durationSamples / _fs, voice.pan);
                    }
                }

                if (voice.releasePending)
                {
                    voice.releasePending = false;
                    voice.phase = Phase.Finished;
                }

                if (voice.phase == Phase.Finished) { }
                else if (voice.phase == Phase.Sustain)
                {
                    value = _clips[voice.noteNumber].data[voice.samplesPlayed];
                    voice.samplesPlayed++;
                    if (voice.samplesPlayed >= voice.durationSamples)
                    {
                        voice.phase = Phase.Finished;
                    }
                }

                _data[k] += voice.velocity * voice.calibratedLevelScaleFactor * value;
                _pan[k] = voice.pan;
            }
        }


    }
}