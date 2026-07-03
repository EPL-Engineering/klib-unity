using System.Collections.Generic;
using UnityEngine;

namespace KLibU.Synthesizers
{
    public enum Waveform { Sine, Triangle, Square, SawUp, SawDown}

    public class Oscillator
    {
        public float MixLevel { get; set; }
        public Waveform Waveform { get; set; }
        public int Harmonics { get; set; }
        public float CalibratedLevel { get; set; }  

        private float[,] _data;
        public float[,] Data { get  { return _data; } }

        private int _bufferSize;
        private int _numVoices;
        private int _numComponents;
        private int _harmonicInterval;

        private float _dBVrms_at_fullscale;
        private float _correction_for_using_F0_for_calibration;
        private float[] _level_at_0dB_Vrms = null;

        private class VoiceData
        {
            public int midiNum;
            public int[] phases;
            public int stepSize;
            public float calibratedLevelScaleFactor;

            public VoiceData(int numComponents)
            {
                phases = new int[numComponents];
            }
        }

        private List<VoiceData> _voices;
        private float[] _amplitudes;

        public Oscillator()
        {
            MixLevel = 1f;
            Waveform = Waveform.Sine;
            Harmonics = -1;
            CalibratedLevel = -6f;
        }

        public void Initialize(float Fs, int bufferSize, int numVoices)
        {
            _numVoices = numVoices;
            _bufferSize = bufferSize;

            _data = new float[_numVoices, bufferSize];

            _voices = new List<VoiceData>();

            _numComponents = Harmonics < 0 ? 1 : Harmonics;
            _amplitudes = new float[_numComponents];

            if (Waveform == Waveform.Sine)
            {
                _amplitudes[0] = 1;
                _dBVrms_at_fullscale = 20 * Mathf.Log10(1 / Mathf.Sqrt(2));
                _correction_for_using_F0_for_calibration = 0;
            }
            else if (Waveform == Waveform.Triangle)
            {
                if (_numComponents == 1)
                {
                    _amplitudes[0] = 1;
                    float power = 20 * Mathf.Log10(Mathf.Sqrt(1/3f));
                    float A0 = 8f / (Mathf.PI * Mathf.PI);
                    _dBVrms_at_fullscale = power;
                    _correction_for_using_F0_for_calibration = power - 20 * Mathf.Log10(Mathf.Sqrt(A0*A0/2));
                }
                else
                {
                    _harmonicInterval = 2;
                    int nc = 0;
                    float power = 0;
                    float sign = 1;
                    float A = 8f / (Mathf.PI * Mathf.PI);
                    for (int k = 0; k < _numComponents; k++)
                    {
                        float n = 2 * k + 1;

                        float Ak = sign * A / (n * n);
                        _amplitudes[nc++] = Ak;
                        sign = -sign;
                        power += (Ak * Ak) / 2;
                    }
                    _dBVrms_at_fullscale = 20*Mathf.Log10(Mathf.Sqrt(power));
                    _correction_for_using_F0_for_calibration = _dBVrms_at_fullscale - 20 * Mathf.Log10(Mathf.Sqrt(_amplitudes[0] * _amplitudes[0] / 2));
                }
            }
            else if (Waveform == Waveform.Square)
            {
                if (_numComponents == 1)
                {
                    _amplitudes[0] = 1;
                    float power = 20 * Mathf.Log10(Mathf.Sqrt(1));
                    float A0 = 4f / Mathf.PI;
                    _dBVrms_at_fullscale = power;
                    _correction_for_using_F0_for_calibration = power - 20 * Mathf.Log10(Mathf.Sqrt(A0 * A0 / 2));
                }
                else
                {
                    _harmonicInterval = 2;
                    int nc = 0;
                    float power = 0;
                    float A = 4f / Mathf.PI;
                    for (int k = 0; k < _numComponents; k++)
                    {
                        float n = 2 * k + 1;
                        float Ak = A / n;
                        _amplitudes[nc++] = Ak;
                        power += Ak * Ak / 2;
                    }
                    _dBVrms_at_fullscale = 20 * Mathf.Log10(Mathf.Sqrt(power));
                    _correction_for_using_F0_for_calibration = _dBVrms_at_fullscale - 20 * Mathf.Log10(Mathf.Sqrt(_amplitudes[0] * _amplitudes[0] / 2));
                }
            }
            else if (Waveform == Waveform.SawUp || Waveform == Waveform.SawDown)
            {
                if (_numComponents == 1)
                {
                    _amplitudes[0] = 1;
                    float power = 20 * Mathf.Log10(Mathf.Sqrt(1 / 3f));
                    float A0 = 2f / Mathf.PI;
                    _dBVrms_at_fullscale = power;
                    _correction_for_using_F0_for_calibration = power - 20 * Mathf.Log10(Mathf.Sqrt(A0 * A0 / 2));
                }
                else
                {
                    _harmonicInterval = 1;
                    int nc = 0;
                    float power = 0;
                    float A = 2f / Mathf.PI;
                    float sign = Waveform == Waveform.SawUp ?  1 : -1;
                    for (int k = 0; k < _numComponents; k++)
                    {
                        float Ak = sign * A / (k + 1);
                        _amplitudes[nc++] = Ak;
                        sign = -sign;
                        power += Ak * Ak / 2;
                    }
                    _dBVrms_at_fullscale = 20 * Mathf.Log10(Mathf.Sqrt(power));
                    _correction_for_using_F0_for_calibration = _dBVrms_at_fullscale - 20 * Mathf.Log10(Mathf.Sqrt(_amplitudes[0] * _amplitudes[0] / 2));
                }
            }

            for (int k = 0; k < _numVoices; k++)
            {
                var voiceData = new VoiceData(_numComponents);
                voiceData.midiNum = 69;
                voiceData.phases[0] = 0;
                voiceData.stepSize = Mathf.RoundToInt(440f / WaveTables.Resolution);
                _voices.Add(voiceData);
            }
        }

