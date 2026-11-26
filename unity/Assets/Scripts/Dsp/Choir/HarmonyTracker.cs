using System;

namespace Haptics.Dsp
{
    /// <summary>
    /// First harmony stage: turns the per-singer pitches into pitch classes and estimates the key.
    /// Per voiced singer and step: smooth the pitch (flattens vibrato), subtract the tracked tuning drift, round to a note
    /// with hysteresis, then add the pitch class to a fast histogram (current harmony, ~0.5 s) and a slow one (key, ~10 s)
    /// with weight = pitch confidence. Unvoiced singers add nothing, so breaths just let the histograms fade.
    /// Chord root stays -1 (chord recognition is not implemented). Western tonal music only.
    /// Not thread-safe: use from the thread that calls <see cref="Choir.Process"/>. No allocation after construction.
    /// </summary>
    public sealed class HarmonyTracker : IHarmonyAnalyzer
    {
        private sealed class VoiceState
        {
            public bool HasPitch, HasNote;
            public double Smoothed;   // smoothed continuous MIDI pitch
            public int Note;          // current integer MIDI note
            public float Gap;         // seconds unvoiced
        }

        private readonly VoiceState[] _voices;

        /// <summary>Pitch smoothing time constant in seconds. ~80 ms cuts 5-7 Hz vibrato by a factor of about 4.</summary>
        public float PitchSmoothSeconds { get; set; } = 0.08f;
        /// <summary>A note only changes when the smoothed pitch is further than 0.5 + this many semitones from it.</summary>
        public float HysteresisSemitones { get; set; } = 0.15f;
        /// <summary>Unvoiced gaps up to this long (consonants, YIN dropouts) keep the singer's state.</summary>
        public float HoldSeconds { get; set; } = 0.15f;

        /// <summary>Current harmony: pitch classes of the last ~0.5 s. Input for tension later.</summary>
        public PitchClassHistogram Fast { get; }
        /// <summary>Key context: pitch classes of the last ~10 s.</summary>
        public PitchClassHistogram Slow { get; }
        public TuningTracker Tuning { get; } = new TuningTracker();
        public KeyEstimator Key { get; } = new KeyEstimator();

        public HarmonyTracker(int voiceCount, float fastSeconds = 0.5f, float slowSeconds = 10f)
        {
            if (voiceCount < 1) throw new ArgumentOutOfRangeException(nameof(voiceCount));
            _voices = new VoiceState[voiceCount];
            for (int i = 0; i < voiceCount; i++) _voices[i] = new VoiceState();
            Fast = new PitchClassHistogram(fastSeconds);
            Slow = new PitchClassHistogram(slowSeconds);
        }

        public HarmonyFrame Analyze(VoiceFrame[] voices, float dtSeconds)
        {
            Fast.Decay(dtSeconds);
            Slow.Decay(dtSeconds);

            double offset = Tuning.OffsetSemitones;
            double residualSum = 0, weightSum = 0;
            float alpha = 1f - (float)Math.Exp(-dtSeconds / PitchSmoothSeconds);
            double switchDistance = 0.5 + HysteresisSemitones;
            int n = Math.Min(voices.Length, _voices.Length);

            for (int i = 0; i < n; i++)
            {
                var s = _voices[i];
                var v = voices[i];
                if (!v.Voiced)
                {
                    s.Gap += dtSeconds;
                    if (s.Gap > HoldSeconds) { s.HasPitch = false; s.HasNote = false; }
                    continue;
                }
                s.Gap = 0f;
                double midi = PitchClass.HzToMidi(v.PitchHz);
                if (!s.HasPitch) { s.Smoothed = midi; s.HasPitch = true; }
                else s.Smoothed += (midi - s.Smoothed) * alpha;

                double corrected = s.Smoothed - offset;
                if (!s.HasNote || Math.Abs(corrected - s.Note) > switchDistance)
                {
                    s.Note = PitchClass.NearestNote(corrected);
                    s.HasNote = true;
                }

                float w = Math.Max(0f, Math.Min(1f, v.PitchConfidence));
                float amount = w * dtSeconds;
                int pc = PitchClass.Of(s.Note);
                Fast.Add(pc, amount);
                Slow.Add(pc, amount);
                residualSum += w * (corrected - s.Note);
                weightSum += w;
            }

            Tuning.Update(residualSum, weightSum, dtSeconds);
            var key = Key.Update(Slow.Bins, dtSeconds);

            return new HarmonyFrame
            {
                RootPitchClass = -1,
                Confidence = 0f,
                KeyTonic = key.Valid ? key.Tonic : -1,
                KeyIsMinor = key.IsMinor,
                KeyConfidence = key.Valid ? key.Confidence : 0f,
            };
        }
    }
}
