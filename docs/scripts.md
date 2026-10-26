# Script map

One row per script, grouped by pipeline stage. Status: **done** = implemented and unit-tested on synthetic signals; **untested** = written, never run in Unity; **stub** = interface only.

```
audio in -> Voice/ (per singer) -> Choir/ (ensemble) -> Haptic/ (one stream) -> IHapticSink
              ^ Core/ primitives
```

## `unity/Assets/Scripts/Dsp/` (Unity-free, tested by `dsp/`)

| Folder | Script | What it does | Status |
|--------|--------|--------------|--------|
| `Core/` | `Fft.cs` | Radix-2 FFT + Hann window | done |
| `Core/` | `Yin.cs` | YIN pitch detector (100-1100 Hz) | done (CPU cost unmeasured) |
| `Voice/` | `FeatureFrame.cs` | Spectral features of one frame (data) | done |
| `Voice/` | `FeatureExtractor.cs` | RMS, centroid, spread, skewness, entropy, ZCR, flux, onset | done |
| `Voice/` | `VoiceFrame.cs` | Everything known about one singer (data) | done |
| `Voice/` | `VoiceProcessor.cs` | Per-singer: features + YIN -> `VoiceFrame` | done |
| `Choir/` | `ChoirFrame.cs` | Ensemble features (data) | done |
| `Choir/` | `ChoirAnalyzer.cs` | Roughness between voices + loudness | done (scale calibrated on synthetic voices only) |
| `Choir/` | `Choir.cs` | Wires N `VoiceProcessor` + `ChoirAnalyzer`; entry point `Process(...)` | done |
| `Haptic/` | `HapticFrame.cs` | Intensity + pulse (data) | done |
| `Haptic/` | `HapticMapper.cs` | `ChoirFrame` -> smoothed `HapticFrame` | untested, weights are guesses |
| `Haptic/` | `IHapticSink.cs` | Output interface | stub (no implementation) |

## `unity/Assets/Scripts/Unity/` (MonoBehaviours)

| Script | What it does | Status |
|--------|--------------|--------|
| `ChoirHost.cs` | Owns `Choir` + `HapticMapper`, debug HUD, calls the sink | untested in Unity |
| `AudioSourceInput.cs` | Feeds an AudioSource / mic / VB-Cable into `ChoirHost` | untested; stereo only |

## Tests

`dsp/Haptics.Dsp.Tests/FeatureExtractorTests.cs`, `ChoirTests.cs`. Run `cd dsp && dotnet test Haptics.Dsp.Tests`. No tests yet for `HapticMapper`.

Rules: one type per file, file named after the type, folder = pipeline stage. New code goes in the stage it belongs to.
