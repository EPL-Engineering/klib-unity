using System.Collections.Generic;
using UnityEngine;

namespace KLibU.Synthesizers
{
    public static class Utilities
    {
        public static float MIDINoteToFrequency(int midi)
        {
            return 440 * Mathf.Pow(2, (float)(midi - 69) / 12);
        }

        public static int FrequencyToMIDINote(float frequency)
        {
            return Mathf.RoundToInt(69 + 12 / Mathf.Log(2) * Mathf.Log(frequency / 440));
        }

        public static int MusicalNoteToMidi(string note)
        {
            return FrequencyToMIDINote(MusicalNoteToFrequency(note));
        }

        public static int[] MusicalNotesToMidi(params string[] notes)
        {
            int[] midi = new int[notes.Length];
            for (int k=0; k<notes.Length; k++)
            {
                midi[k] = MusicalNoteToMidi(notes[k]);
            }
            return midi;
        }

        public static float MusicalNoteToFrequency(string note)
        {
            int[] semitones = new int[] { 0, 2, 3, 5, 7, 8, 10 };
            float A4 = 440;

            string accidental = null;
            int noteNumber = note[0] - 'A';
            int octave = int.Parse(note.Substring(note.Length - 1));
            if (noteNumber > 1)
            {
                octave--;
            }

            int increment = 0;
            if (note.Length > 2)
            {
                accidental = note.Substring(1, 1);
                if (!string.IsNullOrEmpty(accidental))
                {
                    if (accidental.Equals("#"))
                    {
                        increment++;
                    }
                    else if (accidental.Equals("b"))
                    {
                        increment--;
                    }
                }
            }

            float frequency = A4 * Mathf.Pow(2f, (octave - 4) + (float)(semitones[noteNumber] + increment) / 12);
            //Debug.Log($"{note}: {octave} {noteNumber} {semitones[noteNumber] + increment} => {frequency}");

            return frequency;
        }

        public static int[] Permute(int N, int numElements)
        {
            int nrepeats = Mathf.CeilToInt((float)numElements / N);
            int[] list = new int[nrepeats * N];
            int idx = 0;
            for (int k = 0; k < nrepeats; k++)
            {
                foreach (int i in Permute(N)) list[idx++] = i;
            }

            int[] trimmed = new int[numElements];
            for (int k = 0; k < numElements; k++) trimmed[k] = list[k];

            return trimmed;
        }

        public static int[] Permute(int N)
        {
            System.Random r = new System.Random();
            int[] list = new int[N];
            for (int k = 0; k < N; k++)
                list[k] = k;

            int max = N - 1;
            for (int k = 0; k < N; k++)
            {
                int idx = r.Next(0, max + 1);
                int temp = list[idx];
                list[idx] = list[max];
                list[max] = temp;
                --max;
            }

            return list;
        }

        public static int[] ReadMidiFile(string path)
        {
            byte[] raw = System.IO.File.ReadAllBytes(path);

            List<int> noteNumbers = new List<int>();

            for (int i = 0; i < raw.Length - 2; i++)
            {
                // Note On events: status byte is 0x90–0x9F (channel 0–15)
                if (raw[i] >= 0x90 && raw[i] <= 0x9F)
                {
                    byte note = raw[i + 1];  // Note number (0–127)
                    byte velocity = raw[i + 2];  // Velocity (0 = note off in disguise)

                    if (velocity > 0)
                        noteNumbers.Add(note);
                }
            }

            return noteNumbers.ToArray();
        }

    }
}