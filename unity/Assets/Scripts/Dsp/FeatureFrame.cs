namespace Haptics.Dsp
{
    /// <summary>Features extracted from one analysis frame.</summary>
    public struct FeatureFrame
    {
        /// <summary>Root-mean-square level of the frame (0..1 for full-scale input).</summary>
        public float Rms;

        /// <summary>Spectral centroid in Hz (brightness).</summary>
        public float SpectralCentroid;

        /// <summary>Spectral spread in Hz: standard deviation of the spectrum around the centroid.</summary>
        public float SpectralSpread;

        /// <summary>Spectral skewness (asymmetry around the centroid; positive = energy tail towards high frequencies).
        /// Called "tilt" in the RASTA toolbox.</summary>
        public float SpectralSkewness;

        /// <summary>Normalised spectral entropy, 0 (one pure peak) .. 1 (flat, noise-like).</summary>
        public float SpectralEntropy;

        /// <summary>Fraction of adjacent sample pairs that change sign, 0..1.</summary>
        public float ZeroCrossingRate;

        /// <summary>Half-wave rectified spectral flux versus the previous frame.</summary>
        public float SpectralFlux;

        /// <summary>True when an onset was detected on this frame.</summary>
        public bool Onset;
    }
}
