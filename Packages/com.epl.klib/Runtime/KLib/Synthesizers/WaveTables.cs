using UnityEngine;

namespace KLibU.Synthesizers
{
    public static class WaveTables
    {
        public static float Resolution;
        public static int Length;

        public static float[] Sine;
        public static float[] Triangle;
        public static float[] Square;
        public static float[] SawTooth;
        public static float[] Noise;

        public static void Initialize(float Fs, float dur)
        {
            Resolution = 1 / dur;
            Length = Mathf.RoundToInt(Fs * dur);
            float dt = 1 / Fs;

            Sine = new float[Length];
            Triangle = new float[Length];
            Square = new float[Length];
            SawTooth = new float[Length];
            Noise = new float[Length];

            float theta = 0;
            for (int k=0; k< Length; ++k)
            {
                Sine[k] = Mathf.Sin(2 * Mathf.PI * theta);
                Triangle[k] = 4 * Mathf.Abs(((theta - 1 / 4) % 1) - 0.5f) - 1;
                Square[k] = Mathf.Sign(Sine[k]);
                SawTooth[k] = 2 * (theta - Mathf.Floor(theta + 0.5f));

                Noise[k] = Random.Range(-1f, 1f);

                theta += dt / dur;
            }
        }
    }
}