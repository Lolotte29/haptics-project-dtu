using System;

namespace Haptics.Dsp
{
    /// <summary>
    /// 12-bin leaky histogram of pitch classes: "which notes sounded lately". Every <see cref="Decay"/> fades the
    /// bins with time constant <see cref="TauSeconds"/>; <see cref="Add"/> puts weight on a bin. Callers add
    /// weight x dt, so a bin's value is in weighted voice-seconds and does not depend on the update rate.
    /// </summary>
    public sealed class PitchClassHistogram
    {
        public readonly float[] Bins = new float[12];

        public float TauSeconds { get; set; }

        public PitchClassHistogram(float tauSeconds)
        {
            if (!(tauSeconds > 0f)) throw new ArgumentOutOfRangeException(nameof(tauSeconds));
            TauSeconds = tauSeconds;
        }

        public void Decay(float dtSeconds)
        {
            if (dtSeconds <= 0f) return;
            float k = (float)Math.Exp(-dtSeconds / TauSeconds);
            for (int i = 0; i < 12; i++) Bins[i] *= k;
        }

        public void Add(int pitchClass, float weight) => Bins[PitchClass.Of(pitchClass)] += weight;

        public float Total
        {
            get
            {
                float t = 0f;
                for (int i = 0; i < 12; i++) t += Bins[i];
                return t;
            }
        }

        public void Clear() => Array.Clear(Bins, 0, 12);
    }
}
