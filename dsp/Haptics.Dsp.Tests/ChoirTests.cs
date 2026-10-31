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

    private static float Sine(double hz, int n, float amp) => amp * (float)Math.Sin(2 * Math.PI * hz * n / Sr);

    /// <summary>Runs per-voice amplitudes for <paramref name="seconds"/>, one pure sine per channel.</summary>
    private static void RunSines(Choir choir, double[] hz, float[] amp, double seconds, int start = 0, Action<Choir> perChunk = null)
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
            perChunk?.Invoke(choir);
        }
    }

    [Fact]
    public void LoudnessIsRmsOfTheSum()
    {
        // Two sines of different frequency are uncorrelated: power adds, RMS = sqrt(0.1^2/2 + 0.1^2/2).
        var choir = new Choir(Sr, 2);
        RunSines(choir, new[] { 300.0, 900.0 }, new[] { 0.1f, 0.1f }, 1.0);
        Assert.InRange(choir.Frame.Loudness, 0.0995f, 0.1005f);
    }

    [Fact]
    public void SpectralFeaturesDescribeTheSum()
    {
        var low = new Choir(Sr, 2);
        RunSines(low, new[] { 300.0, 300.0 }, new[] { 0.1f, 0f }, 1.0);
        var both = new Choir(Sr, 2);
        RunSines(both, new[] { 300.0, 3000.0 }, new[] { 0.1f, 0.1f }, 1.0);
        Assert.InRange(low.Frame.SpectralCentroid, 250f, 350f);
        Assert.InRange(both.Frame.SpectralCentroid, 1000f, 2000f); // between the two voices
        Assert.True(both.Frame.SpectralSpread > low.Frame.SpectralSpread * 3);
    }

    [Fact]
    public void GainScalesAChannelBeforeAnalysis()
    {
        var choir = new Choir(Sr, 2);
        choir.SetGainDb(0, -6.0206f); // half
        choir.SetGain(1, 0f);          // muted
        RunSines(choir, new[] { 300.0, 900.0 }, new[] { 0.2f, 0.2f }, 1.0);
        Assert.InRange(choir.Voices[0].Rms, 0.0695f, 0.0720f); // 0.2 * 0.5 / sqrt2
        Assert.Equal(0f, choir.Voices[1].Rms);
        Assert.Equal(1, choir.Frame.VoicedCount);
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
        for (int v = 0; v < 3; v++) Assert.InRange(choir.Voices[v].Rms, 0.095f, 0.105f);
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
    public void OnsetOfOneVoiceIsDetectedWhileOthersSustain()
    {
        var choir = new Choir(Sr, 2);
        var hz = new[] { 262.0, 392.0 };
        // Voice 1 sustains; settle so its own flux is quiet.
        RunSines(choir, hz, new[] { 0.2f, 0f }, 1.0);
        bool onset = false;
        RunSines(choir, hz, new[] { 0.2f, 0.2f }, 0.3, start: Sr, perChunk: c => onset |= c.Frame.Onset);
        Assert.True(onset);
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
