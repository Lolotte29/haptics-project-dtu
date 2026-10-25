# haptics-project-dtu

Real-time musical feature extraction for a Unity haptics project. Primary use case: a 4-singer choir, one microphone each (VB-Cable for now, direct mics later), with a future Bluetooth/ESP haptic bracelet worn by audience members (not singers) to enhance the music. Agents and contributors: see [CLAUDE.md](CLAUDE.md) for context and rules.

## Layout

| Path | Purpose |
|------|---------|
| `unity/` | Unity project root (open this folder in Unity Hub). |
| `unity/Assets/Scripts/Dsp/` | Unity-free C# DSP code (FFT, RMS, spectral centroid/flux, onset detection). Own asmdef, `noEngineReferences`. **Single source of truth.** |
| `unity/Assets/Scripts/Unity/` | Thin MonoBehaviours that call the DSP code (e.g. `AudioFeatureBehaviour`). |
| `dsp/Haptics.Dsp/` | Plain `netstandard2.1` project that compiles the files above, so they can be built and tested without Unity. |
| `dsp/Haptics.Dsp.Tests/` | xUnit tests for the DSP code. |
| `samples/` | Short test audio and recorded feature dumps (WAV via Git LFS). |
| `tools/` | Helper scripts (offline analysis, plotting, etc.). |
| `docs/` | Notes and design docs. |

## Workflow

- Real-time processing runs **inside Unity** on the audio thread (`OnAudioFilterRead`). Latency-sensitive work never leaves the Unity machine.
- DSP changes are developed and tested on any machine with the .NET SDK:

  ```bash
  cd dsp && dotnet test Haptics.Dsp.Tests
  ```
- Then push, and pull on the Unity machine.

## First-time setup on the Unity machine

1. `git lfs install` (once), then clone the repo.
2. Create the Unity project **inside `unity/`** (Unity Hub > New project > location `.../haptics-project-dtu/unity`, or open the folder if Unity is already initialised). Commit the generated `ProjectSettings/`, `Packages/manifest.json` and all `.meta` files. Keep *Version Control: Visible Meta Files* and *Asset Serialization: Force Text* enabled.
3. Add `AudioFeatureBehaviour` to a GameObject that has an `AudioSource`.

## Features currently extracted

RMS, spectral centroid, half-wave-rectified spectral flux, onset flag.
