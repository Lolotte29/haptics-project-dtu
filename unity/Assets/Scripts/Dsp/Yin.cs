using System;

namespace Haptics.Dsp
{
    /// <summary>YIN monophonic pitch detector (de Cheveigné &amp; Kawahara 2002). No allocations after construction.</summary>
    public sealed class Yin
    {
        private readonly int _sampleRate;
        private readonly int _window;
        private readonly int _tauMin;
        private readonly int _tauMax;
        private readonly float _threshold;
        private readonly float[] _cmnd;

        /// <summary>Number of most-recent samples <see cref="Detect"/> needs.</summary>
        public int BufferSize => _window + _tauMax;

        public Yin(int sampleRate, int window = 768, float minHz = 100f, float maxHz = 1100f, float threshold = 0.15f)
        {
            _sampleRate = sampleRate;
            _window = window;
            _tauMin = Math.Max(2, (int)(sampleRate / maxHz));
            _tauMax = (int)(sampleRate / minHz);
            _threshold = threshold;
            _cmnd = new float[_tauMax + 1];
        }

        /// <summary>
        /// Estimates the pitch of the first <see cref="BufferSize"/> samples of <paramref name="x"/> (oldest first).
        /// Returns Hz, or 0 if no pitch was found. <paramref name="confidence"/> is 0..1.
        /// </summary>
        public float Detect(float[] x, out float confidence)
        {
            // Difference function + cumulative mean normalisation.
            _cmnd[0] = 1f;
            float running = 0f;
            for (int tau = 1; tau <= _tauMax; tau++)
            {
                float d = 0f;
                for (int j = 0; j < _window; j++)
                {
                    float diff = x[j] - x[j + tau];
                    d += diff * diff;
                }
                running += d;
                _cmnd[tau] = running > 1e-12f ? d * tau / running : 1f;
            }

            int best = -1;
            for (int tau = _tauMin; tau < _tauMax; tau++)
            {
                if (_cmnd[tau] < _threshold)
                {
                    while (tau + 1 < _tauMax && _cmnd[tau + 1] < _cmnd[tau]) tau++;
                    best = tau;
                    break;
                }
            }

            if (best < 0)
            {
                // No clear dip: report the deepest one as a low-confidence guess.
                float min = float.MaxValue;
                for (int tau = _tauMin; tau < _tauMax; tau++)
                    if (_cmnd[tau] < min) { min = _cmnd[tau]; best = tau; }
                confidence = Math.Max(0f, 1f - min);
                return 0f;
            }

            confidence = Math.Max(0f, 1f - _cmnd[best]);

            // Parabolic interpolation around the minimum.
            float tauF = best;
            if (best > 1 && best < _tauMax)
            {
                float a = _cmnd[best - 1], b = _cmnd[best], c = _cmnd[best + 1];
                float denom = a - 2f * b + c;
                if (Math.Abs(denom) > 1e-9f) tauF = best + 0.5f * (a - c) / denom;
            }
            return _sampleRate / tauF;
        }
    }
}
