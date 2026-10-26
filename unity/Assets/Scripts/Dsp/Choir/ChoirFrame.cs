namespace Haptics.Dsp
{
    /// <summary>Features of the whole ensemble.</summary>
    public struct ChoirFrame
    {
        /// <summary>Sensory dissonance between voices, 0 (consonant/none) .. 1 (very rough).</summary>
        public float Roughness;
        /// <summary>Loudest voice RMS.</summary>
        public float Loudness;
        /// <summary>True if any voice had an onset since the previous frame.</summary>
        public bool Onset;
        public int VoicedCount;
    }
}
