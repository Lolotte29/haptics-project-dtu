using System;
using Haptics.Dsp;
using Xunit;

public class ChoirTests
{
    private const int Sr = 44100;

    /// <summary>Harmonic "voice": fundamental plus a few decaying partials.</summary>
    private static float Voice(double hz, int n, float amp = 0.2f)
    {
        double t = (double)n / Sr, s = 0;
        for (int k = 1; k <= 4; k++) s += Math.Sin(2 * Math.PI * hz * k * t) / k;
        return amp * (float)s;
    }

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

    [Theory]
    [InlineData(130.8)]
    [InlineData(220.0)]
    [InlineData(440.0)]
    [InlineData(880.0)]
    public void YinFindsPitch(double hz)
    {
        var yin = new Yin(Sr);
        var x = new float[yin.BufferSize];
        for (int i = 0; i < x.Length; i++) x[i] = Voice(hz, i);
        float f = yin.Detect(x, out float conf);
        Assert.InRange(f, hz * 0.99, hz * 1.01);
        Assert.True(conf > 0.8f);
    }

    [Fact]
    public void YinReportsNoPitchForNoise()
    {
        var rng = new Random(1);
        var yin = new Yin(Sr);
        var x = new float[yin.BufferSize];
        for (int i = 0; i < x.Length; i++) x[i] = (float)(rng.NextDouble() * 2 - 1) * 0.2f;
        Assert.Equal(0f, yin.Detect(x, out _));
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
    public void MultiSourceMixHasSpectralFeaturesButNoPitch()
    {
        var choir = RunChoir(new[] { 261.6, 329.6, 392.0, 523.3 });
        Assert.Equal(ChoirMode.MultiSource, choir.Mode);
        Assert.True(choir.Mix.Rms > 0.01f);
        Assert.True(choir.Mix.SpectralCentroid > 100f);
        Assert.Equal(0f, choir.Mix.PitchHz);
    }

    [Fact]
    public void MixMatchesSumOfVoices()
    {
        // Two identical in-phase voices: the mix at gain 1/sqrt(2) is sqrt(2) times one voice.
        var choir = RunChoir(new[] { 261.6, 261.6 });
        float expected = choir.Voices[0].Rms * (float)Math.Sqrt(2);
        Assert.InRange(choir.Mix.Rms, expected * 0.9f, expected * 1.1f);
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
