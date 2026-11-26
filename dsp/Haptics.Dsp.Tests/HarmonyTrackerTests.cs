using System;
using Haptics.Dsp;
using Xunit;

public class PitchClassTests
{
    [Fact]
    public void ReferenceFrequenciesMapToTheRightNotes()
    {
        Assert.Equal(69.0, PitchClass.HzToMidi(440.0), 6);
        Assert.Equal(9, PitchClass.Of(PitchClass.NearestNote(PitchClass.HzToMidi(440.0))));
        Assert.Equal(0, PitchClass.Of(PitchClass.NearestNote(PitchClass.HzToMidi(261.63))));
        Assert.Equal(0, PitchClass.Of(PitchClass.NearestNote(PitchClass.HzToMidi(130.81)))); // octave-insensitive
    }

    [Fact]
    public void NegativeNotesWrapAndNamesWork()
    {
        Assert.Equal(11, PitchClass.Of(-1));
        Assert.Equal("F#", PitchClass.Name(6));
    }

    [Fact]
    public void CentsOffIsSignedAndBounded()
    {
        Assert.Equal(30.0, PitchClass.CentsOffNearest(60.3), 6);
        Assert.Equal(-30.0, PitchClass.CentsOffNearest(59.7), 6);
    }
}

public class PitchClassHistogramTests
{
    [Fact]
    public void DecaysByOneOverEAfterOneTimeConstant()
    {
        var h = new PitchClassHistogram(0.5f);
        h.Add(3, 1f);
        for (int i = 0; i < 50; i++) h.Decay(0.01f);
        Assert.Equal(Math.Exp(-1), h.Bins[3], 3);
    }

    [Fact]
    public void AddWrapsPitchClassAndTotalSums()
    {
        var h = new PitchClassHistogram(1f);
        h.Add(14, 2f); // 14 -> pitch class 2
        h.Add(2, 1f);
        Assert.Equal(3f, h.Bins[2]);
        Assert.Equal(3f, h.Total);
    }
}

public class HarmonyTrackerTests
{
    private const float Dt = 0.01f;

    private static float Hz(double midi) => (float)(440.0 * Math.Pow(2.0, (midi - 69.0) / 12.0));

    // Voicing of the progressions, MIDI notes, bass first.
    private static readonly int[][] CMajor =
    {
        new[] { 48, 60, 64, 67 }, // I   C E G
        new[] { 53, 60, 65, 69 }, // IV  F A C
        new[] { 55, 59, 62, 67 }, // V   G B D
        new[] { 48, 60, 64, 67 }, // I
    };
    private static readonly int[][] AMinor =
    {
        new[] { 45, 57, 60, 64 }, // i   A C E
        new[] { 50, 57, 62, 65 }, // iv  D A D F
        new[] { 52, 56, 59, 64 }, // V   E G# B E
        new[] { 45, 57, 60, 64 }, // i
    };

    /// <summary>Plays a progression (each chord 2 s) for <paramref name="seconds"/>, looping, transposed, with a pitch error in semitones that may change over time.</summary>
    private static HarmonyFrame Play(HarmonyTracker t, int[][] prog, double seconds, int transpose = 0,
                                     Func<double, double> error = null, Func<double, double> vibrato = null,
                                     float confidence = 0.9f, double startTime = 0)
    {
        var voices = new VoiceFrame[prog[0].Length];
        HarmonyFrame last = default;
        for (double time = startTime; time < startTime + seconds; time += Dt)
        {
            int chord = (int)(time / 2.0) % prog.Length;
            double err = error != null ? error(time) : 0.0;
            double vib = vibrato != null ? vibrato(time) : 0.0;
            for (int v = 0; v < voices.Length; v++)
                voices[v] = new VoiceFrame { PitchHz = Hz(prog[chord][v] + transpose + err + vib), PitchConfidence = confidence };
            last = t.Analyze(voices, Dt);
        }
        return last;
    }

    [Fact]
    public void NoEvidenceMeansNoKey()
    {
        var t = new HarmonyTracker(4);
        var silent = new VoiceFrame[4];
        HarmonyFrame f = default;
        for (int i = 0; i < 500; i++) f = t.Analyze(silent, Dt);
        Assert.Equal(-1, f.KeyTonic);
        Assert.Equal(-1, f.RootPitchClass);
        Assert.Equal(0f, t.Slow.Total);
    }

    [Fact]
    public void CMajorProgressionGivesCMajor()
    {
        var f = Play(new HarmonyTracker(4), CMajor, 12);
        Assert.Equal(0, f.KeyTonic);
        Assert.False(f.KeyIsMinor);
        Assert.True(f.KeyConfidence > 0f);
    }

    [Fact]
    public void AMinorProgressionGivesAMinorNotItsRelativeMajor()
    {
        var f = Play(new HarmonyTracker(4), AMinor, 12);
        Assert.Equal(9, f.KeyTonic);
        Assert.True(f.KeyIsMinor);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(6)]
    [InlineData(-3)]
    public void TransposedProgressionsGiveTheTransposedKey(int semitones)
    {
        var f = Play(new HarmonyTracker(4), CMajor, 12, transpose: semitones);
        Assert.Equal(((semitones % 12) + 12) % 12, f.KeyTonic);
        Assert.False(f.KeyIsMinor);
    }

