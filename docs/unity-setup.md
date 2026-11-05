# Unity setup

`unity/` is a complete Unity project (Unity **6000.6.0f1**, URP): settings, packages, scene and `.meta` files are all committed. Nothing to generate.

## 0. What you need

- **Git** (to clone the repository).
- **Unity Hub** and **Unity 6000.6.0f1**. The exact version is in `unity/ProjectSettings/ProjectVersion.txt`; Unity Hub offers to install it when you open the project. Use that version, otherwise Unity may upgrade the project files.
- A **microphone** (the built-in laptop mic or a headset is fine) or an audio file with singing.

## 1. Clone and open

```bash
git clone https://github.com/Lolotte29/haptics-project-dtu
cd haptics-project-dtu
```

Unity Hub > **Add > Add project from disk**, pick the **`unity/`** folder (not the repository root), and open it with Unity 6000.6.0f1. The first import takes a few minutes.

Then set **Edit > Project Settings > Audio > DSP Buffer Size = Best latency**. The committed value is currently the default (1024), which adds delay; "Best latency" is better for live use.

## 2. Scene

Open `Assets/main.unity`. It already has `Choir` (**ChoirHost**, Voice Count 4; the *Pitch Enabled* list switches YIN on or off per source, also in the HUD) and `Input` (**AudioSource** + **AudioSourceInput**, Host assigned, *Use Microphone* ticked). Nothing to wire.

The project compiles and runs. If the Console shows compile errors anyway, check you opened the `unity/` folder with the right Unity version.

## 3. Try it

There are three ways to feed audio in. Pick one:

**Microphone (the simplest, recommended to start):** on `AudioSourceInput` keep *Use Microphone* ticked and leave *Microphone Device* empty, which uses the computer's default microphone. Press Play and sing or talk. The top-left display shows each voice's pitch, note, level, peak and gain trim (with save / reset buttons). A normal mic is one channel, so only voice 1 shows signal; the other voices show `--`. The names of all detected devices are printed to the Console at start, if you want to pick another one (paste its name into *Microphone Device*). Allow microphone access if Windows asks.

**Audio clip (no microphone):** untick *Use Microphone*, assign a singing clip to the AudioSource (Loop + Play On Awake) and press Play.

**VB-Cable (used by the developer):** [VB-Audio Virtual Cable](https://vb-audio.com/Cable/) is a free program that creates a "virtual audio cable": sound played by another program (for example a recording or a DAW) comes out as if it were a microphone. It is handy for testing with a fixed recording and reproducible input. After installing it, tick *Use Microphone* and paste the cable's device name (typically `CABLE Output (VB-Audio Virtual Cable)`) into *Microphone Device*. A stereo cable gives voices 1 and 2 only. You do not need it if you use a real microphone.

See [testing-features.md](testing-features.md) for what to expect from each feature and [concert-manual.md](concert-manual.md) for the sound check.

## 4. What to check and report back

- Console errors or compile errors.
- Does a sung note show a stable, correct pitch and note name?
- Do the voice rows update smoothly, with `--` for channels that carry no signal?
- Do the gain `-`/`+` and *Save gains* buttons work, and are the values still there after stopping and restarting Play?
- Does the display show the right mode and a sensible mix level/centroid (Voice Count 1 vs 4)?
- Audio-thread CPU in the Profiler (Window > Analysis > Profiler).

When you add scripts or scenes in Unity, commit them together with their `.meta` files. `Library/`, `Temp/`, `Logs/`, `UserSettings/` are git-ignored.