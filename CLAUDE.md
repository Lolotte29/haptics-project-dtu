# Project context

Real-time musical feature extraction in Unity for a haptics project (DTU). Read this before changing anything.

## Goal

Analyse live singing and turn it into features that will drive haptic actuators.

- **Concept:** a music-enhancement idea. Singers wear nothing. Selected **audience members** wear haptic bracelets (ESP, Bluetooth) and feel the music as vibration. Very likely **every wearer gets the same signal**: one broadcast stream derived from all voices, no per-wearer routing.
- **Primary use case:** a 4-person women's choir, **one mic per singer** (4 mono channels). Don't hard-code "4" or a device: channel count and source are configuration.
- **Real-time:** analysis runs on the audio thread. No allocations, locks, logging or Unity API calls there.

## Status of the hardware

- Input today: **VB-Audio Virtual Cable** (stereo). Later: several mics straight into the computer, either one multichannel interface or several devices (undecided).
- Output (**not started**): ESP over Bluetooth to a bracelet. No haptic code exists; it is deliberately postponed until harmonic tension works.

Open questions (ask the user, don't assume): mic/interface model, latency budget (mic to vibration), number of bracelets and actuators, BLE vs Classic and how one stream is broadcast to many bracelets, how 4 voices combine into one stream.

## Architecture

```
audio in (channel i = singer i)
  -> VoiceProcessor x N : level, spectral shape (FeatureExtractor), YIN pitch   -> VoiceFrame
        (exists up to here)
  -> chord recognition  : pitch classes of all voices -> chord                   (planned)
  -> key estimation     : running key from chord/pitch history                    (planned)
  -> harmonic tension   : chord in key context -> tension 0..1                    (planned)
  -> haptic mapping     : tension + loudness -> one intensity + pulse -> Bluetooth (planned, last)
```

`Choir` wraps the voice stage: `Choir.Process(interleavedSamples, frames, channels)` fills `Choir.Voices`. Mode follows `voiceCount`: **1 source** = spectral features only (no YIN, no harmony); **2+ sources** = per-voice YIN, optional `IHarmonyAnalyzer` (chord recognition, not implemented yet), plus spectral features of the summed signal (`Choir.Mix`, gain 1/sqrt(N)). Planned stages go in `Dsp/Choir/`. Order and checklist: docs/roadmap.md.

- `unity/Assets/Scripts/Dsp/` (subfolders Core/Voice/Choir, see docs/scripts.md; next steps in docs/roadmap.md): Unity-free (own asmdef, `noEngineReferences`). Single source of truth.
- `unity/Assets/Scripts/Unity/`: `ChoirHost` (owns the pipeline, HUD) and `AudioSourceInput` (feeds an AudioSource, or a mic/VB-Cable device looped through it).
- `dsp/`: builds those files as `netstandard2.1` and tests them with xUnit (27 tests).

## Honest state

- **Never run in Unity.** The scripts were written without an editor and may not compile. The DSP is only verified on synthetic signals.
- Spectral descriptors follow the MathWorks definitions used by the RASTA toolbox (AAU). Its public code (razvysme/Unity3D_Spectral_Analysis) was reviewed and not adopted: no license, static single-instance state, blocks the main thread, unwindowed FFT.
- An earlier roughness (Plomp-Levelt) analyser and a haptic mapper were removed on purpose: too early. They are in git history before the "Trim Dsp to features + pitch" commit.
- **Harmonic tension** (chords in a key context) is the main goal. Needs key estimation plus a model such as key-relative chord distance (Lerdahl) or the spiral array (Chew/Herremans). Design not chosen yet.
- `AudioSourceInput` only sees one AudioSource's channels (Unity may down-mix), so true 4-mic input needs another adapter, e.g. keijiro/Lasp (Unlicense, desktop, per-device/channel input; multi-stream behaviour unverified).
- YIN cost on the audio thread (4 voices x ~86 frames/s) is unmeasured.

## Rules for agents

- Keep `Dsp/` free of `UnityEngine`, C# 9 / netstandard2.1, allocation-free in the hot path.
- Every DSP change gets a test in `dsp/Haptics.Dsp.Tests`. Run `cd dsp && dotnet test Haptics.Dsp.Tests` (the test project uses `RollForward=Major` because only .NET 10 may be installed).
- Don't hand-write Unity `.meta`, scenes or prefabs. Never commit `Library/`, `Temp/`, `Logs/`, `UserSettings/`. Large audio goes through Git LFS.
- Agents here can't run Unity: say what was verified by unit tests and what needs checking in the editor.
- Work on a branch; no force-push or history rewrite on `main`.
