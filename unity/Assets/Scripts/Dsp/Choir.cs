namespace Haptics.Dsp
{
    /// <summary>
    /// The analysis pipeline for N singers: one <see cref="VoiceProcessor"/> per voice, then <see cref="ChoirAnalyzer"/>.
    /// Input-agnostic: feed it interleaved samples from any source (voice i reads channel i).
    /// Not thread-safe: call <see cref="Process"/> from a single thread.
    /// </summary>
    public sealed class Choir
    {
        private readonly VoiceProcessor[] _processors;
        private bool _onsetSinceLastFrame;

        public VoiceFrame[] Voices { get; }
        public ChoirFrame Frame { get; private set; }
        public int VoiceCount => _processors.Length;

        public Choir(int sampleRate, int voiceCount, int hopSize = 512)
        {
            _processors = new VoiceProcessor[voiceCount];
            Voices = new VoiceFrame[voiceCount];
            for (int i = 0; i < voiceCount; i++) _processors[i] = new VoiceProcessor(sampleRate, hopSize);
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
                for (int v = 0; v < _processors.Length; v++)
                {
                    float s = v < channels ? interleaved[n * channels + v] : 0f;
                    if (_processors[v].Process(s))
                    {
                        Voices[v] = _processors[v].Latest;
                        if (Voices[v].Onset) _onsetSinceLastFrame = true;
                        updated = true;
                    }
                }
            }

            if (updated)
            {
                Frame = ChoirAnalyzer.Analyze(Voices, _onsetSinceLastFrame);
                _onsetSinceLastFrame = false;
            }
            return updated;
        }
    }
}
