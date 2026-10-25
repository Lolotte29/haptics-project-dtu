# Unity setup

The `unity/` folder holds the scripts but not yet a Unity project (no `ProjectSettings/`, `Packages/`). Do this once on the Unity machine.

## 1. Get the repo

```bash
git lfs install
git clone https://github.com/Lolotte29/haptics-project-dtu
cd haptics-project-dtu
git checkout init/project-scaffold   # until it is merged into main
```

## 2. Create the project files

Unity Hub won't create a project inside a folder that already has content, so generate one elsewhere and copy the project files in:

1. Unity Hub > **New project**: Unity 6 LTS or 2022.3 LTS, any 3D template, name `HapticsTmp`, saved *outside* the repo.
2. Close the editor. Copy `HapticsTmp/ProjectSettings/` and `HapticsTmp/Packages/` into the repo's `unity/` folder. Do not overwrite `unity/Assets/`.
3. Delete `HapticsTmp`. In Unity Hub: **Add > Add project from disk** and pick the repo's `unity/` folder, then open it.

(If Hub does open `unity/` directly, you can skip the copy.)

## 3. Project settings

- **Edit > Project Settings > Editor**: Version Control = *Visible Meta Files*, Asset Serialization = *Force Text* (usually the default).
- **Project Settings > Audio**: DSP Buffer Size = *Best latency*.

## 4. Build the scene (2 objects)

1. Create an empty GameObject `Choir`, add **ChoirHost** (Voice Count = 4; the HUD is on by default).
2. Create a GameObject `Input`, add an **AudioSource** (uncheck *Play On Awake* if you use the microphone), then add **AudioSourceInput**. Drag `Choir` into its *Host* field.

## 5. Try it

**A. Without hardware:** give the AudioSource a clip of singing, tick *Loop* and *Play On Awake*, press Play. The HUD (top-left) shows pitch, note name and level for each voice, plus roughness and haptic intensity.

**B. With VB-Cable:**
- On `AudioSourceInput` tick *Use Microphone*. The console lists device names at start; paste the VB-Cable one (typically `CABLE Output (VB-Audio Virtual Cable)`) into *Microphone Device*, or leave it empty for the default input.
- To avoid hearing the audio twice or feedback, create an **Audio Mixer**, set the group's volume to -80 dB, and assign it as the AudioSource's *Output*. (The analysis taps the AudioSource before the mixer; verify this on your Unity version.)
- A stereo cable carries 2 channels, so only voices 1 and 2 will be active, and a mono mic only voice 1. The others show `--`. That is expected; real 4-mic input needs another input adapter (see CLAUDE.md).

## 6. What to check

- Singing a steady note shows a stable pitch and the right note name.
- Two simultaneous notes a semitone apart give clearly higher *Roughness* than a fifth. If the numbers feel too small or too large, tune `ChoirAnalyzer.Scale`.
- Open the Profiler (Window > Analysis > Profiler) and look at the audio thread's CPU, since 4 YIN trackers run there. If it is heavy, tell the agent so the pitch hop can be reduced.
- No errors in the console, especially compile errors in the scripts (they have not been compiled in Unity yet).

## 7. Commit

Commit `unity/ProjectSettings/`, `unity/Packages/manifest.json`, `packages-lock.json`, scenes, and every `.meta` file. `Library/`, `Temp/`, `Logs/` and `UserSettings/` are git-ignored.
