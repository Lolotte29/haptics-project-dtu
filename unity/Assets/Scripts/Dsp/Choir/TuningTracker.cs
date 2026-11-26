using System;

namespace Haptics.Dsp
{
    /// <summary>
    /// Follows the choir's overall tuning drift. <see cref="OffsetSemitones"/> is subtracted from every pitch before
    /// it is rounded to a note, so a choir that sinks or goes sharp is still read as the same notes.
    /// It is a slow feedback loop on the rounding error (not a circular mean of raw pitches): the offset is
    /// unwrapped, so a drift of more than half a semitone is followed continuously instead of flipping notes.
    /// Symmetric vibrato averages out; a single out-of-tune singer is diluted by the others.
    /// </summary>
    public sealed class TuningTracker
    {
        /// <summary>Current correction in semitones (positive = the choir is sharp of equal temperament).</summary>
        public double OffsetSemitones { get; private set; }

        /// <summary>Adaptation time constant in seconds. Slow, so that individual notes do not move it.</summary>
        public float TauSeconds { get; set; } = 8f;

        /// <summary>
        /// Moves the offset towards the mean residual of this step. <paramref name="weightedResidualSum"/> is the sum over
        /// voices of weight x (corrected pitch - assigned note) in semitones; <paramref name="weightSum"/> the sum of weights.
        /// </summary>
        public void Update(double weightedResidualSum, double weightSum, float dtSeconds)
        {
            if (!(weightSum > 0.0) || dtSeconds <= 0f) return;
            double alpha = 1.0 - Math.Exp(-dtSeconds / TauSeconds);
            OffsetSemitones += alpha * (weightedResidualSum / weightSum);
        }

        public void Reset() => OffsetSemitones = 0.0;
    }
}
