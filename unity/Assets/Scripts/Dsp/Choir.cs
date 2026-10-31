using System;

namespace Haptics.Dsp
{
    /// <summary>
    /// The analysis pipeline for N singers. Input-agnostic: feed it interleaved samples from any source (voice i reads channel i).
    /// Each channel is first scaled by its own gain (mic trim). Then:
    /// per voice, <see cref="VoiceProcessor"/> gives level, onset and YIN pitch;
    /// on the sum of all voices, one <see cref="FeatureExtractor"/> gives loudness and the spectral descriptors;
    /// <see cref="ChoirAnalyzer"/> combines both into a <see cref="ChoirFrame"/>.
    /// Not thread-safe: call <see cref="Process"/> from a single thread.
    /// </summary>
    public sealed class Choir
    {
        private const float CalibrationFloorRms = 0.002f;

        private readonly VoiceProcessor[] _processors;
        private readonly FeatureExtractor _mix;
        private readonly Action<FeatureFrame> _onMix;
        private readonly float[] _gains;
        private readonly float[] _peaks;
        private readonly double[] _calSum;
        private readonly int[] _calCount;
        private readonly float[] _mixSample = new float[1];
        private FeatureFrame _mixFrame;
        private bool _mixReady;
        private bool _onsetSinceLastFrame;
        private volatile bool _calibrating;

        public VoiceFrame[] Voices { get; }
        public ChoirFrame Frame { get; private set; }
        public int VoiceCount => _processors.Length;

        /// <summary>Per-sample decay of the peak meters (about 0.5 s at 44.1 kHz).</summary>
        public float PeakDecay = 0.99995f;

        public Choir(int sampleRate, int voiceCount, int hopSize = 512)
        {
            _processors = new VoiceProcessor[voiceCount];
            Voices = new VoiceFrame[voiceCount];
            _gains = new float[voiceCount];
            _peaks = new float[voiceCount];
            _calSum = new double[voiceCount];
            _calCount = new int[voiceCount];
            for (int i = 0; i < voiceCount; i++)
            {
                _processors[i] = new VoiceProcessor(sampleRate, hopSize);
                _gains[i] = 1f;
            }
            _mix = new FeatureExtractor(sampleRate, 1024, hopSize);
            _onMix = f => { _mixFrame = f; _mixReady = true; };
        }

        /// <summary>Linear gain applied to voice <paramref name="voice"/> before any analysis.</summary>
        public float GetGain(int voice) => _gains[voice];

        public void SetGain(int voice, float gain) => _gains[voice] = Math.Max(0f, gain);

        public float GetGainDb(int voice) => 20f * (float)Math.Log10(Math.Max(_gains[voice], 1e-6f));

        public void SetGainDb(int voice, float db) => SetGain(voice, (float)Math.Pow(10.0, db / 20.0));

        /// <summary>Peak level of the raw input of a channel (before gain), 0..1+, slowly decaying. 1.0 means clipping.</summary>
        public float GetPeak(int voice) => _peaks[voice];

        public bool IsCalibrating => _calibrating;

        /// <summary>Starts averaging each channel's level. Have everyone sing at concert volume, then call <see cref="FinishCalibration"/>.</summary>
        public void StartCalibration()
        {
            for (int i = 0; i < _calSum.Length; i++) { _calSum[i] = 0; _calCount[i] = 0; }
            _calibrating = true;
        }

        /// <summary>
        /// Sets every gain so that the channel's average level while singing equals <paramref name="targetRms"/>.
        /// Channels that stayed silent keep their gain. Gains are limited to -30..+30 dB.
        /// </summary>
        public void FinishCalibration(float targetRms = 0.1f)
        {
            _calibrating = false;
            for (int v = 0; v < _gains.Length; v++)
            {
                if (_calCount[v] == 0) continue;
                float mean = (float)(_calSum[v] / _calCount[v]); // measured with the current gain applied
                float g = _gains[v] * targetRms / mean;
                _gains[v] = Math.Min(31.6f, Math.Max(0.0316f, g));
            }
        }

        /// <summary>
        /// Feeds <paramref name="frames"/> sample-frames of interleaved audio. Voices without a matching channel get silence.
        /// Returns true if <see cref="Frame"/> was refreshed.
        /// </summary>
        public bool Process(float[] interleaved, int frames, int channels)
        {
            bool updated = false;
            for (int n = 0; n < frames; n++)
            {
                float mixSum = 0f;
                for (int v = 0; v < _processors.Length; v++)
                {
                    float raw = v < channels ? interleaved[n * channels + v] : 0f;
                    _peaks[v] = Math.Max(Math.Abs(raw), _peaks[v] * PeakDecay);

                    float s = raw * _gains[v];
                    mixSum += s;
                    if (_processors[v].Process(s))
                    {
                        Voices[v] = _processors[v].Latest;
                        if (Voices[v].Onset) _onsetSinceLastFrame = true;
                        if (_calibrating && Voices[v].Rms >= CalibrationFloorRms)
                        {
                            _calSum[v] += Voices[v].Rms;
                            _calCount[v]++;
                        }
                        updated = true;
                    }
                }

                _mixSample[0] = mixSum;
                _mix.Process(_mixSample, 0, 1, _onMix);
            }

            if (updated && _mixReady)
            {
                Frame = ChoirAnalyzer.Analyze(Voices, _mixFrame, _onsetSinceLastFrame);
                _onsetSinceLastFrame = false;
                return true;
            }
            return false;
        }
    }
}
