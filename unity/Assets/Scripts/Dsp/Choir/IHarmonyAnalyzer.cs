namespace Haptics.Dsp
{
    /// <summary>
    /// Chord recognition / harmonic analysis over per-voice pitches. Plugged into <see cref="Choir"/>
    /// in multi-source mode only. Implementations must not allocate in <see cref="Analyze"/>.
    /// </summary>
    public interface IHarmonyAnalyzer
    {
        HarmonyFrame Analyze(VoiceFrame[] voices);
    }
}
