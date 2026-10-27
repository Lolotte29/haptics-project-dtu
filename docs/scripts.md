# Script map

Goal: features + YIN pitch -> chord / key recognition -> harmonic tension -> (later) haptic mapping.
Only the first part exists. Planned scripts are listed at the bottom and are **not created yet**.

```
audio in -> Voice/ (per singer: features + pitch) -> Choir/ (ensemble: chord, key, tension) -> haptics (later)
              ^ Core/ primitives (FFT, YIN)
```

`Choir` picks its mode from the number of sources (`voiceCount`):

```
1 source   : channel 0 -> VoiceProcessor (pitch OFF) -> Voices[0] = Mix        (features only)

2+ sources : channel i -> VoiceProcessor i (YIN ON)  -> Voices[i] -> IHarmonyAnalyzer -> Harmony
             sum of all channels x 1/sqrt(N) -> VoiceProcessor (pitch OFF)     -> Mix
```

Read the results from `Choir`: `Voices` (per singer), `Mix` (features of the summed signal, or of the single source), `Harmony` (chord; root -1 until an analyzer is plugged in via `Choir.HarmonyAnalyzer`).

Status: **done** = implemented and unit-tested on synthetic signals; **untested** = written, never run in Unity.

## Exists: `unity/Assets/Scripts/Dsp/` (Unity-free, tested by `dsp/`)

| Folder | Script | What it does | Status |
|--------|--------|--------------|--------|
| `Core/` | `Fft.cs` | Radix-2 FFT + Hann window | done |
| `Core/` | `Yin.cs` | YIN pitch detector (100-1100 Hz) | done (CPU cost unmeasured) |
| `Voice/` | `FeatureFrame.cs` | Spectral features of one frame (data) | done |
| `Voice/` | `FeatureExtractor.cs` | RMS, centroid, spread, skewness, entropy, ZCR, flux, onset | done |
| `Voice/` | `VoiceFrame.cs` | Everything known about one singer (data) | done |
| `Voice/` | `VoiceProcessor.cs` | Per-singer: features + YIN -> `VoiceFrame`. `EnablePitch = false` skips YIN | done |
| `Choir/` | `Choir.cs` | Runs N `VoiceProcessor`, plus a summed-mix processor when N >= 2. Fills `Voices`, `Mix`, `Harmony`; `Mode` = single/multi source | done |
| `Choir/` | `Harmony.cs` | `IHarmonyAnalyzer` + `HarmonyFrame` (chord root, confidence): the plug-in point for chord recognition. No implementation yet | interface only |

## Exists: `unity/Assets/Scripts/Unity/`

| Script | What it does | Status |
|--------|--------------|--------|
| `ChoirHost.cs` | Owns `Choir` (`voiceCount` field sets the mode), exposes `HarmonyAnalyzer`, debug display of pitch/note/level per voice, mode, mix centroid/level, chord root | untested in Unity |
| `AudioSourceInput.cs` | Feeds a clip (audio thread) or a capture device read directly from the mic buffer (main thread, VB-Cable) into `ChoirHost` | untested; reads one device's channels only |

Tests: `dsp/Haptics.Dsp.Tests/` (`cd dsp && dotnet test Haptics.Dsp.Tests`).

## Planned (not created): add one at a time, see [roadmap.md](roadmap.md)

| # | Script (in `Dsp/Choir/`) | Input -> output |
|---|--------------------------|-----------------|
| 1 | `PitchClass.cs` | pitch in Hz -> note, pitch class 0..11, cents off |
| 2 | `ChordRecognizer.cs` | all `VoiceFrame` pitches -> chord (root + quality); implements `IHarmonyAnalyzer` |
| 3 | `KeyEstimator.cs` | chord / pitch-class history -> running key (tonic + mode) |
| 4 | `HarmonicTension.cs` | chord + key -> tension 0..1 |
| 5 | extend `HarmonyFrame` | add key and tension next to the chord; exposed as `Choir.Harmony` |
| 6 | `Dsp/Haptic/` | tension + loudness -> intensity/pulse, then the Bluetooth sink |

Rules: one type per file, file named after the type, folder = pipeline stage.
