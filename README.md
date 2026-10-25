# haptics-project-dtu

Real-time musical feature extraction in Unity for a haptics project. Target setup: a 4-singer choir, one microphone each (VB-Cable for now). Later, a Bluetooth/ESP haptic bracelet worn by audience members will play one shared signal derived from the singers. Agents: read [CLAUDE.md](CLAUDE.md).

## Layout

| Path | Purpose |
|------|---------|
| `unity/Assets/Scripts/Dsp/` | Unity-free C# analysis code (the part to test and extend). |
| `unity/Assets/Scripts/Unity/` | Two small MonoBehaviours: `ChoirHost` (pipeline + on-screen display) and `AudioSourceInput`. |
| `dsp/` | Plain .NET project + xUnit tests that compile the DSP files without Unity: `cd dsp && dotnet test Haptics.Dsp.Tests`. |
| `docs/unity-setup.md` | How to open the project in Unity and try it. |

## What it computes

- **Per singer:** level (RMS), pitch (YIN), spectral centroid / spread / skewness / entropy / flux, zero-crossing rate, onsets.
- **Ensemble:** roughness between voices (sensory dissonance) and loudness. Harmonic (key-aware) tension is not implemented yet.
- **Output:** one haptic stream (intensity + pulse) behind an `IHapticSink` interface. Nothing is sent over Bluetooth yet.
