using System.Collections.Generic;

using UnityEngine;

namespace KLibU.Synthesizers
{
    public enum EventOrder { Fixed, ShuffleEachCycle, FixedRate, FixedProbability}

    public class Sequencer
    {
        public bool Active { get; set; }
        public float Rotation { get; set; }
        public int Events { get; set; }
        public int Steps { get; set; }
        public EventOrder EventOrder { get; set; }
        public float EventRate { get; set; }
        public PatternDrifter PatternDrifter = null;

        private bool[] _isStepAnEvent;
        public bool[] IsStepAnEvent { get { return _isStepAnEvent; } }

        private float _cycleLength;
        public float CycleLength { get { return _cycleLength; } }

        private float _beatsPerStep = 0.5f;
        public float BeatsPerStep {  get { return _beatsPerStep; } }

        private float[] _nominalStepBeats;
        private float[] _stepBeats;
        public float[] StepBeats { get { return _stepBeats; } }

        private float _lastRotation;

        private float _cumulativeTime;
        private float _nextEventTime;
        private float _frameTime;

        private System.Random _rng = new System.Random();

        private SequenceParameter _notes = new SequenceParameter();
        private SequenceParameter _velocities = new SequenceParameter();
        private SequenceParameter _durations = new SequenceParameter();
        private SequenceParameter _pans = new SequenceParameter();

        private int _currentCycle;
        public int CurrentCycle { get { return _currentCycle; } }

        private readonly MIDIMessagePool _msgPool = new MIDIMessagePool(4); // match your max polyphony
        private readonly List<MIDIMessage> _msgs = new List<MIDIMessage>(4);

        public Sequencer()
        {
            Active = false;
            Rotation = 0;
            Events = 4;
            Steps = 16;
            EventOrder = EventOrder.Fixed;
            EventRate = 1f;

            _notes.SetValues(SequenceOrder.Sequential, 69);
            _velocities.SetValues(SequenceOrder.Sequential, 1f);
            _durations.SetValues(SequenceOrder.Sequential, 0.1f);
            _pans.SetValues(SequenceOrder.Sequential, 0f);
        }

        public void SetEvents(bool setAll = true)
        {
            _isStepAnEvent = new bool[Steps];
            for (int k = 0; k < Steps; k++)
            {
                _isStepAnEvent[k] = setAll;
            }
        }

        public void SetEvents(params int[] stepNumbers)
        {
            _isStepAnEvent = new bool[Steps];
            foreach (int i in stepNumbers)
            {
                _isStepAnEvent[i] = true;
            }
        }

        public void Randomize(int numEvents, bool shuffle=false)
        {
            Events = numEvents;
            EventOrder = shuffle ? EventOrder.ShuffleEachCycle : EventOrder.Fixed;

            _isStepAnEvent = new bool[Steps];

            foreach (var i in Utilities.Permute(Steps, Events))
            {
                _isStepAnEvent[i] = true;
            }
        }

        public void Initialize(float Fs, int bufferSize)
        {
            _cycleLength = Steps * _beatsPerStep;

            _nominalStepBeats = new float[Steps];
            _stepBeats = new float[Steps];

            for (int k=0; k<Steps; k++)
            {
                _nominalStepBeats[k] = k * _beatsPerStep;
                _stepBeats[k] = _nominalStepBeats[k];
            }

            _lastRotation = 0;
            _currentCycle = 0;

            _cumulativeTime = 0;
            _nextEventTime = 0;
            _frameTime = bufferSize / Fs;
        }

        public void SetNote(int note)
        {
            _notes.SetValues(SequenceOrder.Sequential, note);
        }

        public void SetNotes(SequenceOrder order, params int[] notes)
        {
            _notes.SetValues(order, notes);
        }

        public void SetNotes(SequenceOrder order, params string[] notes)
        {
            _notes.SetValues(order, Utilities.MusicalNotesToMidi(notes));
        }

