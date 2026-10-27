using System;

namespace Haptics.Dsp
{
    public enum ChoirMode
    {
        /// <summary>One source: spectral features only, no pitch tracking, no harmony.</summary>
        SingleSource,
        /// <summary>Several sources: per-voice YIN, optional chord analysis, plus spectral features of the summed signal.</summary>
        MultiSource,
    }

    /// <summary>
    /// The analysis pipeline. The mode follows <c>voiceCount</c>:
    /// 1 = <see cref="ChoirMode.SingleSource"/>, 2+ = <see cref="ChoirMode.MultiSource"/>.
    /// Input-agnostic: feed it interleaved samples from any source (voice i reads channel i).
    /// This is where ensemble-level stages (chord, key, harmonic tension) will plug in, see docs/roadmap.md.
    /// Not thread-safe: call <see cref="Process"/> from a single thread.
    /// </summary>
    public sealed class Choir
    {
        private readonly VoiceProcessor[] _processors;
        private readonly VoiceProcessor _mixProcessor; // null in single-source mode

        public VoiceFrame[] Voices { get; }
        public int VoiceCount => _processors.Length;
        public ChoirMode Mode { get; }

        /// <summary>Spectral features of the summed signal (of the single source when there is only one). Pitch is never set.</summary>
        public VoiceFrame Mix { get; private set; }

        /// <summary>Optional chord recognition, used in multi-source mode only.</summary>
        public IHarmonyAnalyzer HarmonyAnalyzer { get; set; }

        /// <summary>Latest chord analysis; <see cref="HarmonyFrame.RootPitchClass"/> is -1 when none is available.</summary>
        public HarmonyFrame Harmony { get; private set; } = new HarmonyFrame { RootPitchClass = -1 };

        /// <summary>Gain applied to the sum of channels. Default 1/sqrt(N) keeps power constant for uncorrelated voices.</summary>
        public float MixGain { get; set; }

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
            _processors = new VoiceProcessor[voiceCount];
            Voices = new VoiceFrame[voiceCount];
            for (int i = 0; i < voiceCount; i++) _processors[i] = new VoiceProcessor(sampleRate, hopSize);

            Mode = voiceCount == 1 ? ChoirMode.SingleSource : ChoirMode.MultiSource;
            MixGain = 1f / (float)Math.Sqrt(voiceCount);
            if (Mode == ChoirMode.SingleSource)
                _processors[0].EnablePitch = false;
            else
                _mixProcessor = new VoiceProcessor(sampleRate, hopSize) { EnablePitch = false };
        }

        /// <summary>
        /// Feeds <paramref name="frames"/> sample-frames of interleaved audio. Voices without a matching channel get silence.
        /// Returns true if any <see cref="Voices"/> entry was refreshed.
        /// </summary>
        public bool Process(float[] interleaved, int frames, int channels)
        {
            bool updated = false;
            int used = Math.Min(channels, _processors.Length);
            for (int n = 0; n < frames; n++)
            {
                float sum = 0f;
                for (int v = 0; v < _processors.Length; v++)
                {
                    float s = v < used ? interleaved[n * channels + v] : 0f;
                    sum += s;
                    if (_processors[v].Process(s))
                    {
                        Voices[v] = _processors[v].Latest;
                        updated = true;
                    }
                }

                if (_mixProcessor != null && _mixProcessor.Process(sum * MixGain))
                    Mix = _mixProcessor.Latest;
            }

            if (updated)
            {
                if (Mode == ChoirMode.SingleSource)
                    Mix = Voices[0];
                else if (HarmonyAnalyzer != null)
                    Harmony = HarmonyAnalyzer.Analyze(Voices);
            }
            return updated;
        }
    }
}
