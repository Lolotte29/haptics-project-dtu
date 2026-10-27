using System;

/// <summary>Synthetic signals shared by the test classes.</summary>
internal static class TestSignals
{
    public const int Sr = 44100;

    /// <summary>Harmonic "voice": fundamental plus a few decaying partials.</summary>
    public static float Voice(double hz, int n, float amp = 0.2f)
    {
        double t = (double)n / Sr, s = 0;
        for (int k = 1; k <= 4; k++) s += Math.Sin(2 * Math.PI * hz * k * t) / k;
        return amp * (float)s;
    }
}
