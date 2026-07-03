using Microsoft.Graph;
using System.Collections.Generic;

using UnityEngine;

namespace KLibU.Synthesizers
{
    public class MIDI
    {
        public float Duration { get; set; }

        private List<MIDIMessage> _messages;
        private int _index = 0;
        private float _offset;

        private readonly MIDIMessagePool _msgPool = new MIDIMessagePool(4); // match your max polyphony
        private readonly List<MIDIMessage> _msgs = new List<MIDIMessage>(4);

        public MIDI()
        {
            _messages = new List<MIDIMessage>();
            Duration = 1;
        }

        public void Reset()
        {
            _index = 0;
            _offset = 0;
        }

        public List<MIDIMessage> GetMessages(float startTime, float endTime)
        {
            _msgs.Clear();
            _msgPool.Reset();

            while (_index < _messages.Count && _messages[_index].beat + _offset >= startTime && _messages[_index].beat + _offset <= endTime)
            {
                var msg = _msgPool.Get();
                msg.messageType = _messages[_index].messageType;
                msg.beat = _messages[_index].beat;
                msg.note = _messages[_index].note;
                msg.velocity = _messages[_index].velocity;
                msg.duration = _messages[_index].duration;
                _msgs.Add(msg);

                _index++;
                if (_index >= _messages.Count)
                {
                    _index = 0;
                    _offset += Duration;
                }
            }

            return _msgs;
        }

        public void AddMessage(MIDIMessage message)
        {
            _messages.Add(message);
        }

        public void AddMessage(MIDIMessage.MessageType type = MIDIMessage.MessageType.NoteOn, float time=0, int note=69, float velocity = 1, float duration=0)
        {
            _messages.Add(new MIDIMessage(type, time, note, velocity, duration, 0));
        }

        public void Clear()
        {
            _messages.Clear();
        }

    }
}
