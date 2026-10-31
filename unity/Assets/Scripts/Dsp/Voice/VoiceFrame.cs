namespace Haptics.Dsp
{
    /// <summary>What we know about one singer at one analysis step: pitch only. Spectral features live on the mix.</summary>
    public struct VoiceFrame
    {
        /// <summary>Fundamental frequency in Hz, or 0 when the voice is silent/unvoiced.</summary>
        public float PitchHz;
        public float PitchConfidence;

        public bool Voiced => PitchHz > 0f;
    }
}
