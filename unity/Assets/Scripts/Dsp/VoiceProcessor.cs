using System;

namespace Haptics.Dsp
{
    /// <summary>Everything we know about one singer at one analysis step.</summary>
    public struct VoiceFrame
    {
        public float Rms;
        public float SpectralCentroid;
        public float SpectralSpread;
        public float SpectralSkewness;
        public float SpectralEntropy;
        public float ZeroCrossingRate;
        public float SpectralFlux;
        public bool Onset;
        /// <summary>Fundamental frequency in Hz, or 0 when the voice is silent/unvoiced.</summary>
        public float PitchHz;
        public float PitchConfidence;

        public bool Voiced => PitchHz > 0f;
    }

    /// <summary>Per-singer analysis: level, spectral features, onset (<see cref="FeatureExtractor"/>) plus YIN pitch.</summary>
    public sealed class VoiceProcessor
    {
        private readonly FeatureExtractor _features;
        private readonly Yin _yin;
        private readonly float[] _ring;
        private readonly float[] _linear;
        private readonly Action<FeatureFrame> _onFeatures;
        private int _write;
        private bool _frameReady;

        /// <summary>RMS below this is treated as silence (no pitch reported).</summary>
        public float SilenceRms = 0.01f;
        public float MinPitchConfidence = 0.7f;

        public VoiceFrame Latest { get; private set; }

        public VoiceProcessor(int sampleRate, int hopSize = 512)
        {
            _features = new FeatureExtractor(sampleRate, 1024, hopSize);
            _yin = new Yin(sampleRate);
            _ring = new float[_yin.BufferSize];
            _linear = new float[_yin.BufferSize];
            _onFeatures = OnFeatures; // allocated once
        }

        /// <summary>Feeds one sample. Returns true when <see cref="Latest"/> was refreshed.</summary>
        public bool Process(float sample)
        {
            _ring[_write] = sample;
            _write = (_write + 1) % _ring.Length;

            _frameReady = false;
            _single[0] = sample;
            _features.Process(_single, 0, 1, _onFeatures);
            return _frameReady;
        }

        private readonly float[] _single = new float[1];

        private void OnFeatures(FeatureFrame f)
        {
            float pitch = 0f, conf = 0f;
            if (f.Rms >= SilenceRms)
            {
                for (int i = 0; i < _ring.Length; i++) _linear[i] = _ring[(_write + i) % _ring.Length];
                pitch = _yin.Detect(_linear, out conf);
                if (conf < MinPitchConfidence) pitch = 0f;
            }

            Latest = new VoiceFrame
            {
                Rms = f.Rms,
                SpectralCentroid = f.SpectralCentroid,
                SpectralSpread = f.SpectralSpread,
                SpectralSkewness = f.SpectralSkewness,
                SpectralEntropy = f.SpectralEntropy,
                ZeroCrossingRate = f.ZeroCrossingRate,
                SpectralFlux = f.SpectralFlux,
                Onset = f.Onset,
                PitchHz = pitch,
                PitchConfidence = conf,
            };
            _frameReady = true;
        }
    }
}