        public void SetLevelAt1Vrms(int midiNum, float value)
        {
            if (_level_at_0dB_Vrms == null)
            {
                _level_at_0dB_Vrms = new float[128];
                for (int k = 0; k < _level_at_0dB_Vrms.Length; k++) _level_at_0dB_Vrms[k] = float.NaN;
            }
            _level_at_0dB_Vrms[midiNum] = value;
        }

        public void SetFrequency(int voiceNum, int midiNum)
        {
            float freq = Utilities.MIDINoteToFrequency(midiNum);
            _voices[voiceNum].midiNum = midiNum;
            _voices[voiceNum].stepSize = Mathf.RoundToInt(freq / WaveTables.Resolution);

            // actual full scale level = (level at 0dB Vrms) - (dB Vmrs at full scale)
            float atten = CalibratedLevel - _dBVrms_at_fullscale;
            if (_level_at_0dB_Vrms != null)
            {
                atten -= _level_at_0dB_Vrms[midiNum] + _correction_for_using_F0_for_calibration;
            }
            _voices[voiceNum].calibratedLevelScaleFactor = Mathf.Pow(10, atten / 20);
        }

        public int FindVoiceNumber(int midiNum)
        {
            return _voices.FindIndex(x => x.midiNum == midiNum);   
        }

        public void Process()
        {
            if (Waveform == Waveform.Sine || _numComponents > 1)
            {
                for (int kv = 0; kv < _voices.Count; kv++)
                {
                    for (int k = 0; k < _bufferSize; k++)
                    {
                        _data[kv, k] = 0;
                        int stepScale = 1;
                        for (int kc = 0; kc < _numComponents; kc++)
                        {
                            _data[kv, k] += _amplitudes[kc] * WaveTables.Sine[_voices[kv].phases[kc]];
                            _voices[kv].phases[kc] += _voices[kv].stepSize * stepScale;
                            _voices[kv].phases[kc] = _voices[kv].phases[kc] % WaveTables.Length;
                            stepScale += _harmonicInterval;
                        }
                        _data[kv, k] *= MixLevel;
                        _data[kv, k] *= _voices[kv].calibratedLevelScaleFactor;
                    }
                }
            }
            else if (Waveform == Waveform.Triangle)
            {
                for (int kv = 0; kv < _numVoices; kv++)
                {
                    for (int k = 0; k < _bufferSize; k++)
                    {
                        _data[kv, k] = _amplitudes[0] * WaveTables.Triangle[_voices[kv].phases[0]];
                        _data[kv, k] *= MixLevel;
                        _data[kv, k] *= _voices[kv].calibratedLevelScaleFactor;
                        _voices[kv].phases[0] = (_voices[kv].phases[0] + _voices[kv].stepSize) % WaveTables.Length;
                    }
                }
            }
            else if (Waveform == Waveform.Square)
            {
                for (int kv = 0; kv < _numVoices; kv++)
                {
                    for (int k = 0; k < _bufferSize; k++)
                    {
                        _data[kv, k] = _amplitudes[0] * WaveTables.Square[_voices[kv].phases[0]];
                        _data[kv, k] *= MixLevel;
                        _data[kv, k] *= _voices[kv].calibratedLevelScaleFactor;
                        _voices[kv].phases[0] += _voices[kv].stepSize;
                        _voices[kv].phases[0] = _voices[kv].phases[0] % WaveTables.Length;
                    }
                }
            }
            else if (Waveform == Waveform.SawUp)
            {
                for (int kv = 0; kv < _numVoices; kv++)
                {
                    for (int k = 0; k < _bufferSize; k++)
                    {
                        _data[kv, k] = _amplitudes[0] * WaveTables.SawTooth[_voices[kv].phases[0]];
                        _data[kv, k] *= MixLevel;
                        _data[kv, k] *= _voices[kv].calibratedLevelScaleFactor;
                        _voices[kv].phases[0] += _voices[kv].stepSize;
                        _voices[kv].phases[0] = _voices[kv].phases[0] % WaveTables.Length;
                    }
                }
            }
            else if (Waveform == Waveform.SawDown)
            {
                for (int kv = 0; kv < _numVoices; kv++)
                {
                    for (int k = 0; k < _bufferSize; k++)
                    {
                        _data[kv, k] = -_amplitudes[0] * WaveTables.SawTooth[_voices[kv].phases[0]];
                        _voices[kv].phases[0] += _voices[kv].stepSize;
                        _data[kv, k] *= MixLevel;
                        _data[kv, k] *= _voices[kv].calibratedLevelScaleFactor;
                        _voices[kv].phases[0] = _voices[kv].phases[0] % WaveTables.Length;
                    }
                }
            }

        }
    }
}