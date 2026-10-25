using System;
using System.Collections.Generic;
using Haptics.Dsp;
using Xunit;

public class FeatureExtractorTests
{
    private const int Sr = 44100;

    private static float[] Sine(double hz, double seconds, float amp = 1f)
    {
        var x = new float[(int)(Sr * seconds)];
        for (int i = 0; i < x.Length; i++) x[i] = amp * (float)Math.Sin(2 * Math.PI * hz * i / Sr);
        return x;
    }

    private static List<FeatureFrame> Run(float[] x, int chunk = 441)
    {
        var fe = new FeatureExtractor(Sr);
        var frames = new List<FeatureFrame>();
        for (int o = 0; o < x.Length; o += chunk)
            fe.Process(x, o, Math.Min(chunk, x.Length - o), frames.Add);
        return frames;
    }

    [Fact]
    public void FftPeaksAtSineBin()
    {
        var fft = new Fft(1024);
        var input = new float[1024];
        int bin = 40;
        for (int i = 0; i < 1024; i++) input[i] = (float)Math.Sin(2 * Math.PI * bin * i / 1024);
        var mag = new float[fft.BinCount];
        fft.Magnitude(input, mag);
        int argmax = 0;
        for (int i = 1; i < mag.Length; i++) if (mag[i] > mag[argmax]) argmax = i;
        Assert.Equal(bin, argmax);
    }

    [Fact]
    public void RmsOfSineIsAmpOverRootTwo()
    {
        var frames = Run(Sine(440, 1, 0.8f));
        Assert.Equal(0.8f / Math.Sqrt(2), frames[^1].Rms, 2);
    }

    [Fact]
    public void CentroidTracksFrequency()
    {
        float low = Run(Sine(500, 1))[^1].SpectralCentroid;
        float high = Run(Sine(4000, 1))[^1].SpectralCentroid;
        Assert.InRange(low, 400, 700);
        Assert.InRange(high, 3800, 4300);
    }

    [Fact]
    public void TimbreDescriptorsSeparateToneFromNoise()
    {
        var rng = new Random(3);
        var noise = new float[Sr];
        for (int i = 0; i < noise.Length; i++) noise[i] = (float)(rng.NextDouble() * 2 - 1) * 0.5f;

        var tone = Run(Sine(1000, 1))[^1];
        var nz = Run(noise)[^1];

        Assert.True(tone.SpectralEntropy < 0.3f, $"tone entropy {tone.SpectralEntropy}");
        Assert.True(nz.SpectralEntropy > 0.8f, $"noise entropy {nz.SpectralEntropy}");
        Assert.True(nz.SpectralSpread > tone.SpectralSpread * 5);
        Assert.True(nz.ZeroCrossingRate > tone.ZeroCrossingRate);
    }

    [Fact]
    public void ZeroCrossingRateOfSineMatchesFrequency()
    {
        // A sine crosses zero twice per period: rate ~ 2*f/sr.
        float zcr = Run(Sine(1000, 1))[^1].ZeroCrossingRate;
        Assert.InRange(zcr, 2 * 1000f / Sr * 0.95f, 2 * 1000f / Sr * 1.05f);
    }

    [Fact]
    public void ChunkSizeDoesNotChangeResults()
    {
        var x = Sine(1000, 0.5f);
        var a = Run(x, 441);
        var b = Run(x, 1000);
        Assert.Equal(a.Count, b.Count);
        for (int i = 0; i < a.Count; i++) Assert.Equal(a[i].Rms, b[i].Rms, 5);
    }

    [Fact]
    public void DetectsClicksAsOnsets()
    {
        var x = new float[Sr * 2];
        int[] clickAt = { Sr / 2, Sr, Sr * 3 / 2 };
        foreach (int c in clickAt)
            for (int i = 0; i < 200; i++)
                x[c + i] = (float)Math.Sin(2 * Math.PI * 2000 * i / Sr) * (1f - i / 200f);

        var frames = Run(x);
        int onsets = 0;
        foreach (var f in frames) if (f.Onset) onsets++;
        Assert.Equal(clickAt.Length, onsets);
    }

    [Fact]
    public void SilenceHasNoOnsets()
    {
        foreach (var f in Run(new float[Sr])) Assert.False(f.Onset);
    }
}
