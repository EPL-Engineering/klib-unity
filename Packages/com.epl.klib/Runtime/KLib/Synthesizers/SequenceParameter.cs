
namespace KLibU.Synthesizers
{
    public enum SequenceOrder { Sequential, RandomNoRepeats, RandomWithRepeats} 

    internal class SequenceParameter
    {
        private int[] _intValues;
        private float[] _floatValues;
        private int _index;
        private int[] _indexOrder;
        private int _seqLength;
        private SequenceOrder _order;
        private System.Random _rn;

        public SequenceParameter()
        {
            _order = SequenceOrder.Sequential;

            _intValues = new int[1];
            _floatValues = new float[1];
            _seqLength = 1;

            _rn = new System.Random();
        }

        public void SetValues(SequenceOrder order, params int[] intValues)
        {
            _order = order;
            _intValues = intValues;
            _seqLength = _intValues.Length;

            Reset();
        }

        public void SetValues(SequenceOrder order, params float[] floatValues)
        {
            _order = order;
            _floatValues = floatValues;
            _seqLength = _floatValues.Length;

            Reset();
        }

        public void Reset()
        {
            _index = _seqLength - 1;
            Advance();
        }

        public int GetIntegerValue(int  index) => _intValues[index % _seqLength];
        public float GetFloatValue(int index) => _floatValues[index % _seqLength];

        public int GetIntegerValue()
        {
            int value = _intValues[GetIndex()];
            Advance();
            return value;
        }

        public float GetFloatValue()
        {
            float value = _floatValues[GetIndex()];
            Advance();
            return value;
        }

        private int GetIndex()
        {
            if (_order == SequenceOrder.RandomNoRepeats)
            {
                return _indexOrder[_index];
            }
            return _index;
        }

        private void Advance()
        {
            if (_order == SequenceOrder.Sequential)
            {
                _index = (_index + 1) % _seqLength;
            }
            else if (_order == SequenceOrder.RandomWithRepeats)
            {
                _index = _rn.Next(0, _seqLength);
            }
            else
            {
                _index++;
                if (_index == _seqLength)
                {
                    _indexOrder = Utilities.Permute(_seqLength);
                    _index = 0;
                }
            }
        }
    }
        
}