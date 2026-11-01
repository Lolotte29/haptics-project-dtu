namespace Haptics.Dsp
{
    /// <summary>
    /// Chord recognition / harmonic analysis over per-voice pitches. Plugged into <see cref="Choir"/>.
    /// Sources with pitch tracking switched off arrive unvoiced, so implementations only consider voiced entries.
    /// Implementations must not allocate in <see cref="Analyze"/>.
    /// </summary>
    public interface IHarmonyAnalyzer
    {
        HarmonyFrame Analyze(VoiceFrame[] voices);
    }
}
