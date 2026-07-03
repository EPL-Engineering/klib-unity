namespace KLibU.Synthesizers
{
    public class MIDIMessage
    {
        public enum MessageType { NoteOn, NoteOff}

        public MessageType messageType;
        public float beat;
        public int note;
        public float velocity;
        public float duration;
        public float pan;

        public MIDIMessage()
        {
            messageType = MessageType.NoteOff;
            beat = 0;
            note = 69;
            velocity = 1f;
            duration = 0;
            pan = 0f;
        }
        public MIDIMessage(MessageType messageType, float beat, int note, float velocity, float duration, float pan)
        {
            this.messageType = messageType;
            this.beat = beat;
            this.note = note;
            this.velocity = velocity;
            this.duration = duration;
            this.pan = pan;
        }
    }
}