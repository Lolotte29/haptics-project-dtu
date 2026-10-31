using System;
using Haptics.Dsp;
using Xunit;
using static TestSignals;

public class ChoirTests
{
    private static Choir RunChoir(double[] pitches, double seconds = 1.0, int chunk = 441)
    {
        var choir = new Choir(Sr, pitches.Length);
        int total = (int)(Sr * seconds);
        var buf = new float[chunk * pitches.Length];
        for (int o = 0; o < total; o += chunk)
        {
            for (int n = 0; n < chunk; n++)
                for (int v = 0; v < pitches.Length; v++)
                    buf[n * pitches.Length + v] = Voice(pitches[v], o + n);
            choir.Process(buf, chunk, pitches.Length);
        }
        return choir;
    }

    [Fact]
    public void ChoirTracksEachVoicePitch()
    {
        double[] p = { 261.6, 329.6, 392.0, 523.3 };
        var choir = RunChoir(p);
        for (int v = 0; v < p.Length; v++)
            Assert.InRange(choir.Voices[v].PitchHz, p[v] * 0.99, p[v] * 1.01);
        Assert.Equal(4, choir.VoicedCount);
    }

    [Fact]
    public void SilenceProducesNothing()
    {
        var choir = new Choir(Sr, 4);
        var buf = new float[441 * 4];
        for (int i = 0; i < 100; i++) choir.Process(buf, 441, 4);
        Assert.Equal(0, choir.VoicedCount);
    }

    [Fact]
    public void MissingChannelsAreSilentVoices()
    {
        // 4 voices configured, but the source only has 2 channels.
        var choir = new Choir(Sr, 4);
        var buf = new float[441 * 2];
        for (int o = 0; o < Sr; o += 441)
        {
            for (int n = 0; n < 441; n++)
            {
                buf[n * 2] = Voice(261.6, o + n);
                buf[n * 2 + 1] = Voice(392.0, o + n);
            }
            choir.Process(buf, 441, 2);
        }
        Assert.Equal(2, choir.VoicedCount);
    }

    private static float Sine(double hz, int n, float amp) => amp * (float)Math.Sin(2 * Math.PI * hz * n / Sr);

    /// <summary>Runs one pure sine per channel with the given amplitudes.</summary>
    private static void RunSines(Choir choir, double[] hz, float[] amp, double seconds, int start = 0)
    {
        const int chunk = 512;
        int ch = hz.Length;
        var buf = new float[chunk * ch];
        int total = (int)(Sr * seconds);
        for (int o = 0; o < total; o += chunk)
        {
            for (int n = 0; n < chunk; n++)
                for (int v = 0; v < ch; v++)
                    buf[n * ch + v] = Sine(hz[v], start + o + n, amp[v]);
            choir.Process(buf, chunk, ch);
        }
    }

    [Fact]
    public void GainScalesAChannelBeforeAnalysis()
    {
        var choir = new Choir(Sr, 2);
        choir.SetGainDb(0, -6.0206f); // half
        choir.SetGain(1, 0f);          // muted
        RunSines(choir, new[] { 300.0, 900.0 }, new[] { 0.2f, 0.2f }, 1.0);
        Assert.InRange(choir.GetLevel(0), 0.0695f, 0.0720f); // 0.2 * 0.5 / sqrt2
        Assert.Equal(0f, choir.GetLevel(1));
        Assert.Equal(1, choir.VoicedCount); // the muted channel has no pitch
    }

    [Fact]
    public void GainAffectsTheMix()
    {
        var plain = new Choir(Sr, 2);
        RunSines(plain, new[] { 300.0, 900.0 }, new[] { 0.2f, 0.2f }, 1.0);
        var muted = new Choir(Sr, 2);
        muted.SetGain(1, 0f);
        RunSines(muted, new[] { 300.0, 900.0 }, new[] { 0.2f, 0.2f }, 1.0);
        Assert.True(muted.Mix.Rms < plain.Mix.Rms * 0.8f);
    }

    [Fact]
    public void CalibrationEqualisesChannelLevels()
    {
        var choir = new Choir(Sr, 3);
        var hz = new[] { 262.0, 330.0, 392.0 };
        var amp = new[] { 0.05f, 0.2f, 0.4f };
        choir.StartCalibration();
        RunSines(choir, hz, amp, 1.0);
        Assert.True(choir.IsCalibrating);
        choir.FinishCalibration(0.1f);
        Assert.False(choir.IsCalibrating);

        RunSines(choir, hz, amp, 1.0, start: Sr);
        for (int v = 0; v < 3; v++) Assert.InRange(choir.GetLevel(v), 0.095f, 0.105f);
    }

