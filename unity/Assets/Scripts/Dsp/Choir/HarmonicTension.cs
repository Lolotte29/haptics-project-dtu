using System;

namespace Haptics.Dsp
{
    /// <summary>Result of <see cref="HarmonicTension.Compute"/>. All values 0..1.</summary>
    public struct TensionResult
    {
        /// <summary>Combined tension: distance from the key centre and dissonance of the current notes.</summary>
        public float Tension;
        /// <summary>How far the current pitch classes are from the key's tonal centre (0 = on it). 0 when no key is known.</summary>
        public float KeyDistance;
        /// <summary>Scaled (1 - length of the tonal vector): 0 for open or triadic sounds, 1 for clusters of semitones.</summary>
        public float Dissonance;
        /// <summary>False when nothing is sounding (the other values are 0).</summary>
        public bool Active;
        /// <summary>True when a key was available, so <see cref="KeyDistance"/> contributed.</summary>
        public bool UsedKey;
    }

    /// <summary>
    /// Harmonic tension from the Tonal Interval Space (Bernardes et al. 2016): a 12-bin pitch-class vector is mapped to
    /// the DFT coefficients 1..6 (each scaled by a consonance weight), giving one point in a 12-dimensional space.
    /// Tension combines (a) the distance of that point from the key's point (how remote the notes are from the tonal centre)
    /// and (b) its distance from the space's edge (dissonance: spread-out, semitone-heavy sets are short vectors).
    /// No chord labels are needed, so incomplete or doubled chords are fine. Western tonal music only.
    /// The constants are first guesses to be tuned by ear on real recordings. No allocation after construction.
    /// </summary>
    public sealed class HarmonicTension
    {
        // Weights for DFT coefficients 1..6 (Bernardes et al. 2016, from consonance ratings of dyads and triads).
        private static readonly double[] W = { 3.0, 8.0, 11.5, 15.0, 14.5, 7.5 };

        /// <summary>Histogram total (weighted voice-seconds) below which nothing is considered to be sounding.</summary>
        public float MinEvidence { get; set; } = 0.05f;
        /// <summary>Weight of the key distance in <see cref="TensionResult.Tension"/>; the dissonance gets 1 minus this.</summary>
        public float KeyWeight { get; set; } = 0.6f;
        /// <summary>Key distance (in units of the maximum possible vector length) that counts as full tension.
        /// Measured: tonic triad 0, relative minor ~0.5, IV and V ~0.78, a triad a tritone away ~0.96.</summary>
        public float FullKeyDistance { get; set; } = 1.0f;
        /// <summary>Raw dissonance up to which nothing is added. Measured: one note 0, open fifth 0.27, any major/minor triad 0.40.</summary>
        public float DissonanceFloor { get; set; } = 0.30f;
        /// <summary>Raw dissonance that counts as full tension. Measured: dominant seventh 0.49, diminished seventh 0.43, three adjacent semitones 0.68.</summary>
        public float FullDissonance { get; set; } = 0.70f;

        private readonly double[] _cos = new double[6 * 12];
        private readonly double[] _sin = new double[6 * 12];
        private readonly double _maxNorm;
        private readonly double[] _keyRe = new double[24 * 6]; // T of each key's tonic triad; index = (tonic + 12 * minor) * 6 + k
        private readonly double[] _keyIm = new double[24 * 6];
        private readonly double[] _re = new double[6];
        private readonly double[] _im = new double[6];

