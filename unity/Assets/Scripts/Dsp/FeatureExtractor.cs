using System;

namespace Haptics.Dsp
{
    /// <summary>
    /// Streaming feature extractor. Push mono samples of any chunk size with <see cref="Process"/>;
    /// a <see cref="FeatureFrame"/> is produced every <c>hopSize</c> samples. No allocations while running.
    /// </summary>
    public sealed class FeatureExtractor
    {
        private readonly int _frameSize;
        private readonly int _hopSize;
        private readonly float _binHz;
        private readonly Fft _fft;
        private readonly float[] _ring;      // last _frameSize samples, in order after Linearise
        private readonly float[] _frame;
        private readonly float[] _mag;
        private readonly float[] _prevMag;
        private readonly float[] _fluxHistory;

        private int _writePos;
        private int _sinceHop;
        private int _filled;
        private int _fluxIdx;
        private int _fluxCount;
        private int _framesSinceOnset = int.MaxValue / 2;

        /// <summary>Onset threshold = mean(flux history) * Sensitivity + MinFlux.</summary>
        public float Sensitivity = 1.5f;
        public float MinFlux = 0.02f;
        /// <summary>Minimum frames between two onsets.</summary>
        public int RefractoryFrames = 4;

        public int SampleRate { get; }
        public int HopSize => _hopSize;

        public FeatureExtractor(int sampleRate, int frameSize = 1024, int hopSize = 512, int fluxHistoryLength = 16)
        {
            if (hopSize <= 0 || hopSize > frameSize) throw new ArgumentException("Invalid hop size.", nameof(hopSize));
            SampleRate = sampleRate;
            _frameSize = frameSize;
            _hopSize = hopSize;
            _binHz = (float)sampleRate / frameSize;
            _fft = new Fft(frameSize);
            _ring = new float[frameSize];
            _frame = new float[frameSize];
            _mag = new float[_fft.BinCount];
            _prevMag = new float[_fft.BinCount];
            _fluxHistory = new float[fluxHistoryLength];
        }

        /// <summary>
        /// Feeds samples. Calls <paramref name="onFrame"/> (may be null) for every completed frame
        /// and returns the number of frames produced.
        /// </summary>
        public int Process(float[] samples, int offset, int count, Action<FeatureFrame> onFrame)
        {
            int frames = 0;
            for (int i = 0; i < count; i++)
            {
                _ring[_writePos] = samples[offset + i];
                _writePos = (_writePos + 1) % _frameSize;
                if (_filled < _frameSize) _filled++;

                if (++_sinceHop >= _hopSize && _filled >= _frameSize)
                {
                    _sinceHop = 0;
                    onFrame?.Invoke(AnalyseFrame());
                    frames++;
                }
            }
            return frames;
        }

        private FeatureFrame AnalyseFrame()
        {
            // Oldest sample sits at _writePos.
            double sumSq = 0;
            int crossings = 0;
            for (int i = 0; i < _frameSize; i++)
            {
                float v = _ring[(_writePos + i) % _frameSize];
                _frame[i] = v;
                sumSq += v * v;
                if (i > 0 && (v >= 0f) != (_frame[i - 1] >= 0f)) crossings++;
            }

            _fft.Magnitude(_frame, _mag);

            double num = 0, den = 0, flux = 0;
            for (int k = 0; k < _mag.Length; k++)
            {
                num += k * _binHz * _mag[k];
                den += _mag[k];
                float d = _mag[k] - _prevMag[k];
                if (d > 0) flux += d;
                _prevMag[k] = _mag[k];
            }

            // Spectral shape around the centroid (MathWorks definitions: spread, skewness, normalised entropy).
            double centroid = den > 1e-9 ? num / den : 0.0;
            double m2 = 0, m3 = 0, entropy = 0;
            if (den > 1e-9)
            {
                for (int k = 0; k < _mag.Length; k++)
                {
                    double dev = k * _binHz - centroid;
                    m2 += dev * dev * _mag[k];
                    m3 += dev * dev * dev * _mag[k];
                    double p = _mag[k] / den;
                    if (p > 1e-12) entropy -= p * Math.Log(p);
                }
                m2 /= den;
                m3 /= den;
                entropy /= Math.Log(_mag.Length);
            }
            double spread = Math.Sqrt(m2);

            float fluxF = (float)flux;
            bool onset = false;
            if (_fluxCount > 0)
            {
                float mean = 0;
                for (int i = 0; i < _fluxCount; i++) mean += _fluxHistory[i];
                mean /= _fluxCount;
                onset = fluxF > mean * Sensitivity + MinFlux && _framesSinceOnset >= RefractoryFrames;
            }
            _fluxHistory[_fluxIdx] = fluxF;
            _fluxIdx = (_fluxIdx + 1) % _fluxHistory.Length;
            if (_fluxCount < _fluxHistory.Length) _fluxCount++;
            _framesSinceOnset = onset ? 0 : Math.Min(_framesSinceOnset + 1, int.MaxValue / 2);

            return new FeatureFrame
            {
                Rms = (float)Math.Sqrt(sumSq / _frameSize),
                SpectralCentroid = (float)centroid,
                SpectralSpread = (float)spread,
                SpectralSkewness = spread > 1e-9 ? (float)(m3 / (spread * spread * spread)) : 0f,
                SpectralEntropy = (float)entropy,
                ZeroCrossingRate = (float)crossings / (_frameSize - 1),
                SpectralFlux = fluxF,
                Onset = onset,
            };
        }
    }
}
