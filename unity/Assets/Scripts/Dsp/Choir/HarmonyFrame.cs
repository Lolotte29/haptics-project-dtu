namespace Haptics.Dsp
{
    /// <summary>Chord/harmony state of the ensemble at one analysis step.</summary>
    public struct HarmonyFrame
    {
        /// <summary>Chord root as pitch class 0..11 (C = 0), or -1 when unknown.</summary>
        public int RootPitchClass;
        /// <summary>0..1 confidence in <see cref="RootPitchClass"/>.</summary>
        public float Confidence;
    }
}
