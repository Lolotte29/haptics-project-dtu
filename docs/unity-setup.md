# Unity setup

`unity/` has the scripts but no Unity project files yet. Do this once on the Unity machine.

## 1. Clone

```bash
git lfs install
git clone https://github.com/Lolotte29/haptics-project-dtu
cd haptics-project-dtu
git checkout init/project-scaffold
```

## 2. Create the project files

Unity Hub won't create a project in a non-empty folder, so generate one elsewhere and copy its settings in:

1. Unity Hub > **New project**: Unity 6 LTS or 2022.3 LTS, any 3D template, named `HapticsTmp`, saved outside the repo. Close the editor.
2. Copy `HapticsTmp/ProjectSettings/` and `HapticsTmp/Packages/` into the repo's `unity/` folder. Don't touch `unity/Assets/`.
3. Delete `HapticsTmp`. In Hub: **Add > Add project from disk**, pick `unity/`, open it.

Then set **Edit > Project Settings > Audio > DSP Buffer Size = Best latency**. (Check that Editor settings have *Visible Meta Files* and *Force Text*; they usually do.)

## 3. Scene

1. Empty GameObject `Choir` + component **ChoirHost** (Voice Count 4).
2. Empty GameObject `Input` + **AudioSource** + **AudioSourceInput**. Drag `Choir` into *Host*.

Fix any compile errors in the Console first (the scripts have never been compiled).

## 4. Try it

**Without hardware:** assign a singing clip to the AudioSource (Loop + Play On Awake) and press Play. The top-left display shows each voice's pitch, note and level, plus roughness and haptic intensity.

**With VB-Cable:** on `AudioSourceInput` tick *Use Microphone*. Device names are printed to the Console at start; paste the VB-Cable one (typically `CABLE Output (VB-Audio Virtual Cable)`) into *Microphone Device*. To avoid hearing it twice, create an Audio Mixer, set its group to -80 dB, and use that as the AudioSource output (verify that the analysis still receives audio). A stereo cable gives voices 1 and 2 only; the others show `--`.

## 5. What to check and report back

- Console errors or compile errors.
- Does a sung note show a stable, correct pitch and note name?
- Do two notes a semitone apart show much higher roughness than a fifth? (`ChoirAnalyzer.Scale` can be tuned.)
- Audio-thread CPU in the Profiler (Window > Analysis > Profiler).

Commit the generated `ProjectSettings/`, `Packages/manifest.json`, `packages-lock.json`, scenes and all `.meta` files. `Library/`, `Temp/`, `Logs/`, `UserSettings/` are git-ignored.
