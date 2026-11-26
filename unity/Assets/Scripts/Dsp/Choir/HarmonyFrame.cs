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

        /// <summary>Harmonic tension 0..1 of the last ~0.5 s of pitch classes in the current key (see <see cref="HarmonicTension"/>); 0 when nothing sounds.</summary>
        public float Tension;
        /// <summary>The key-distance part of <see cref="Tension"/>, 0..1.</summary>
        public float TensionKeyDistance;
        /// <summary>The dissonance part of <see cref="Tension"/>, 0..1.</summary>
        public float TensionDissonance;
    }
}