    [Fact]
    public void VibratoOnASharpSingerStaysOnOneNote()
    {
        // One voice holds A4, 20 cents sharp, with +-50 cents vibrato at 6 Hz.
        var t = new HarmonyTracker(1);
        var v = new VoiceFrame[1];
        for (double time = 0; time < 5; time += Dt)
        {
            v[0] = new VoiceFrame { PitchHz = Hz(69 + 0.2 + 0.5 * Math.Sin(2 * Math.PI * 6 * time)), PitchConfidence = 0.9f };
            t.Analyze(v, Dt);
        }
        Assert.True(t.Slow.Bins[9] > 0.98f * t.Slow.Total, "all weight on A");
    }

    [Fact]
    public void FollowsAChoirDriftingSharpBy70Cents()
    {
        var t = new HarmonyTracker(4);
        var f = Play(t, CMajor, 40, error: time => 0.7 * time / 40.0);
        Assert.Equal(0, f.KeyTonic);
        Assert.False(f.KeyIsMinor);
        Assert.InRange(t.Tuning.OffsetSemitones, 0.45, 0.8); // true drift 0.7, minus the loop's lag (~rate x 8 s = 0.14)
    }

    [Fact]
    public void FollowsAChoirSinkingBy60Cents()
    {
        var t = new HarmonyTracker(4);
        var f = Play(t, AMinor, 40, error: time => -0.6 * time / 40.0);
        Assert.Equal(9, f.KeyTonic);
        Assert.True(f.KeyIsMinor);
        Assert.InRange(t.Tuning.OffsetSemitones, -0.75, -0.45);
    }

    [Fact]
    public void UnvoicedSingersAddNothing()
    {
        var t = new HarmonyTracker(2);
        var v = new VoiceFrame[2];
        v[0] = new VoiceFrame { PitchHz = Hz(60), PitchConfidence = 1f };
        for (int i = 0; i < 300; i++) t.Analyze(v, Dt); // voice 1 stays unvoiced
        Assert.True(t.Slow.Bins[0] > 0f);
        float others = t.Slow.Total - t.Slow.Bins[0];
        Assert.Equal(0f, others, 4);
    }

    [Fact]
    public void ShortDropoutsDoNotResetANote()
    {
        var t = new HarmonyTracker(1);
        var v = new VoiceFrame[1];
        for (int i = 0; i < 100; i++)
        {
            bool gap = i % 20 >= 17; // 30 ms gaps, shorter than the hold time
            v[0] = gap ? default : new VoiceFrame { PitchHz = Hz(62.4), PitchConfidence = 1f };
            t.Analyze(v, Dt);
        }
        Assert.True(t.Slow.Bins[2] > 0.99f * t.Slow.Total); // D, not E from a re-rounded 62.4
    }

    [Fact]
    public void KeyDoesNotFlipOnABriefExcursionButFollowsALongModulation()
    {
        var t = new HarmonyTracker(4);
        Play(t, CMajor, 30);
        var f = Play(t, CMajor, 1, transpose: 7, startTime: 30); // one second of G major
        Assert.Equal(0, f.KeyTonic);

        f = Play(t, CMajor, 30, transpose: 7, startTime: 31);    // then it stays in G major
        Assert.Equal(7, f.KeyTonic);
        Assert.False(f.KeyIsMinor);
    }

    [Fact]
    public void ExtraVoicesBeyondTheTrackersCapacityAreIgnored()
    {
        var t = new HarmonyTracker(2);
        var voices = new VoiceFrame[4];
        for (int i = 0; i < 4; i++) voices[i] = new VoiceFrame { PitchHz = Hz(60 + i), PitchConfidence = 1f };
        t.Analyze(voices, Dt);
        Assert.Equal(2 * Dt, t.Slow.Total, 4);
    }

    [Fact]
    public void WorksInsideChoirWithRealAudio()
    {
        // C major chord sung by 4 synthetic voices through the real pipeline: pitch classes C, E, G end up in the histogram.
        const int sr = TestSignals.Sr;
        var tracker = new HarmonyTracker(4);
        var choir = new Choir(sr, 4) { HarmonyAnalyzer = tracker };
        double[] midi = { 48, 60, 64, 67 };
        var buf = new float[441 * 4];
        int n = 0;
        for (int chunk = 0; chunk < 300; chunk++) // 3 s
        {
            for (int f = 0; f < 441; f++, n++)
                for (int v = 0; v < 4; v++) buf[f * 4 + v] = TestSignals.Voice(440.0 * Math.Pow(2.0, (midi[v] - 69.0) / 12.0), n, 0.3f);
            choir.Process(buf, 441, 4);
        }
        var bins = tracker.Slow.Bins;
        float chord = bins[0] + bins[4] + bins[7];
        Assert.True(chord > 0.9f * tracker.Slow.Total, $"C-E-G share {chord / tracker.Slow.Total:0.00}");
    }
}
