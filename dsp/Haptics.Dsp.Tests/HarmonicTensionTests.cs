using System;
using Haptics.Dsp;
using Xunit;

public class HarmonicTensionTests
{
    private static TensionResult Of(int[] pitchClasses, int key, bool minor = false, float total = 1f)
    {
        var bins = new float[12];
        foreach (var pc in pitchClasses) bins[pc] += 1f;
        return new HarmonicTension().Compute(bins, total, key, minor);
    }

    [Fact]
    public void NothingSoundingIsInactiveWithZeroTension()
    {
        var r = new HarmonicTension().Compute(new float[12], 0f, 0, false);
        Assert.False(r.Active);
        Assert.Equal(0f, r.Tension);
        r = new HarmonicTension().Compute(new float[12], 1f, 0, false); // total says "sounding" but the bins are empty
        Assert.False(r.Active);
    }

    [Fact]
    public void TonicTriadHasNoKeyDistanceInMajorAndMinor()
    {
        Assert.Equal(0f, Of(new[] { 0, 4, 7 }, 0).KeyDistance, 4);
        Assert.Equal(0f, Of(new[] { 9, 0, 4 }, 9, minor: true).KeyDistance, 4);
    }

    [Fact]
    public void ChordsFarFromTheKeyAreMoreTense()
    {
        float tonic = Of(new[] { 0, 4, 7 }, 0).Tension;
        float relativeMinor = Of(new[] { 9, 0, 4 }, 0).Tension;
        float dominant = Of(new[] { 7, 11, 2 }, 0).Tension;
        float dominant7 = Of(new[] { 7, 11, 2, 5 }, 0).Tension;
        float foreign = Of(new[] { 6, 10, 1 }, 0).Tension; // F# major in C
        Assert.True(tonic < relativeMinor, $"{tonic} < {relativeMinor}");
        Assert.True(relativeMinor < dominant, $"{relativeMinor} < {dominant}");
        Assert.True(dominant < dominant7, $"{dominant} < {dominant7}");
        Assert.True(dominant7 <= foreign + 0.05f, $"{dominant7} vs {foreign}");
        Assert.True(tonic < 0.2f, $"tonic triad is calm: {tonic}");
    }

    [Fact]
    public void ClustersOfSemitonesAreMoreDissonantThanTriads()
    {
        var triad = Of(new[] { 0, 4, 7 }, -1);
        var cluster = Of(new[] { 0, 1, 2 }, -1);
        Assert.True(cluster.Dissonance > triad.Dissonance + 0.5f, $"{cluster.Dissonance} vs {triad.Dissonance}");
        Assert.True(triad.Dissonance < 0.3f, $"triad dissonance {triad.Dissonance}");
    }

    [Fact]
    public void SameChordIsMoreTenseInADistantKey()
    {
        float inC = Of(new[] { 0, 4, 7 }, 0).Tension;
        float inFSharp = Of(new[] { 0, 4, 7 }, 6).Tension;
        Assert.True(inFSharp > inC + 0.4f, $"{inFSharp} vs {inC}");
    }

    [Fact]
    public void WithoutAKeyOnlyDissonanceCounts()
    {
        var r = Of(new[] { 0, 1, 2 }, -1);
        Assert.False(r.UsedKey);
        Assert.Equal(r.Dissonance, r.Tension, 5);
        Assert.Equal(0f, r.KeyDistance);
    }

    [Fact]
    public void ScaleOfTheHistogramDoesNotMatter()
    {
        var a = Of(new[] { 7, 11, 2 }, 0);
        var bins = new float[12];
        bins[7] = bins[11] = bins[2] = 25f;
        var b = new HarmonicTension().Compute(bins, 75f, 0, false);
        Assert.Equal(a.Tension, b.Tension, 4);
    }

    [Fact]
    public void ResultsStayWithinZeroAndOne()
    {
        var rng = new Random(1);
        var t = new HarmonicTension();
        var bins = new float[12];
        for (int i = 0; i < 2000; i++)
        {
            for (int n = 0; n < 12; n++) bins[n] = rng.Next(3) == 0 ? 0f : (float)rng.NextDouble() * 3f;
            var r = t.Compute(bins, 1f, rng.Next(-1, 12), rng.Next(2) == 0);
            Assert.InRange(r.Tension, 0f, 1f);
            Assert.InRange(r.KeyDistance, 0f, 1f);
            Assert.InRange(r.Dissonance, 0f, 1f);
        }
    }
}

public class HarmonyTensionIntegrationTests
{
    private const float Dt = 0.01f;
    private static float Hz(double midi) => (float)(440.0 * Math.Pow(2.0, (midi - 69.0) / 12.0));

    private static HarmonyFrame Hold(HarmonyTracker t, int[] chord, double seconds)
    {
        var v = new VoiceFrame[chord.Length];
        HarmonyFrame f = default;
        for (double time = 0; time < seconds; time += Dt)
        {
            for (int i = 0; i < chord.Length; i++) v[i] = new VoiceFrame { PitchHz = Hz(chord[i]), PitchConfidence = 0.9f };
            f = t.Analyze(v, Dt);
        }
        return f;
    }

    [Fact]
    public void TensionRisesOnTheDominantAndFallsBackOnTheTonic()
    {
        var t = new HarmonyTracker(4);
        for (int round = 0; round < 4; round++) // establish C major
        {
            Hold(t, new[] { 48, 60, 64, 67 }, 2);
            Hold(t, new[] { 53, 60, 65, 69 }, 2);
            Hold(t, new[] { 43, 59, 62, 67 }, 2);
        }
        var atV = Hold(t, new[] { 43, 59, 62, 65 }, 2);   // V7
        var atI = Hold(t, new[] { 48, 60, 64, 67 }, 2);   // I
        Assert.Equal(0, atI.KeyTonic);
        Assert.True(atV.Tension > atI.Tension + 0.3f, $"V7 {atV.Tension:0.00} vs I {atI.Tension:0.00}");
    }

    [Fact]
    public void SilenceGivesZeroTension()
    {
        var t = new HarmonyTracker(2);
        Hold(t, new[] { 60, 64 }, 3);
        var silent = new VoiceFrame[2];
        HarmonyFrame f = default;
        for (int i = 0; i < 1500; i++) f = t.Analyze(silent, Dt); // 15 s: the fast histogram fades out
        Assert.Equal(0f, f.Tension);
    }
}
