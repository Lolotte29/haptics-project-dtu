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

        /// <summary>Lowest pitch this tracker can report in Hz (set in the constructor; the YIN window is sized from it).</summary>
        public float MinHz { get; }
        /// <summary>Highest pitch this tracker can report in Hz.</summary>
        public float MaxHz { get; }

        /// <summary>
        /// <paramref name="minHz"/>/<paramref name="maxHz"/> is the pitch range searched. A lower minimum costs more CPU (the search
        /// grows with 1/minHz) and a wider range makes octave errors more likely, so give each voice only the range it needs
        /// (e.g. bass about 60-500 Hz, soprano about 200-1100 Hz). The analysis window grows to at least one period of the minimum.
        /// </summary>
        public PitchTracker(int sampleRate, int hopSize = 512, float minHz = 100f, float maxHz = 1100f)
        {
            if (hopSize <= 0) throw new ArgumentOutOfRangeException(nameof(hopSize));
            if (!(minHz > 0f) || !(maxHz > minHz)) throw new ArgumentOutOfRangeException(nameof(minHz), "need 0 < minHz < maxHz");
            if (sampleRate / maxHz < 2f) throw new ArgumentOutOfRangeException(nameof(maxHz), "maxHz is too high for this sample rate");
            _hopSize = hopSize;
            MinHz = minHz;
            MaxHz = maxHz;
            int window = Math.Max(768, (int)(sampleRate / minHz));
            _yin = new Yin(sampleRate, window, minHz, maxHz);
            _ring = new float[_yin.BufferSize];
            _linear = new float[_yin.BufferSize];
        }

        /// <summary>Clears the analysis window and <see cref="Latest"/>, e.g. after the source was switched off.</summary>
        public void Reset()
        {
            Array.Clear(_ring, 0, _ring.Length);
            _write = _sinceHop = _filled = 0;
            Latest = default;
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
