using System;

namespace Haptics.Dsp
{
    /// <summary>
    /// The analysis pipeline. Pitch tracking (YIN) can be switched on or off for every source, whatever the
    /// source count (<see cref="SetPitchEnabled"/>); spectral features always run on the summed signal.
    /// Input-agnostic: feed it interleaved samples from any source (voice i reads channel i).
    /// This is where ensemble-level stages (chord, key, harmonic tension) will plug in, see docs/roadmap.md.
    /// Not thread-safe: call <see cref="Process"/> from a single thread.
    /// </summary>
    public sealed class Choir
    {
        private readonly PitchTracker[] _trackers;
        private readonly bool[] _pitchEnabled;
        private readonly bool[] _trackerLive;  // tracker has been fed since it was last reset
        private readonly FeatureExtractor _features;
        private readonly Action<FeatureFrame> _onFeatures;
        private readonly float[] _one = new float[1];
        private readonly float[] _gains;
        private readonly float[] _peaks;
        private readonly float[] _meanSq;      // smoothed mean square per channel, after gain
        private readonly float _levelCoef;
        private int _voiceCount;
        private bool _featuresUpdated;

        /// <summary>Per-singer pitch; stays default (unvoiced) for sources whose pitch tracking is off.</summary>
        public VoiceFrame[] Voices { get; }
        public int VoiceCount => _voiceCount;

        /// <summary>
        /// Spectral features (level, centroid, ...) of the summed signal, or of the single channel when there is only one.
        /// These are never computed per singer.
        /// </summary>
        public FeatureFrame Mix { get; private set; }

        /// <summary>
        /// Optional chord recognition. It sees <see cref="Voices"/>, where sources with pitch tracking off
        /// are always unvoiced, so only the sources chosen with <see cref="SetPitchEnabled"/> take part.
        /// </summary>
        public IHarmonyAnalyzer HarmonyAnalyzer { get; set; }

        /// <summary>Latest chord analysis; <see cref="HarmonyFrame.RootPitchClass"/> is -1 when none is available.</summary>
        public HarmonyFrame Harmony { get; private set; } = new HarmonyFrame { RootPitchClass = -1 };

        /// <summary>Gain applied to the sum of channels. Default 1/sqrt(N) keeps power constant for uncorrelated voices.</summary>
        public float MixGain { get; set; }

        /// <summary>Attack time constant of <see cref="FeatureFrame.Envelope"/> in ms. Safe to change while running.</summary>
        public float EnvelopeAttackMs
        {
            get => _features.Envelope.AttackMs;
            set => _features.Envelope.AttackMs = value;
        }

        /// <summary>Release time constant of <see cref="FeatureFrame.Envelope"/> in ms. Safe to change while running.</summary>
        public float EnvelopeReleaseMs
        {
            get => _features.Envelope.ReleaseMs;
            set => _features.Envelope.ReleaseMs = value;
        }

        /// <summary>Per-sample decay of the peak meters (about 0.5 s at 44.1 kHz).</summary>
        public float PeakDecay = 0.99995f;

        /// <summary>Number of voices with a detected pitch in their latest frame.</summary>
        public int VoicedCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Voices.Length; i++) if (Voices[i].Voiced) n++;
                return n;
            }
        }

        public Choir(int sampleRate, int voiceCount, int hopSize = 512)
        {
            if (voiceCount < 1) throw new ArgumentOutOfRangeException(nameof(voiceCount));
            _voiceCount = voiceCount;
            Voices = new VoiceFrame[voiceCount];
            MixGain = 1f / (float)Math.Sqrt(voiceCount);
            _gains = new float[voiceCount];
            _peaks = new float[voiceCount];
            _meanSq = new float[voiceCount];
            _levelCoef = 1f / (0.05f * sampleRate); // ~50 ms smoothing
            _pitchEnabled = new bool[voiceCount];
            _trackerLive = new bool[voiceCount];
            for (int i = 0; i < voiceCount; i++) { _gains[i] = 1f; _pitchEnabled[i] = true; }

            _features = new FeatureExtractor(sampleRate, 1024, hopSize);
            _onFeatures = f => { Mix = f; _featuresUpdated = true; }; // allocated once

            _trackers = new PitchTracker[voiceCount];
            for (int i = 0; i < voiceCount; i++) _trackers[i] = new PitchTracker(sampleRate, hopSize);
        }

        /// <summary>Whether YIN runs on source <paramref name="voice"/> (default true for every source).</summary>
        public bool GetPitchEnabled(int voice) => _pitchEnabled[voice];

        /// <summary>
        /// Switches pitch tracking of one source on or off. A source that is off costs no YIN time, reports no pitch
        /// and is ignored by harmony analysis. Its gain, meters and share of the mix are unaffected.
        /// </summary>
        public void SetPitchEnabled(int voice, bool enabled) => _pitchEnabled[voice] = enabled;

        /// <summary>Linear gain (mic trim) applied to channel <paramref name="voice"/> before any analysis.</summary>
        public float GetGain(int voice) => _gains[voice];

        public void SetGain(int voice, float gain) => _gains[voice] = Math.Max(0f, gain);

        public float GetGainDb(int voice) => 20f * (float)Math.Log10(Math.Max(_gains[voice], 1e-6f));

        public void SetGainDb(int voice, float db) => SetGain(voice, (float)Math.Pow(10.0, db / 20.0));

        /// <summary>Peak of the raw input of a channel (before gain), slowly decaying. 1.0 means clipping.</summary>
        public float GetPeak(int voice) => _peaks[voice];

        /// <summary>Smoothed RMS level of a channel after its gain.</summary>
        public float GetLevel(int voice) => (float)Math.Sqrt(_meanSq[voice]);

        /// <summary>
        /// Feeds <paramref name="frames"/> sample-frames of interleaved audio. Voices without a matching channel get silence.
        /// Returns true if <see cref="Mix"/> or any <see cref="Voices"/> entry was refreshed.
        /// </summary>
        public bool Process(float[] interleaved, int frames, int channels)
        {
            bool voicesUpdated = false;
            _featuresUpdated = false;
            int used = Math.Min(channels, _voiceCount);
            for (int n = 0; n < frames; n++)
            {
                float sum = 0f;
                for (int v = 0; v < used; v++)
                {
                    float raw = interleaved[n * channels + v];
                    _peaks[v] = Math.Max(Math.Abs(raw), _peaks[v] * PeakDecay);
                    float s = raw * _gains[v];
                    _meanSq[v] += (s * s - _meanSq[v]) * _levelCoef;
                    sum += s;
                    voicesUpdated |= TrackPitch(v, s);
                }
                // Channels without input are silent voices: keep their trackers' windows moving.
                for (int v = used; v < _voiceCount; v++)
                    voicesUpdated |= TrackPitch(v, 0f);

                _one[0] = sum * MixGain;
                _features.Process(_one, 0, 1, _onFeatures);
            }

            if (voicesUpdated && HarmonyAnalyzer != null)
                Harmony = HarmonyAnalyzer.Analyze(Voices);
            return voicesUpdated || _featuresUpdated;
        }

        private bool TrackPitch(int v, float sample)
        {
            if (!_pitchEnabled[v])
            {
                if (!_trackerLive[v]) return false;
                _trackers[v].Reset(); // switched off: forget the window, report no pitch
                _trackerLive[v] = false;
                Voices[v] = default;
                return true;
            }
            _trackerLive[v] = true;
            if (!_trackers[v].Process(sample)) return false;
            Voices[v] = _trackers[v].Latest;
            return true;
        }
    }
}
