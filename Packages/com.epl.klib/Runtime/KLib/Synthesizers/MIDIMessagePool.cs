using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KLibU.Synthesizers
{
    public class MIDIMessagePool
    {
        private readonly MIDIMessage[] _pool;
        private int _index;

        public MIDIMessagePool(int capacity)
        {
            _pool = new MIDIMessage[capacity];
            for (int k = 0; k < capacity; k++)
                _pool[k] = new MIDIMessage();
        }

        public MIDIMessage Get()
        {
            var msg = _pool[_index];
            _index = (_index + 1) % _pool.Length;
            return msg;
        }

        public void Reset() => _index = 0;
    }
}
