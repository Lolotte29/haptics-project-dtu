using System;

namespace Haptics.Dsp
{
    /// <summary>Result of <see cref="KeyEstimator"/>. Tonic is -1 while there is not enough evidence.</summary>
    public struct KeyEstimate
    {
        public int Tonic;
        public bool IsMinor;
        /// <summary>0..1: margin of the current key over the best other key.</summary>
        public float Confidence;
        public bool Valid => Tonic >= 0;
    }

    /// <summary>
    /// Key from a (slow) pitch-class histogram: correlates it with the 24 rotated major/minor key profiles
    /// (Krumhansl-Schmuckler method, with Temperley's profiles, which separate relative major/minor better).
    /// The reported key only changes when another key has been clearly better for <see cref="SwitchSeconds"/>,
    /// because the raw best match flips on ambiguous passages. Western tonal music only.
    /// No allocation after construction.
    /// </summary>
    public sealed class KeyEstimator
    {
        // Temperley (CBMS) profiles, relative weight of each scale degree above the tonic (index 0 = tonic).
        private static readonly double[] MajorProfile = { 5.0, 2.0, 3.5, 2.0, 4.5, 4.0, 2.0, 4.5, 2.0, 3.5, 1.5, 4.0 };
        private static readonly double[] MinorProfile = { 5.0, 2.0, 3.5, 4.5, 2.0, 4.0, 2.0, 4.5, 3.5, 2.0, 1.5, 4.0 };

        /// <summary>Histogram total (weighted voice-seconds) needed before any key is reported.</summary>
        public float MinEvidence { get; set; } = 1f;
        /// <summary>A challenger must beat the current key's correlation by this much to count (correlation units, -1..1).</summary>
        public float SwitchMargin { get; set; } = 0.05f;
        /// <summary>...and keep doing so for this long.</summary>
        public float SwitchSeconds { get; set; } = 1.5f;
        /// <summary>Correlation margin over the runner-up that maps to confidence 1.</summary>
        public float FullConfidenceMargin { get; set; } = 0.15f;

        public KeyEstimate Current { get; private set; } = new KeyEstimate { Tonic = -1 };

        private readonly double[][] _profiles = new double[2][]; // zero-mean, unit-norm
        private readonly double[] _scores = new double[24];      // index = tonic + 12 * (minor ? 1 : 0)
        private int _candidate = -1;
        private float _candidateTime;

        public KeyEstimator()
        {
            _profiles[0] = Normalise(MajorProfile);
            _profiles[1] = Normalise(MinorProfile);
        }

        private static double[] Normalise(double[] p)
        {
            double mean = 0;
            foreach (var x in p) mean += x;
            mean /= p.Length;
            double norm = 0;
            var r = new double[p.Length];
            for (int i = 0; i < p.Length; i++) { r[i] = p[i] - mean; norm += r[i] * r[i]; }
            norm = Math.Sqrt(norm);
            for (int i = 0; i < r.Length; i++) r[i] /= norm;
            return r;
        }

        public void Reset()
        {
            Current = new KeyEstimate { Tonic = -1 };
            _candidate = -1;
            _candidateTime = 0f;
        }

        /// <summary>Updates the estimate from a 12-bin histogram (pitch class 0 = C). <paramref name="dtSeconds"/> is the time since the last call.</summary>
        public KeyEstimate Update(float[] histogram, float dtSeconds)
        {
            double total = 0;
            for (int i = 0; i < 12; i++) total += histogram[i];
            if (total < MinEvidence) { Reset(); return Current; }

            double mean = total / 12.0, norm = 0;
            for (int i = 0; i < 12; i++) { double d = histogram[i] - mean; norm += d * d; }
            if (norm < 1e-12) { Reset(); return Current; } // flat histogram: no key information
            norm = Math.Sqrt(norm);

            int best = 0;
            for (int mode = 0; mode < 2; mode++)
            {
                var prof = _profiles[mode];
                for (int t = 0; t < 12; t++)
                {
                    double dot = 0;
                    for (int pc = 0; pc < 12; pc++) dot += (histogram[pc] - mean) * prof[(pc - t + 12) % 12];
                    int idx = t + 12 * mode;
                    _scores[idx] = dot / norm;
                    if (_scores[idx] > _scores[best]) best = idx;
                }
            }

            int cur = Current.Valid ? Current.Tonic + (Current.IsMinor ? 12 : 0) : -1;
            if (cur < 0)
            {
                cur = best; // first estimate: take the best match straight away
                _candidate = -1;
            }
            else if (best != cur && _scores[best] > _scores[cur] + SwitchMargin)
            {
                if (best == _candidate) _candidateTime += dtSeconds; else { _candidate = best; _candidateTime = dtSeconds; }
                if (_candidateTime >= SwitchSeconds) { cur = best; _candidate = -1; _candidateTime = 0f; }
            }
            else
            {
                _candidate = -1;
                _candidateTime = 0f;
            }

            double other = double.NegativeInfinity;
            for (int i = 0; i < 24; i++) if (i != cur && _scores[i] > other) other = _scores[i];
            float conf = (float)Math.Max(0.0, Math.Min(1.0, (_scores[cur] - other) / FullConfidenceMargin));

            Current = new KeyEstimate { Tonic = cur % 12, IsMinor = cur >= 12, Confidence = conf };
            return Current;
        }
    }
}
