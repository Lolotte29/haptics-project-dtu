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
}
