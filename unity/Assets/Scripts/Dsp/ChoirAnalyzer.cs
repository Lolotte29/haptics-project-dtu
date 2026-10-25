using System;

namespace Haptics.Dsp
{
    /// <summary>Features of the whole ensemble.</summary>
    public struct ChoirFrame
    {
        /// <summary>Sensory dissonance between voices, 0 (consonant/none) .. 1 (very rough).</summary>
        public float Roughness;
        /// <summary>Loudest voice RMS.</summary>
        public float Loudness;
        /// <summary>True if any voice had an onset since the previous frame.</summary>
        public bool Onset;
        public int VoicedCount;
    }

    /// <summary>
    /// Combines per-singer pitches into a roughness value using Plomp-Levelt roughness (Sethares' parametrisation)
    /// over the harmonic partials of every pair of voiced singers.
    /// </summary>
    public static class ChoirAnalyzer
    {
        private const int Partials = 6;
        private const float PartialDecay = 0.8f; // amplitude ratio between successive partials
        /// <summary>Maps the weighted-average roughness (~0.03 for a minor second) onto roughly 0..1.</summary>
        public static float Scale = 25f;

        public static ChoirFrame Analyze(VoiceFrame[] voices, bool onset)
        {
            float loudness = 0f, sum = 0f, norm = 0f;
            int voiced = 0;

            for (int i = 0; i < voices.Length; i++)
            {
                if (voices[i].Rms > loudness) loudness = voices[i].Rms;
                if (voices[i].Voiced) voiced++;
            }

            for (int i = 0; i < voices.Length; i++)
            {
                if (!voices[i].Voiced) continue;
                for (int j = i + 1; j < voices.Length; j++)
                {
                    if (!voices[j].Voiced) continue;
                    float w = voices[i].Rms * voices[j].Rms;
                    float ampP = 1f;
                    for (int p = 1; p <= Partials; p++, ampP *= PartialDecay)
                    {
                        float ampQ = 1f;
                        for (int q = 1; q <= Partials; q++, ampQ *= PartialDecay)
                        {
                            float a = w * ampP * ampQ;
                            sum += a * Dissonance(voices[i].PitchHz * p, voices[j].PitchHz * q);
                            norm += a;
                        }
                    }
                }
            }

            float roughness = norm > 1e-12f ? Math.Min(1f, sum / norm * Scale) : 0f;
            return new ChoirFrame { Roughness = roughness, Loudness = loudness, Onset = onset, VoicedCount = voiced };
        }

        /// <summary>Roughness of two pure tones (unit amplitudes).</summary>
        public static float Dissonance(float f1, float f2)
        {
            float fmin = Math.Min(f1, f2);
            float df = Math.Abs(f1 - f2);
            float s = 0.24f / (0.0207f * fmin + 18.96f);
            return (float)(Math.Exp(-3.5 * s * df) - Math.Exp(-5.75 * s * df));
        }
    }
}
