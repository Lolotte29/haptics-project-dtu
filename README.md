# haptics-project-dtu

Real-time musical feature extraction in Unity for a haptics project. Target setup: a 4-singer choir, one microphone each (VB-Cable for now). Later, a Bluetooth/ESP haptic bracelet worn by audience members will play one shared signal derived from the singers. Agents: read [CLAUDE.md](CLAUDE.md).

## Layout

| Path | Purpose |
|------|---------|
| `unity/Assets/Scripts/Dsp/` | Unity-free C# analysis code, in `Core/`, `Voice/`, `Choir/` (the part to test and extend). |
| `unity/Assets/Scripts/Unity/` | Two small MonoBehaviours: `ChoirHost` (pipeline + on-screen display) and `AudioSourceInput`. |
| `dsp/` | Plain .NET project + xUnit tests that compile the DSP files without Unity: `cd dsp && dotnet test Haptics.Dsp.Tests`. |
| `docs/unity-setup.md` | How to open the project in Unity and try it. |
| `docs/scripts.md` | Map of every script, grouped by pipeline stage, with status. |
| `docs/roadmap.md` | What to implement next, in order. |
| `docs/mic-input.md` | Microphone connection options and open questions (for the supervisor). |
| `docs/concert-manual.md` | Sound check: gains, what to watch live. |
| `docs/testing-features.md` | What each displayed feature should do when testing. |

## What it computes

- **Per singer** (2+ sources): pitch only (YIN), to feed harmony analysis. Each channel also has a gain trim, a level meter and a peak/clip meter.
- **Summed signal** (`Choir.Mix`): level (RMS), spectral centroid / spread / skewness / entropy / flux, zero-crossing rate, onsets, computed once on the sum of all channels, or on the single source when there is only one (no pitch tracking in that case).
- **Next, piece by piece** ([roadmap](docs/roadmap.md)): chord recognition, key estimation, harmonic tension, then mapping to haptics. None of these exist yet.
