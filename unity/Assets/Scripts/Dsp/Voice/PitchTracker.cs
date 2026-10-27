using System;

namespace Haptics.Dsp
{
    /// <summary>Per-singer pitch tracking (YIN) on one channel. This is the only analysis done per singer.</summary>
    public sealed class PitchTracker
    {
        private readonly Yin _yin;
        private readonly int _hopSize;
        private readonly float[] _ring;
        private readonly float[] _linear;
        private int _write;
        private int _sinceHop;
        private int _filled;

        /// <summary>RMS of the analysed window below this is treated as silence (no pitch reported).</summary>
        public float SilenceRms = 0.01f;
        public float MinPitchConfidence = 0.7f;

        public VoiceFrame Latest { get; private set; }

        public PitchTracker(int sampleRate, int hopSize = 512)
        {
            if (hopSize <= 0) throw new ArgumentOutOfRangeException(nameof(hopSize));
            _hopSize = hopSize;
            _yin = new Yin(sampleRate);
            _ring = new float[_yin.BufferSize];
            _linear = new float[_yin.BufferSize];
        }

        /// <summary>Feeds one sample. Returns true when <see cref="Latest"/> was refreshed (every hop).</summary>
        public bool Process(float sample)
        {
            _ring[_write] = sample;
            _write = (_write + 1) % _ring.Length;
            if (_filled < _ring.Length) _filled++;

            if (++_sinceHop < _hopSize || _filled < _ring.Length) return false;
            _sinceHop = 0;

            double sumSq = 0;
            for (int i = 0; i < _ring.Length; i++)
            {
                float v = _ring[(_write + i) % _ring.Length];
                _linear[i] = v;
                sumSq += v * v;
            }

            float pitch = 0f, conf = 0f;
            if (Math.Sqrt(sumSq / _ring.Length) >= SilenceRms)
            {
                pitch = _yin.Detect(_linear, out conf);
                if (conf < MinPitchConfidence) pitch = 0f;
            }

            Latest = new VoiceFrame { PitchHz = pitch, PitchConfidence = conf };
            return true;
        }
    }
}
