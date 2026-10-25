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
        Assert.Equal(4, choir.Frame.VoicedCount);
    }

    [Fact]
    public void ClusterIsMoreTenseThanMajorChord()
    {
        // C major triad + octave versus a semitone cluster.
        float major = RunChoir(new[] { 261.6, 329.6, 392.0, 523.3 }).Frame.Roughness;
        float cluster = RunChoir(new[] { 261.6, 277.2, 293.7, 311.1 }).Frame.Roughness;
        Assert.True(cluster > major * 2, $"cluster={cluster} major={major}");
        Assert.InRange(cluster, 0.3f, 1f);
    }

    [Fact]
    public void MinorSecondIsMoreTenseThanFifth()
    {
        float fifth = RunChoir(new[] { 261.6, 392.0 }).Frame.Roughness;
        float second = RunChoir(new[] { 261.6, 277.2 }).Frame.Roughness;
        Assert.True(second > fifth * 2, $"second={second} fifth={fifth}");
    }

    [Fact]
    public void SingleVoiceHasNoRoughness()
    {
        var f = RunChoir(new[] { 261.6 }).Frame;
        Assert.Equal(0f, f.Roughness);
        Assert.Equal(1, f.VoicedCount);
    }

    [Fact]
    public void SilenceProducesNothing()
    {
        var choir = new Choir(Sr, 4);
        var buf = new float[441 * 4];
        for (int i = 0; i < 100; i++) choir.Process(buf, 441, 4);
        Assert.Equal(0, choir.Frame.VoicedCount);
        Assert.Equal(0f, choir.Frame.Roughness);
        Assert.False(choir.Frame.Onset);
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
        Assert.Equal(2, choir.Frame.VoicedCount);
    }

    [Fact]
    public void HapticMapperPulsesOnOnsetAndSmooths()
    {
        var m = new HapticMapper();
        var quiet = m.Map(new ChoirFrame { Loudness = 0f });
        Assert.Equal(0f, quiet.Intensity);
        Assert.False(quiet.Pulse);

        var hit = m.Map(new ChoirFrame { Loudness = 0.3f, Roughness = 1f, Onset = true });
        Assert.True(hit.Pulse);
        Assert.InRange(hit.Intensity, 0.01f, 1f);

        float prev = hit.Intensity;
        var after = m.Map(new ChoirFrame { Loudness = 0f });
        Assert.True(after.Intensity < prev && after.Intensity > 0f); // decays, doesn't drop instantly
    }
}