        public void SetVelocityRangeDB(SequenceOrder order, float minVal, float stepVal, float maxVal)
        {
            int n = Mathf.FloorToInt((float)(maxVal - minVal) / stepVal) + 1;

            float[] velocity = new float[n];
            for (int k = 0; k < n; k++)
            {
                velocity[k] = Mathf.Pow(10, (minVal + k * stepVal) / 20f);
            }

            _velocities.SetValues(order, velocity);
        }

        public void SetDurations(SequenceOrder order, float durations)
        {
            _durations.SetValues(order, durations);
        }

        public void SetPans(SequenceOrder order, float[] pans)
        {
            _pans.SetValues(order, pans);
        }

        public void Reset()
        {
            _currentCycle = 0;
            _notes.Reset();
            _velocities.Reset();
            _durations.Reset();
            _pans.Reset();
        }

        private void ShuffleEvents()
        {
            for (int k=0; k < Steps; k++) _isStepAnEvent[k] = false;

            foreach (var i in Utilities.Permute(Steps, Events))
            {
                _isStepAnEvent[i] = true;
            }

            for (int k = 0; k < Steps; k++)
            {
                _nominalStepBeats[k] = k * _beatsPerStep;
                _stepBeats[k] = _nominalStepBeats[k];
            }
        }

        private void UpdateRotation(float rotation)
        {
            if (rotation != _lastRotation)
            {
                float offset = rotation * _cycleLength;

                for (int k=0; k<Steps; k++)
                {
                    _stepBeats[k] = (_nominalStepBeats[k] + offset) % _cycleLength;
                    if (_stepBeats[k] < 0)
                    {
                        _stepBeats[k] += _cycleLength;
                    }
                }
                _lastRotation = rotation;
            }
        }

        public List<MIDIMessage> Process(float[] beat, float BPM)
        {
            _msgs.Clear();
            _msgPool.Reset();

            float numCycles = beat[^1] / _cycleLength;
            if (EventOrder == EventOrder.ShuffleEachCycle && Mathf.FloorToInt(numCycles) > _currentCycle)
            {
                ShuffleEvents();
                _currentCycle = Mathf.FloorToInt(numCycles);
            }

            UpdateRotation(Rotation);

            if (!Active) return _msgs;
            
            float bmin = beat[0] % _cycleLength;
            float bmax = beat[^1] % _cycleLength;

            if (bmin > bmax)
            {
                bmin -= _cycleLength;
            }

            for (int k=0; k<Steps; k++)
            {
                float bnext = _stepBeats[k];
                bool thisFrameContainsBeat = (bnext >= bmin && bnext < bmax);

                if (EventOrder == EventOrder.FixedRate)
                {
                    bool eventDue = _cumulativeTime >= _nextEventTime;
                    _isStepAnEvent[k] = thisFrameContainsBeat && eventDue;
                    if (_isStepAnEvent[k])
                    {
                        _nextEventTime += 1 / EventRate;
                    }
                }
                else if (EventOrder == EventOrder.FixedProbability)
                {
                    float eventProbability = _beatsPerStep * 60 / BPM * EventRate;
                    _isStepAnEvent[k] = thisFrameContainsBeat && _rng.NextDouble() < eventProbability;
                }

                if (_isStepAnEvent[k])
                {
                    if (thisFrameContainsBeat)
                    {
                        var msg = _msgPool.Get();
                        msg.messageType = MIDIMessage.MessageType.NoteOn;
                        msg.beat = beat[0] + (bnext - bmin);
                        if (PatternDrifter != null)
                        {
                            int patternIndex = PatternDrifter.Advance();
                            if (patternIndex >= 0)
                            {
                                msg.note = _notes.GetIntegerValue(patternIndex);
                                msg.velocity = _velocities.GetFloatValue(patternIndex);
                                msg.duration = _durations.GetFloatValue(patternIndex);
                            }
                        }
                        else
                        {
                            msg.note = _notes.GetIntegerValue();
                            msg.velocity = _velocities.GetFloatValue();
                            msg.duration = _durations.GetFloatValue();
                        }
                        _msgs.Add(msg);
                    }
                }            
            }

            _cumulativeTime += _frameTime;

            return _msgs;
        }
    }
}