using System;
using System.Collections.Generic;
using Haptics.Dsp;
using Xunit;

public class EnvelopeFollowerTests
{
    private const int Sr = 44100;

    private static float Run(EnvelopeFollower e, float input, double ms)
    {
        int n = (int)(Sr * ms / 1000.0);
        for (int i = 0; i < n; i++) e.Process(input);
        return e.Value;
    }

    [Fact]
    public void StepReaches63PercentAfterOneAttackTimeConstant()
    {
        var e = new EnvelopeFollower(Sr, attackMs: 10f, releaseMs: 100f);
        Assert.Equal(1 - Math.Exp(-1), Run(e, 1f, 10), 2);
    }

    [Fact]
    public void ReleaseIsSlowerThanAttack()
    {
        var e = new EnvelopeFollower(Sr, attackMs: 5f, releaseMs: 200f);
        Run(e, 1f, 100);                       // fully attacked
        float after = Run(e, 0f, 200);         // one release time constant of silence
        Assert.Equal(Math.Exp(-1), after, 2);
    }

    [Fact]
    public void ZeroTimesAreInstant()
    {
        var e = new EnvelopeFollower(Sr, 0f, 0f);
        Assert.Equal(0.7f, e.Process(0.7f), 5);
        Assert.Equal(0f, e.Process(0f), 5);
    }

    [Fact]
    public void NegativeSamplesAreRectified()
    {
        var e = new EnvelopeFollower(Sr, 0f, 100f);
        Assert.Equal(0.5f, e.Process(-0.5f), 5);
    }

    [Fact]
    public void SineEnvelopeSitsNearAmplitudeAndVariesLittle()
    {
        var e = new EnvelopeFollower(Sr, 5f, 100f);
        float min = float.MaxValue, max = 0;
        for (int i = 0; i < Sr; i++)
        {
            float v = e.Process(0.8f * (float)Math.Sin(2 * Math.PI * 220 * i / Sr));
            if (i > Sr / 2) { min = Math.Min(min, v); max = Math.Max(max, v); }
        }
        Assert.InRange(min, 0.4f, 0.8f);
        Assert.InRange(max, 0.5f, 0.8f);
        Assert.True(max - min < 0.2f);
    }

    [Fact]
    public void FeatureFrameCarriesEnvelopeAndItDecaysAfterSignalStops()
    {
        var fe = new FeatureExtractor(Sr);
        var frames = new List<FeatureFrame>();
        var loud = new float[Sr / 2];
        for (int i = 0; i < loud.Length; i++) loud[i] = 0.8f * (float)Math.Sin(2 * Math.PI * 440 * i / Sr);
        fe.Process(loud, 0, loud.Length, frames.Add);
        float during = frames[^1].Envelope;
        fe.Process(new float[Sr / 2], 0, Sr / 2, frames.Add);
        float after = frames[^1].Envelope;
        Assert.True(during > 0.3f);
        Assert.True(after < during * 0.05f);
    }
}

public class ChoirEnvelopeTuningTests
{
    [Fact]
    public void ChoirForwardsAttackAndReleaseToTheFollower()
    {
        var choir = new Choir(44100, 1);
        choir.EnvelopeAttackMs = 20f;
        choir.EnvelopeReleaseMs = 300f;
        Assert.Equal(20f, choir.EnvelopeAttackMs);
        Assert.Equal(300f, choir.EnvelopeReleaseMs);
    }
}
