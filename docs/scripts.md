# Script map

Goal: features + YIN pitch -> chord / key recognition -> harmonic tension -> (later) haptic mapping.
Only the first part exists. Planned scripts are listed at the bottom and are **not created yet**.

```
audio in -> Voice/ (per singer: pitch; features on the mix) -> Choir/ (ensemble: chord, key, tension) -> haptics (later)
              ^ Core/ primitives (FFT, YIN)
```

YIN is switched on or off for every source separately (`Choir.SetPitchEnabled`, default on), whatever the number of sources:

```
channel i (YIN on)  -> PitchTracker i -> Voices[i] -+-> IHarmonyAnalyzer -> Harmony   (only sources with YIN on)
channel i (YIN off) -> Voices[i] stays unvoiced ----+
sum of all channels x 1/sqrt(N) -> FeatureExtractor                      -> Mix       (always)
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
| `Voice/` | `VoiceFrame.cs` | What is known about one singer: pitch and confidence (data) | done |
| `Voice/` | `PitchTracker.cs` | Per-singer YIN pitch with a silence gate -> `VoiceFrame` | done |
| `Choir/` | `Choir.cs` | Runs N `PitchTracker` (N >= 2) and one `FeatureExtractor` on the sum (or the single channel). Fills `Voices` (pitch), `Mix` (`FeatureFrame`), `Harmony` | done |
| `Choir/` | `HarmonyFrame.cs` | Harmony result: chord root + confidence (root is -1 until chord recognition exists), key tonic / mode / confidence; later tension | done (data) |
| `Choir/` | `PitchClass.cs` | Hz -> MIDI note, pitch class 0..11, cents off, note names | done, tested |
| `Choir/` | `PitchClassHistogram.cs` | 12-bin leaky histogram of pitch classes (fast ~0.5 s for the current harmony, slow ~10 s for the key) | done, tested |
| `Choir/` | `TuningTracker.cs` | Slow, unwrapped feedback loop on the rounding error: follows choir tuning drift | done, tested |
| `Choir/` | `KeyEstimator.cs` | Slow histogram -> key (24 Temperley profiles, correlation) with switch hysteresis | done, tested on synthetic progressions |
| `Choir/` | `HarmonicTension.cs` | Tonal Interval Space tension (Bernardes et al. 2016): 12-bin pitch-class vector -> DFT coefficients 1..6 with consonance weights; tension = 0.6 x distance to the key's tonic triad + 0.4 x dissonance (short vector). No chord labels. Constants are first guesses | done, synthetic tests only |
| `Choir/` | `HarmonyTracker.cs` | `IHarmonyAnalyzer`: per voice smooth pitch, subtract drift, round with hysteresis, fill histograms, estimate key. Set on `ChoirHost` via *Estimate Key* | done, synthetic tests only; not yet checked on real recordings |
| `Haptic/` | `HapticPacket.cs` | 5-byte UDP packet (sequence + envelope) of the older `UdpHapticSender`; the bridge in use sends 1 byte, see [esp-wifi.md](esp-wifi.md) | done |
| `Choir/` | `IHarmonyAnalyzer.cs` | Plug-in point for harmony analysis, `Analyze(voices, dtSeconds)`; set as `Choir.HarmonyAnalyzer`. Implemented by `HarmonyTracker` | done |

## Exists: `unity/Assets/Scripts/Unity/`

| Script | What it does | Status |
|--------|--------------|--------|
| `ChoirHost.cs` | Owns `Choir` (`voiceCount` field sets the mode), exposes `HarmonyAnalyzer`, debug display of pitch/note per voice, mode, mix centroid/level, chord root | untested in Unity |
| `FeatureVisualizer.cs` | Optional; add to the `Choir` GameObject. Scrolling plots: pitch with one line per singer, then RMS, centroid, spread, skewness, entropy, ZCR, flux + onsets of the mix | untested in Unity |
| `ChoirHostBridge.cs` | Class `ChoirHapticAdvancedBridge` (the sender in use): source switch *Envelope* (envelope + flux + onset -> gate, gamma, soft clip, smoothing) or *Harmony* (tension -> intensity, slower smoothing, only while singing), buttons in the Game view -> one intensity byte over UDP to one or several ESPs, resent at least every keep-alive interval; errors logged once | works with the ESP (per the author); keep-alive, multi-target and error handling added, not yet tried in Unity. The file name differs from the class name |
| `UdpHapticSender.cs` | Older sender, not used in the scene: own thread sends `ChoirHost.Envelope` as a 5-byte packet over UDP to a list of ESP IPs (default 200 pkt/s), status line in the Game view | untested in Unity; packet format unit-tested |
| `AudioSourceInput.cs` | Feeds a clip (audio thread) or a capture device read directly from the mic buffer (main thread, VB-Cable) into `ChoirHost` | untested; reads one device's channels only |
| `MultiClipInput.cs` | One mono clip per singer (e.g. Dagstuhl ChoirSet) -> channel i / voice i; plays them (monitor) and feeds the analysis on the audio clock | untested in Unity; the interleaving is `Dsp/Core/ChannelMap` (tested). A live multi-mic adapter would be a sibling calling `ChoirHost.Push` |

Tests: `dsp/Haptics.Dsp.Tests/` (`cd dsp && dotnet test Haptics.Dsp.Tests`), one file per tested area: `YinTests`, `FeatureExtractorTests`, `PitchRobustnessTests`, `ChoirTests` (modes, mix, harmony hook), with shared synthetic signals in `TestSignals`.

## Planned (not created): add one at a time, see [roadmap.md](roadmap.md)

| # | Script (in `Dsp/Choir/`) | Input -> output |
|---|--------------------------|-----------------|
| 2 | `ChordRecognizer.cs` | all `VoiceFrame` pitches -> chord (root + quality); implements `IHarmonyAnalyzer` |
| 4 | 5 | extend `HarmonyFrame` | add tension next to the key and chord |
| 6 | `Dsp/Haptic/` | tension + loudness -> intensity/pulse, then the Bluetooth sink |

Rules: one type per file, file named after the type, folder = pipeline stage.
