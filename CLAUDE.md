# Project context

Real-time musical feature extraction in Unity, for a haptics project (DTU). Read this before changing anything.

## Goal

Analyse live singing and turn it into features that will eventually drive haptic actuators.

**Concept:** a music-enhancement idea. The singers do **not** wear anything. Selected **audience members** wear haptic bracelets and feel the music (e.g. each voice's rhythm/intensity/pitch movement) as vibration, adding a tactile layer to the live performance. Consequently the mapping layer must decide which singer/feature goes to which wearer, and a performance may have many bracelets (number unknown) fed by only 4 analysed voices.

- **Primary use case:** a 4-person women's choir (soprano/alto-style voices), **one microphone per singer**, so 4 independent mono channels analysed in parallel. Optimise for this first.
- **Versatility matters:** the DSP and input layers must not hard-code "4 singers" or a specific audio device. Channel count, sample rate and source are configuration.
- **Real-time is the constraint:** analysis runs on the audio thread in Unity. No allocations, no locks, no logging, no Unity API calls in the audio callback.

## Hardware / input status

| Stage | Status |
|-------|--------|
| Audio input today | **VB-Audio Virtual Cable** (virtual device) feeding Unity. Used for development with recorded/played-back or routed audio. |
| Audio input planned | Several physical microphones plugged directly into the computer (likely via an audio interface). Expect either multiple devices or one multi-channel device; not decided. |
| Output (planned, **not started**) | An **ESP** (ESP32-class) controlled over **Bluetooth** driving haptic actuators on a **bracelet worn by audience members** (not by the singers). Do not build this yet; just keep the architecture ready for it (see below). |

Open questions (ask the user, don't assume): exact mic/interface model, target latency budget, how many audience members/bracelets and how many actuators per bracelet, BLE vs Bluetooth Classic, one ESP per bracelet vs one ESP broadcasting to several, how voices are assigned to wearers (e.g. each wearer follows one singer, or a mix), and the acceptable end-to-end latency (mic to vibration).

## Architecture

```
input (VB-Cable now / mics later)
   -> per-channel mono stream
   -> FeatureExtractor (one instance per singer)      [pure C#, unity/Assets/Scripts/Dsp]
   -> FeatureFrame per hop (RMS, centroid, flux, onset)
   -> (future) mapping layer: features -> haptic commands
   -> (future) transport: Bluetooth to ESP
```

- `unity/Assets/Scripts/Dsp/`: **Unity-free** DSP (own asmdef, `noEngineReferences`). Single source of truth. Must stay free of `UnityEngine`.
- `unity/Assets/Scripts/Unity/`: thin MonoBehaviours bridging Unity audio to the DSP. Currently `AudioFeatureBehaviour` (one `FeatureExtractor`, reads `OnAudioFilterRead`, downmixes to mono).
- `dsp/`: `Haptics.Dsp` compiles the Unity DSP files as `netstandard2.1`; `Haptics.Dsp.Tests` is xUnit. This is how you verify DSP without Unity.

## Current state (honest)

Implemented: FFT, RMS, spectral centroid, half-wave spectral flux, simple adaptive-threshold onset detector, 6 passing unit tests, single-channel Unity wrapper.

**Not implemented / not tested in Unity:** multi-channel or multi-mic input, device selection, per-singer routing, pitch (F0), tempo/beat, vowel/timbre features, any haptic mapping, any Bluetooth/ESP code. `AudioFeatureBehaviour` has never been run inside Unity from this repo's tooling.

## Likely next steps (suggestions, not commitments)

1. Input abstraction: a source that yields N mono channels with a known sample rate, with implementations for VB-Cable/AudioSource and (later) direct mics. Decide mic strategy: Unity's `Microphone` API is per device and high latency; a multi-channel interface may need a native plugin (ASIO/WASAPI) for low latency.
2. A multi-channel host (e.g. `ChoirFeatureHost`) holding one `FeatureExtractor` per channel, with a thread-safe, allocation-free handoff of the latest `FeatureFrame` per singer to the main thread.
3. Voice-appropriate features: monophonic pitch (YIN/autocorrelation) per singer, voiced/unvoiced, smoothed loudness, onsets. Beware cross-talk (mic bleed) between singers.
4. Offline test material: short recordings of singing in `samples/` (WAV via Git LFS) with expected feature dumps, so DSP can be validated without Unity.
5. Output layer: an `IHapticSink`-style interface so the mapping layer doesn't know about Bluetooth; later an ESP transport. Keep latency and packet rate in mind (BLE has limited throughput).

## Rules for agents

- Keep DSP code in `unity/Assets/Scripts/Dsp/` free of Unity types, compatible with **C# 9 / netstandard2.1** (Unity's limit), and allocation-free in the hot path.
- Every DSP change gets a unit test in `dsp/Haptics.Dsp.Tests`. Run: `cd dsp && dotnet test Haptics.Dsp.Tests` (only .NET 10 runtime may be installed; the test project uses `RollForward=Major`).
- Do not hand-write Unity `.meta` files, scenes or prefabs; Unity generates them. Never commit `Library/`, `Temp/`, `Logs/`, `UserSettings/`.
- Large audio goes through Git LFS (see `.gitattributes`).
- Two machines are used: one runs Unity, one runs Claude/other agents. Agents here cannot run Unity, so state clearly what was verified (unit tests) versus what needs checking in the Unity editor.
- Commit on a branch and push; don't force-push or rewrite history on `main`.
