using System;

namespace Haptics.Dsp
{
    /// <summary>Turns a <see cref="ChoirFrame"/> into a single smoothed haptic stream.</summary>
    public sealed class HapticMapper
    {
        public float LoudnessWeight = 0.4f;
        public float RoughnessWeight = 0.6f;
        /// <summary>Loudness is multiplied by this before weighting (typical sung RMS is well below 1).</summary>
        public float LoudnessGain = 6f;
        /// <summary>Smoothing per frame, 0..1 (higher = faster).</summary>
        public float Attack = 0.5f;
        public float Release = 0.1f;

        private float _intensity;

        public HapticFrame Map(ChoirFrame c)
        {
            float target = Math.Min(1f, c.Loudness * LoudnessGain) * (LoudnessWeight + RoughnessWeight * c.Roughness);
            target = Math.Min(1f, target);
            float k = target > _intensity ? Attack : Release;
            _intensity += (target - _intensity) * k;
            return new HapticFrame { Intensity = _intensity, Pulse = c.Onset };
        }
    }
}
