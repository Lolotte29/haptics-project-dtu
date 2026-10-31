using System;
using Haptics.Dsp;
using Xunit;
using Xunit.Abstractions;

public class PitchRobustnessTests
{
    private readonly ITestOutputHelper _out;
    public PitchRobustnessTests(ITestOutputHelper o) { _out = o; }

    /// <summary>
    /// Sung-like tone: vibrato, optional glide, vowel-like spectrum (strong 2nd/3rd partial, weak fundamental),
    /// breath noise and a slow amplitude envelope. Returns the true f0 (Hz) per sample through <paramref name="f0"/>.
    /// </summary>
    private static float[] Sing(int sr, double seconds, double startHz, double endHz, double fundamentalGain,
                                double noise, int seed, out double[] f0)
    {
        int n = (int)(sr * seconds);
        var x = new float[n];
        f0 = new double[n];
        var rng = new Random(seed);
        double phase = 0;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / n;
            double vib = Math.Pow(2, 0.5 / 12 * Math.Sin(2 * Math.PI * 5.5 * i / sr)); // +-50 cents
            double hz = (startHz + (endHz - startHz) * t) * vib;
            f0[i] = hz;
            phase += 2 * Math.PI * hz / sr;
            double s = fundamentalGain * Math.Sin(phase)
                     + 1.0 * Math.Sin(2 * phase + 0.3)
                     + 0.8 * Math.Sin(3 * phase + 1.1)
                     + 0.4 * Math.Sin(4 * phase)
                     + 0.2 * Math.Sin(5 * phase);
            double env = 0.15 * (0.6 + 0.4 * Math.Sin(2 * Math.PI * 0.7 * i / sr));
            x[i] = (float)(env * s * 0.4 + noise * (rng.NextDouble() * 2 - 1));
        }
        return x;
    }

    /// <summary>Fraction of voiced-reported frames that are off by more than a quarter tone, and the unvoiced fraction.</summary>
    private (double wrong, double dropped, int frames) Score(float[] x, double[] f0, int sr)
    {
        var vp = new PitchTracker(sr);
        int wrong = 0, dropped = 0, frames = 0;
        for (int i = 0; i < x.Length; i++)
        {
            if (!vp.Process(x[i])) continue;
            if (i < sr / 4) continue; // settle
            frames++;
            var v = vp.Latest;
            double truth = f0[i - 384]; // YIN window centre is roughly 400 samples back
            if (!v.Voiced) { dropped++; continue; }
            double cents = 1200 * Math.Log(v.PitchHz / truth, 2);
            if (Math.Abs(cents) > 50) wrong++;
        }
        return ((double)wrong / frames, (double)dropped / frames, frames);
    }

    [Fact]
    public void ToleratesSmallBufferGaps()
    {
        // 32-sample (0.7 ms) dropout at every 1024-sample buffer boundary must not disturb tracking.
        // Gaps of ~128 samples already break YIN (about half the frames), so inputs must not glitch like that:
        // that is why the microphone is read directly instead of being looped through an AudioSource.
        const int sr = 48000;
        var x = Sing(sr, 3.0, 262, 262, 0.6, 0.002, 3, out var f0);
        for (int i = 0; i + 32 < x.Length; i += 1024)
            for (int k = 0; k < 32; k++) x[i + k] = 0f;
        var (wrong, dropped, _) = Score(x, f0, sr);
        Assert.True(wrong < 0.02 && dropped < 0.10, $"wrong {wrong:P1}, dropped {dropped:P1}");
    }

    [Theory]
    [InlineData(48000, 196.0, 196.0, 0.3, 0.000)]  // G3, weak fundamental
    [InlineData(48000, 220.0, 220.0, 1.0, 0.002)]  // A3
    [InlineData(48000, 262.0, 330.0, 0.6, 0.002)]  // glide C4->E4
    [InlineData(48000, 440.0, 440.0, 0.3, 0.003)]  // A4
    [InlineData(48000, 659.0, 784.0, 0.2, 0.003)]  // E5->G5
    [InlineData(44100, 175.0, 175.0, 0.2, 0.003)]  // F3, low alto
    public void TracksSungVoice(int sr, double startHz, double endHz, double fundGain, double noise)
    {
        var x = Sing(sr, 3.0, startHz, endHz, fundGain, noise, 7, out var f0);
        var (wrong, dropped, frames) = Score(x, f0, sr);
        _out.WriteLine($"{startHz}->{endHz} Hz gain {fundGain}: wrong {wrong:P1}, dropped {dropped:P1} of {frames}");
        Assert.True(wrong < 0.02, $"wrong {wrong:P1}");
        Assert.True(dropped < 0.10, $"dropped {dropped:P1}");
    }
}
