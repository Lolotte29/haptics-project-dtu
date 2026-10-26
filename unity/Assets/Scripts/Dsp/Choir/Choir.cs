namespace Haptics.Dsp
{
    /// <summary>
    /// The analysis pipeline for N singers: one <see cref="VoiceProcessor"/> per voice.
    /// Input-agnostic: feed it interleaved samples from any source (voice i reads channel i).
    /// This is where ensemble-level stages (chord, key, harmonic tension) will plug in, see docs/roadmap.md.
    /// Not thread-safe: call <see cref="Process"/> from a single thread.
    /// </summary>
    public sealed class Choir
    {
        private readonly VoiceProcessor[] _processors;

        public VoiceFrame[] Voices { get; }
        public int VoiceCount => _processors.Length;

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
            _processors = new VoiceProcessor[voiceCount];
            Voices = new VoiceFrame[voiceCount];
            for (int i = 0; i < voiceCount; i++) _processors[i] = new VoiceProcessor(sampleRate, hopSize);
        }

        /// <summary>
        /// Feeds <paramref name="frames"/> sample-frames of interleaved audio. Voices without a matching channel get silence.
        /// Returns true if any <see cref="Voices"/> entry was refreshed.
        /// </summary>
        public bool Process(float[] interleaved, int frames, int channels)
        {
            bool updated = false;
            for (int n = 0; n < frames; n++)
            {
                for (int v = 0; v < _processors.Length; v++)
                {
                    float s = v < channels ? interleaved[n * channels + v] : 0f;
                    if (_processors[v].Process(s))
                    {
                        Voices[v] = _processors[v].Latest;
                        updated = true;
                    }
                }
            }
            return updated;
        }
    }
}
