# Project context

Real-time musical feature extraction in Unity for a haptics project (DTU). Read this before changing anything.

## Goal

Analyse live singing and turn it into features that will drive haptic actuators.

- **Concept:** a music-enhancement idea. Singers wear nothing. Selected **audience members** wear haptic bracelets (ESP, Bluetooth) and feel the music as vibration. Very likely **every wearer gets the same signal**: one broadcast stream derived from all voices, no per-wearer routing.
- **Primary use case:** a 4-person women's choir, **one mic per singer** (4 mono channels). Don't hard-code "4" or a device: channel count and source are configuration.
- **Real-time:** analysis runs on the audio thread. No allocations, locks, logging or Unity API calls there.

## Status of the hardware

- Input today: **VB-Audio Virtual Cable** (stereo). Later: several mics straight into the computer, either one multichannel interface or several devices (undecided).
- Output (**not started**): ESP over Bluetooth to a bracelet. Only the `IHapticSink` interface exists.

Decided so far: 4 mics plugged into one computer (how is open, see `docs/mic-input.md`); latency "as good as possible" (no number); probably fewer than 5 bracelets with up to 4 actuators each; Bluetooth is a later problem.

Open questions (ask the user, don't assume): mic/interface model, how the mics are connected, latency budget, BLE vs Classic and how one stream is broadcast to many bracelets.

## Architecture

```
audio in (channel i = singer i)
  -> per-channel gain trim (Choir.SetGainDb; set in the HUD before the concert)
  -> VoiceProcessor x N : level, onset, YIN pitch (per voice)                     -> VoiceFrame
  -> FeatureExtractor   : loudness + spectral descriptors on the SUM of voices
  -> ChoirAnalyzer      : roughness from voice pitches + the sum's features       -> ChoirFrame
  -> HapticMapper       : one smoothed intensity + onset pulse                    -> HapticFrame
  -> IHapticSink        : (future) Bluetooth to ESP
```

Design decision: standard features (loudness, spectral shape) are computed once on the sum of the voices because every wearer gets one shared signal. Only YIN pitch, level and onsets stay per voice.

`Choir` wires the first three stages: `Choir.Process(interleavedSamples, frames, channels)`. It also holds the per-channel gains, input peak meters and a level calibration (`StartCalibration` / `FinishCalibration`).

- `unity/Assets/Scripts/Dsp/`: Unity-free (own asmdef, `noEngineReferences`). Single source of truth.
- `unity/Assets/Scripts/Unity/`: `ChoirHost` (owns the pipeline and mapper, HUD, optional sink) and `AudioSourceInput` (feeds an AudioSource, or a mic/VB-Cable device looped through it).
- `dsp/`: builds those files as `netstandard2.1` and tests them with xUnit (27 tests).
- `docs/`: `unity-setup.md`, `mic-input.md` (input options for the supervisor), `concert-manual.md` (sound check and gains), `testing-features.md` (what to expect per feature).

## Honest state

- **Scope:** the focus is the standard features (level, spectral descriptors, onsets, loudness) feeding the haptic stream. Harmony analysis (and note-level extraction) is important but **deferred**: don't propose or work on it unless the user brings it up.
- **Runs in Unity** and compiles. YIN tracked a simple sung scale well; it has not been tried on dense or real choir/pop material. Other DSP is only verified on synthetic signals.
- Spectral descriptors follow the MathWorks definitions used by the RASTA toolbox (AAU). Its public code (razvysme/Unity3D_Spectral_Analysis) was reviewed and not adopted: no license, static single-instance state, blocks the main thread, unwindowed FFT.
- `ChoirAnalyzer` computes **roughness** (Plomp-Levelt/Sethares) from the YIN pitches. Its `Scale` constant (a gain mapping raw roughness to ~0..1) is calibrated on synthetic voices only.
- The new gain/calibration HUD, the summed spectral features and per-voice onsets are covered by unit tests only; the HUD (IMGUI) has not been looked at in the editor yet.
- `AudioSourceInput` only sees one AudioSource's channels (Unity may down-mix; `Microphone` is normally mono), so true 4-mic input needs another adapter, e.g. keijiro/Lasp (Unlicense, desktop, per-device/channel input; multi-stream behaviour unverified).
- YIN cost on the audio thread (4 voices x ~86 frames/s) is unmeasured.

## Rules for agents

- Keep `Dsp/` free of `UnityEngine`, C# 9 / netstandard2.1, allocation-free in the hot path.
- Every DSP change gets a test in `dsp/Haptics.Dsp.Tests`. Run `cd dsp && dotnet test Haptics.Dsp.Tests` (the test project uses `RollForward=Major` because only .NET 10 may be installed).
- Don't hand-write Unity `.meta`, scenes or prefabs. Never commit `Library/`, `Temp/`, `Logs/`, `UserSettings/`. Large audio goes through Git LFS.
- Agents here can't run Unity: say what was verified by unit tests and what needs checking in the editor.
- Work on a branch; no force-push or history rewrite on `main`.