    [Fact]
    public void CalibrationKeepsGainOfSilentChannel()
    {
        var choir = new Choir(Sr, 2);
        choir.SetGainDb(1, 6f);
        float before = choir.GetGain(1);
        choir.StartCalibration();
        RunSines(choir, new[] { 262.0, 330.0 }, new[] { 0.2f, 0f }, 1.0);
        choir.FinishCalibration();
        Assert.Equal(before, choir.GetGain(1));
    }

    [Fact]
    public void PeakMeterReportsClippingOfRawInput()
    {
        var choir = new Choir(Sr, 1);
        choir.SetGainDb(0, -20f); // trim must not hide input clipping
        RunSines(choir, new[] { 262.0 }, new[] { 1.0f }, 0.2);
        Assert.True(choir.GetPeak(0) >= 0.99f);
    }

    [Fact]
    public void SingleSourceSkipsPitchButKeepsSpectralFeatures()
    {
        var choir = RunChoir(new[] { 261.6 });
        Assert.Equal(ChoirMode.SingleSource, choir.Mode);
        Assert.Equal(0, choir.VoicedCount);
        Assert.Equal(0f, choir.Voices[0].PitchHz);
        Assert.True(choir.Mix.Rms > 0.01f);
        Assert.True(choir.Mix.SpectralCentroid > 100f);
        Assert.Equal(-1, choir.Harmony.RootPitchClass);
    }

    [Fact]
    public void MultiSourceExtractsSpectralFeaturesOnTheMixOnly()
    {
        var choir = RunChoir(new[] { 261.6, 329.6, 392.0, 523.3 });
        Assert.Equal(ChoirMode.MultiSource, choir.Mode);
        Assert.True(choir.Mix.Rms > 0.01f);
        Assert.True(choir.Mix.SpectralCentroid > 100f);
        Assert.Equal(4, choir.VoicedCount); // pitch is still tracked per voice
    }

    [Fact]
    public void MixCentroidLiesBetweenTheSingleVoiceCentroids()
    {
        // Spectral features describe the sum, not the individual voices.
        float low = RunChoir(new[] { 220.0 }).Mix.SpectralCentroid;
        float high = RunChoir(new[] { 880.0 }).Mix.SpectralCentroid;
        float mix = RunChoir(new[] { 220.0, 880.0 }).Mix.SpectralCentroid;
        Assert.True(high > low * 1.5f, $"centroids {low} / {high}");
        Assert.InRange(mix, low, high);
    }

    [Fact]
    public void MixMatchesSumOfVoices()
    {
        // Two identical in-phase voices: the mix at gain 1/sqrt(2) is sqrt(2) times one voice.
        float one = RunChoir(new[] { 261.6 }).Mix.Rms;
        float mix = RunChoir(new[] { 261.6, 261.6 }).Mix.Rms;
        Assert.InRange(mix, one * (float)Math.Sqrt(2) * 0.9f, one * (float)Math.Sqrt(2) * 1.1f);
    }

    private sealed class FakeHarmony : IHarmonyAnalyzer
    {
        public int Calls;
        public HarmonyFrame Analyze(VoiceFrame[] voices)
        {
            Calls++;
            return new HarmonyFrame { RootPitchClass = 0, Confidence = 1f };
        }
    }

    [Fact]
    public void HarmonyRunsOnlyInMultiSourceMode()
    {
        var multi = new Choir(Sr, 2) { HarmonyAnalyzer = new FakeHarmony() };
        var single = new Choir(Sr, 1) { HarmonyAnalyzer = new FakeHarmony() };
        var buf2 = new float[441 * 2];
        var buf1 = new float[441];
        for (int i = 0; i < 20; i++) { multi.Process(buf2, 441, 2); single.Process(buf1, 441, 1); }
        Assert.Equal(0, multi.Harmony.RootPitchClass);
        Assert.Equal(-1, single.Harmony.RootPitchClass);
        Assert.True(((FakeHarmony)multi.HarmonyAnalyzer).Calls > 0);
        Assert.Equal(0, ((FakeHarmony)single.HarmonyAnalyzer).Calls);
    }
}
