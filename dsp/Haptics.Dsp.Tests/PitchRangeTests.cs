using System;
using Haptics.Dsp;
using Xunit;

public class PitchRangeTests
{
    private const int Sr = TestSignals.Sr;

    private static float Track(PitchTracker t, double hz, double seconds = 1.0)
    {
        int n = (int)(Sr * seconds);
        float last = 0f;
        for (int i = 0; i < n; i++)
            if (t.Process(TestSignals.Voice(hz, i, 0.3f))) last = t.Latest.PitchHz;
        return last;
    }

    [Theory]
    [InlineData(65.4)]   // C2
    [InlineData(73.4)]   // D2
    [InlineData(82.4)]   // E2
    public void BassNotesAreTrackedWithALowMinimum(double hz)
    {
        var t = new PitchTracker(Sr, 512, 60f, 500f);
        float got = Track(t, hz);
        Assert.InRange(got, (float)hz * 0.97f, (float)hz * 1.03f);
    }

    [Fact]
    public void TheDefaultRangeDoesNotReportABassNote()
    {
        // 73 Hz is below the default minimum of 100 Hz: it must not be reported as 73 Hz.
        var t = new PitchTracker(Sr);
        float got = Track(t, 73.4);
        Assert.False(got > 70f && got < 77f, $"reported {got} Hz");
    }

    [Fact]
    public void AHigherMinimumStillTracksHighVoices()
    {
        var t = new PitchTracker(Sr, 512, 200f, 1100f);
        float got = Track(t, 523.3); // C5
        Assert.InRange(got, 523.3f * 0.98f, 523.3f * 1.02f);
    }

    [Fact]
    public void AnalysisWindowCoversOnePeriodOfTheMinimum()
    {
        // 40 Hz at 44.1 kHz needs a longer window than the 768-sample default; it must still construct and run.
        var t = new PitchTracker(Sr, 512, 40f, 400f);
        float got = Track(t, 50.0, 1.5);
        Assert.InRange(got, 48f, 52f);
    }

    [Theory]
    [InlineData(0f, 500f)]
    [InlineData(-5f, 500f)]
    [InlineData(500f, 100f)]
    [InlineData(100f, 100f)]
    public void InvalidRangesAreRejected(float lo, float hi) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new PitchTracker(Sr, 512, lo, hi));

    [Fact]
    public void ChoirUsesAPerVoiceRange()
    {
        var choir = new Choir(Sr, 3, 512, new[] { 60f, 150f }, new[] { 400f, 1100f }); // voice 3 uses the default
        choir.GetPitchRange(0, out float lo0, out float hi0);
        choir.GetPitchRange(1, out float lo1, out float hi1);
        choir.GetPitchRange(2, out float lo2, out float hi2);
        Assert.Equal((60f, 400f), (lo0, hi0));
        Assert.Equal((150f, 1100f), (lo1, hi1));
        Assert.Equal((100f, 1100f), (lo2, hi2));
    }

    [Fact]
    public void ChoirFallsBackToTheDefaultRangeOnNonsense()
    {
        var choir = new Choir(Sr, 2, 512, new[] { 500f, 80f }, new[] { 100f, 80f }); // min above max, min equal to max
        choir.GetPitchRange(0, out float lo0, out float hi0);
        choir.GetPitchRange(1, out float lo1, out float hi1);
        Assert.Equal((100f, 1100f), (lo0, hi0));
        Assert.Equal((100f, 1100f), (lo1, hi1));
    }

    [Fact]
    public void ChoirTracksABassAndASopranoAtTheSameTime()
    {
        var choir = new Choir(Sr, 2, 512, new[] { 60f, 200f }, new[] { 400f, 1100f });
        var buf = new float[441 * 2];
        int n = 0;
        for (int chunk = 0; chunk < 100; chunk++)
        {
            for (int f = 0; f < 441; f++, n++)
            {
                buf[f * 2] = TestSignals.Voice(73.4, n, 0.3f);
                buf[f * 2 + 1] = TestSignals.Voice(523.3, n, 0.3f);
            }
            choir.Process(buf, 441, 2);
        }
        Assert.InRange(choir.Voices[0].PitchHz, 73.4f * 0.97f, 73.4f * 1.03f);
        Assert.InRange(choir.Voices[1].PitchHz, 523.3f * 0.98f, 523.3f * 1.02f);
    }
}
