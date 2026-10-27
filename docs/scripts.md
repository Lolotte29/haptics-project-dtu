# Script map

Goal: features + YIN pitch -> chord / key recognition -> harmonic tension -> (later) haptic mapping.
Only the first part exists. Planned scripts are listed at the bottom and are **not created yet**.

```
audio in -> Voice/ (per singer: features + pitch) -> Choir/ (ensemble: chord, key, tension) -> haptics (later)
              ^ Core/ primitives (FFT, YIN)
```

Status: **done** = implemented and unit-tested on synthetic signals; **untested** = written, never run in Unity.

## Exists: `unity/Assets/Scripts/Dsp/` (Unity-free, tested by `dsp/`)

| Folder | Script | What it does | Status |
|--------|--------|--------------|--------|
| `Core/` | `Fft.cs` | Radix-2 FFT + Hann window | done |
| `Core/` | `Yin.cs` | YIN pitch detector (100-1100 Hz) | done (CPU cost unmeasured) |
| `Voice/` | `FeatureFrame.cs` | Spectral features of one frame (data) | done |
| `Voice/` | `FeatureExtractor.cs` | RMS, centroid, spread, skewness, entropy, ZCR, flux, onset | done |
| `Voice/` | `VoiceFrame.cs` | Everything known about one singer (data) | done |
| `Voice/` | `VoiceProcessor.cs` | Per-singer: features + YIN -> `VoiceFrame` | done |
| `Choir/` | `Choir.cs` | Runs N `VoiceProcessor`; fills `Voices`. Hook point for the planned stages | done |

## Exists: `unity/Assets/Scripts/Unity/`

| Script | What it does | Status |
|--------|--------------|--------|
| `ChoirHost.cs` | Owns `Choir`, shows a debug display of pitch/note/level per voice | untested in Unity |
| `AudioSourceInput.cs` | Feeds an AudioSource / mic / VB-Cable into `ChoirHost` | untested; stereo only |

Tests: `dsp/Haptics.Dsp.Tests/` (`cd dsp && dotnet test Haptics.Dsp.Tests`).

## Planned (not created): add one at a time, see [roadmap.md](roadmap.md)

| # | Script (in `Dsp/Choir/`) | Input -> output |
|---|--------------------------|-----------------|
| 1 | `PitchClass.cs` | pitch in Hz -> note, pitch class 0..11, cents off |
| 2 | `ChordRecognizer.cs` | all `VoiceFrame` pitches -> chord (root + quality) |
| 3 | `KeyEstimator.cs` | chord / pitch-class history -> running key (tonic + mode) |
| 4 | `HarmonicTension.cs` | chord + key -> tension 0..1 |
| 5 | `ChoirFrame.cs` | holds chord, key, tension; exposed on `Choir` |
| 6 | `Dsp/Haptic/` | tension + loudness -> intensity/pulse, then the Bluetooth sink |

Rules: one type per file, file named after the type, folder = pipeline stage.
