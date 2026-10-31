using System;
using Haptics.Dsp;
using Xunit;
using static TestSignals;

public class YinTests
{
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
}
