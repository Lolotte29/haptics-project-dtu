namespace Haptics.Dsp
{
    /// <summary>Harmony state of the ensemble at one analysis step.</summary>
    public struct HarmonyFrame
    {
        /// <summary>Chord root as pitch class 0..11 (C = 0), or -1 when unknown.</summary>
        public int RootPitchClass;
        /// <summary>0..1 confidence in <see cref="RootPitchClass"/>.</summary>
        public float Confidence;

        /// <summary>Estimated key tonic as pitch class 0..11 (C = 0), or -1 when there is not enough evidence yet.
        /// Note: a default-constructed frame has 0 here, so check <see cref="KeyTonic"/> &gt;= 0 only on frames from an analyzer.</summary>
        public int KeyTonic;
        /// <summary>True for a minor key, false for major. Meaningful only when <see cref="KeyTonic"/> &gt;= 0.</summary>
        public bool KeyIsMinor;
        /// <summary>0..1: how clearly the current key beats the best alternative.</summary>
        public float KeyConfidence;
    }
}
