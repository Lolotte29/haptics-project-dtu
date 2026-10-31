namespace Haptics.Dsp
{
    public enum ChoirMode
    {
        /// <summary>One source: spectral features only, no pitch tracking, no harmony.</summary>
        SingleSource,
        /// <summary>Several sources: per-voice YIN, optional chord analysis, plus spectral features of the summed signal.</summary>
        MultiSource,
    }
}
