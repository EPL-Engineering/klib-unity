using System;
using UnityEngine;

namespace KLibU.Synthesizers
{
    public class ClipTrack : Track
    {
        public ClipManager ClipManager { get; private set; }
        public MIDI MIDI { get; private set; }
        public Sequencer Sequencer { get; private set; }

        public bool _isPlaying = false;

        public ClipTrack()
        {
            Name = "ClipTrack";
            ClipManager = new ClipManager();
            MIDI = new MIDI();
            Sequencer = new Sequencer();
        }

        public ClipTrack(int channelOffsetL, int channelOffsetR) : base(channelOffsetL, channelOffsetR)
        {
            Name = "InstrumentTrack";
            ClipManager = new ClipManager();
            MIDI = new MIDI();
            Sequencer = new Sequencer();
        }

        public override void Initialize(float Fs, int bufferSize)
        {
            base.Initialize(Fs, bufferSize);
            ClipManager.Initialize(Fs, bufferSize);
            Sequencer.Initialize(Fs, bufferSize);
        }

        public override SynthLog GetLog()
        {
            return ClipManager.GetLog(Name);
        }

        public override void StartPlay()
        {
            MIDI.Reset();
            Sequencer.Reset();
            _isPlaying = true;
            base.StartPlay();
        }

        public override void StopPlay()
        {
            ClipManager.StopAllNotes();
            _isPlaying = false;
        }

        public override void Process(float[] beat, float[] data, int channels)
        {
            if (_isPlaying)
            {
                var msgs = MIDI.GetMessages(beat[0], beat[^1]);
                for (int k = 0; k < msgs.Count; k++)
                {
                    ClipManager.ProcessMIDIMessage(msgs[k]);
                }

                msgs = Sequencer.Process(beat, BPM);
                for (int k = 0; k < msgs.Count; k++)
                {
                    ClipManager.ProcessMIDIMessage(msgs[k]);
                }
            }

            ClipManager.Process(beat);

            int index = 0;
            float deltaAtten = (Attenuation - _lastAtten) / _bufferSize;
            float deltaPan = (Pan - _lastPan) / _bufferSize;

            for (int k = 0; k < _bufferSize; k++)
            {
                float amplitude = Mathf.Pow(10, _lastAtten / 20);

                float pan_0to1 = (_lastPan + 1) / 2;
                float aright = Mathf.Sqrt(pan_0to1) / Mathf.Sqrt(0.5f);
                float aleft = Mathf.Sqrt(1 - pan_0to1) / Mathf.Sqrt(0.5f);

                pan_0to1 = (ClipManager.Pan[k] + 1) / 2;
                aright *= Mathf.Sqrt(pan_0to1) / Mathf.Sqrt(0.5f);
                aleft *= Mathf.Sqrt(1 - pan_0to1) / Mathf.Sqrt(0.5f);

                data[index + _channelOffsetL] += ClipManager.Data[k] * amplitude * aleft;
                if (_channelOffsetR > 0)
                {
                    data[index + _channelOffsetR] += ClipManager.Data[k] * amplitude * aright;
                }

                index += channels;

                _lastAtten += deltaAtten;
                _lastPan += deltaPan;
            }
        }

        public override Action<float> GetParamSetter(string paramName)
        {
            switch (paramName)
            {
                case "Rate":
                    return x => Sequencer.EventRate = x;
            }
            return base.GetParamSetter(paramName);
        }
    }
}