namespace Haptics.Dsp
{
    /// <summary>Features extracted from one analysis frame.</summary>
    public struct FeatureFrame
    {
        /// <summary>Root-mean-square level of the frame (0..1 for full-scale input).</summary>
        public float Rms;

        /// <summary>Spectral centroid in Hz (brightness).</summary>
        public float SpectralCentroid;

        /// <summary>Half-wave rectified spectral flux versus the previous frame.</summary>
        public float SpectralFlux;

        /// <summary>True when an onset was detected on this frame.</summary>
        public bool Onset;
    }
}