        public HarmonicTension()
        {
            double m = 0;
            for (int k = 0; k < 6; k++)
            {
                m += W[k] * W[k];
                for (int n = 0; n < 12; n++)
                {
                    _cos[k * 12 + n] = Math.Cos(2.0 * Math.PI * (k + 1) * n / 12.0);
                    _sin[k * 12 + n] = -Math.Sin(2.0 * Math.PI * (k + 1) * n / 12.0);
                }
            }
            _maxNorm = Math.Sqrt(m);

            // Reference point of each key: its tonic triad. (The key profile used by KeyEstimator is a poor reference here:
            // it is spread over the scale, so its vector is short and every short vector, e.g. a cluster, ends up "near" it.)
            var chroma = new double[12];
            for (int mode = 0; mode < 2; mode++)
            {
                for (int tonic = 0; tonic < 12; tonic++)
                {
                    Array.Clear(chroma, 0, 12);
                    chroma[tonic] = 1;
                    chroma[(tonic + (mode == 0 ? 4 : 3)) % 12] = 1;
                    chroma[(tonic + 7) % 12] = 1;
                    Transform(chroma, out var re, out var im);
                    int baseIdx = (tonic + 12 * mode) * 6;
                    for (int k = 0; k < 6; k++) { _keyRe[baseIdx + k] = re[k]; _keyIm[baseIdx + k] = im[k]; }
                }
            }
        }

        // Normalises to unit sum (L1) and returns the 6 weighted DFT coefficients. Allocates (constructor use only).
        private void Transform(double[] chroma, out double[] re, out double[] im)
        {
            re = new double[6];
            im = new double[6];
            double sum = 0;
            foreach (var x in chroma) sum += x;
            if (sum <= 0) return;
            for (int k = 0; k < 6; k++)
            {
                double r = 0, i = 0;
                for (int n = 0; n < 12; n++) { r += chroma[n] * _cos[k * 12 + n]; i += chroma[n] * _sin[k * 12 + n]; }
                re[k] = W[k] * r / sum;
                im[k] = W[k] * i / sum;
            }
        }

        /// <summary>
        /// Tension of the pitch classes in <paramref name="bins"/> (12 values, 0 = C; scale does not matter) in the key
        /// (<paramref name="keyTonic"/> 0..11, or -1 if unknown). <paramref name="total"/> is the histogram's total weight,
        /// used only to decide whether anything is sounding.
        /// </summary>
        public TensionResult Compute(float[] bins, float total, int keyTonic, bool keyIsMinor)
        {
            var result = new TensionResult();
            if (total < MinEvidence) return result;

            double sum = 0;
            for (int n = 0; n < 12; n++) sum += bins[n];
            if (sum <= 0) return result;

            double norm2 = 0;
            for (int k = 0; k < 6; k++)
            {
                double r = 0, i = 0;
                for (int n = 0; n < 12; n++) { r += bins[n] * _cos[k * 12 + n]; i += bins[n] * _sin[k * 12 + n]; }
                _re[k] = W[k] * r / sum;
                _im[k] = W[k] * i / sum;
                norm2 += _re[k] * _re[k] + _im[k] * _im[k];
            }

            double dissonance = 1.0 - Math.Sqrt(norm2) / _maxNorm;
            double dissN = Clamp01((dissonance - DissonanceFloor) / (FullDissonance - DissonanceFloor));

            double keyN = 0;
            bool usedKey = keyTonic >= 0 && keyTonic < 12;
            if (usedKey)
            {
                int baseIdx = (keyTonic + (keyIsMinor ? 12 : 0)) * 6;
                double d2 = 0;
                for (int k = 0; k < 6; k++)
                {
                    double dr = _re[k] - _keyRe[baseIdx + k], di = _im[k] - _keyIm[baseIdx + k];
                    d2 += dr * dr + di * di;
                }
                keyN = Clamp01(Math.Sqrt(d2) / _maxNorm / FullKeyDistance);
            }

            double tension = usedKey ? KeyWeight * keyN + (1.0 - KeyWeight) * dissN : dissN;
            result.Tension = (float)Clamp01(tension);
            result.KeyDistance = (float)keyN;
            result.Dissonance = (float)dissN;
            result.Active = true;
            result.UsedKey = usedKey;
            return result;
        }

        private static double Clamp01(double x) => x < 0 ? 0 : (x > 1 ? 1 : x);
    }
}
