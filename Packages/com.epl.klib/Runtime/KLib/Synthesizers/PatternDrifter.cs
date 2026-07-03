using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KLibU.Synthesizers
{
    public class PatternDrifter
    {
        private int _numItems;
        private int _patternLength;
        private int _repeatsBeforeChange;
        private bool _insertRest;

        private int[] _pattern;
        private int _index;
        private int _repeats;

        private System.Random _rn;

        public PatternDrifter()
        {
            Initialize(
                numItems: 1,
                patternLength: 4,
                repeatsBeforeChange: 2,
                insertRest: false);
        }

        public PatternDrifter(int numItems, int patternLength, int repeatsBeforeChange, bool insertRest)
        {
            Initialize(numItems, patternLength, repeatsBeforeChange, insertRest);
        }

        private void Initialize(int numItems, int patternLength, int repeatsBeforeChange, bool insertRest)
        {
            _numItems = numItems;
            _patternLength = patternLength;
            _repeatsBeforeChange = repeatsBeforeChange;
            _insertRest = insertRest;

            int n = _insertRest ? _patternLength + 1 : _patternLength;
            _pattern = new int[n];

            _rn = new System.Random();

            for (int k=0; k<_patternLength; k++)
            {
                _pattern[k] = _rn.Next(_numItems);
            }

            if (_insertRest)
            {
                _pattern[_patternLength] = -1;
            }

            _index = 0;
            _repeats = 0;
        }

        public int Advance()
        {
            int value = _pattern[_index];
            _index++;
            if (_index >= _pattern.Length)
            {
                _index = 0;
                _repeats++;
                if (_repeats >= _repeatsBeforeChange)
                {
                    int elementToChange = _rn.Next(_patternLength);
                    _pattern[elementToChange] = _rn.Next(_numItems);
                    _repeats = 0;
                }
            }
            return value;
        }
    }
}
