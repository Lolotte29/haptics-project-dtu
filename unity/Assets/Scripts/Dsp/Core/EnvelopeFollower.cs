using System;

namespace Haptics.Dsp
{
    /// <summary>
    /// Amplitude envelope follower, evaluated per sample on the rectified input. The output rises with the
    /// attack time constant and falls with the release time constant (one-pole smoothing, so a step reaches
    /// 63 % after one time constant). Holds a single float of state; no allocations.
    /// </summary>
    public sealed class EnvelopeFollower
    {
        private readonly float _sampleRate;
        private float _attackCoef;
        private float _releaseCoef;
        private float _attackMs;
        private float _releaseMs;

        /// <summary>Current envelope value (same scale as the input, 0..1 for full-scale audio).</summary>
        public float Value { get; private set; }

        public EnvelopeFollower(int sampleRate, float attackMs = 5f, float releaseMs = 100f)
        {
            if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            _sampleRate = sampleRate;
            AttackMs = attackMs;
            ReleaseMs = releaseMs;
        }

        /// <summary>Time constant for rising levels in milliseconds. 0 means instant.</summary>
        public float AttackMs
        {
            get => _attackMs;
            set { _attackMs = Math.Max(0f, value); _attackCoef = Coef(_attackMs); }
        }

        /// <summary>Time constant for falling levels in milliseconds. 0 means instant.</summary>
        public float ReleaseMs
        {
            get => _releaseMs;
            set { _releaseMs = Math.Max(0f, value); _releaseCoef = Coef(_releaseMs); }
        }

        private float Coef(float ms) => ms <= 0f ? 1f : 1f - (float)Math.Exp(-1.0 / (0.001 * ms * _sampleRate));

        /// <summary>Feeds one sample and returns the updated envelope.</summary>
        public float Process(float sample)
        {
            float x = Math.Abs(sample);
            Value += (x - Value) * (x > Value ? _attackCoef : _releaseCoef);
            return Value;
        }

        public void Reset() => Value = 0f;
    }
}
