using System;
using UnityEngine;

namespace KLibU.Synthesizers
{
    public class Spatializer
    {
        private Track _track;
        private Reverb _reverb;
        private float _dmax;

        private float _Kild;

        public void Initialize(float dmax, Track track, Reverb reverb)
        {
            _dmax = dmax;
            _track = track;
            _reverb = reverb;

            _Kild = -0.18f * Mathf.Sqrt(8000) / 10f;
        }

        public void Process(float distance, float angle)
        {
            _track.Attenuation = -30 * distance / _dmax;
            _reverb.Mix = distance / _dmax;

            _track.Pan = Mathf.Sign(angle) / (1 + Mathf.Pow(10, _Kild * Mathf.Abs(Mathf.Sin(angle * Mathf.PI / 180f))));
        }

        public void Process(float angle)
        {
            _track.Pan = Mathf.Sign(angle) / (1 + Mathf.Pow(10, _Kild * Mathf.Abs(Mathf.Sin(angle * Mathf.PI / 180f))));
        }

        public Action<float> GetParamSetter(string paramName)
        {
            Action<float> setter = null;

            switch (paramName)
            {
                case "Level":
                    setter = x => _track.Attenuation = x;
                    break;
                case "Reverb":
                    setter = x => _reverb.Mix = x;
                    break;
            }
            return setter;
        }

    }
}