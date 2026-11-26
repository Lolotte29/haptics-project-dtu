using System;

namespace Haptics.Dsp
{
    /// <summary>Pure pitch maths: Hz to MIDI note number and pitch class (0 = C .. 11 = B).</summary>
    public static class PitchClass
    {
        private static readonly string[] Names = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

        /// <summary>Continuous MIDI note number (A4 = 440 Hz = 69). Hz must be positive.</summary>
        public static double HzToMidi(double hz) => 69.0 + 12.0 * Math.Log(hz / 440.0, 2.0);

        /// <summary>Nearest integer MIDI note.</summary>
        public static int NearestNote(double midi) => (int)Math.Floor(midi + 0.5);

        /// <summary>Pitch class 0..11 of an integer MIDI note (negative notes wrap correctly).</summary>
        public static int Of(int midiNote) => ((midiNote % 12) + 12) % 12;

        /// <summary>Signed distance of a continuous MIDI value to its nearest note, in cents (-50..+50).</summary>
        public static double CentsOffNearest(double midi) => (midi - NearestNote(midi)) * 100.0;

        public static string Name(int pitchClass) => Names[Of(pitchClass)];
    }
}
