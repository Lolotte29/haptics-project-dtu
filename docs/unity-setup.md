# Unity setup

`unity/` is a complete Unity project (Unity 6000.6, URP): settings, packages, scene and `.meta` files are all committed. Nothing to generate.

## 1. Clone and open

```bash
git lfs install
git clone https://github.com/Lolotte29/haptics-project-dtu
cd haptics-project-dtu
```

Unity Hub > **Add > Add project from disk**, pick the `unity/` folder, open it with the version in `unity/ProjectSettings/ProjectVersion.txt` (Hub offers to install it). The first import takes a few minutes.

Then set **Edit > Project Settings > Audio > DSP Buffer Size = Best latency** (check whether it is already committed).

## 2. Scene

Open `Assets/main.unity`. It already has `Choir` (**ChoirHost**, Voice Count 4; set it to 1 for single-source mode, which skips pitch tracking) and `Input` (**AudioSource** + **AudioSourceInput**, Host assigned, *Use Microphone* ticked). Nothing to wire.

Fix any compile errors in the Console first (the scripts have never been compiled).

## 3. Try it

**Without hardware:** untick *Use Microphone*, assign a singing clip to the AudioSource (Loop + Play On Awake) and press Play. The top-left display shows each voice's pitch, note, level, peak and gain trim (with calibrate / save buttons), and how many voices are detected. See [testing-features.md](testing-features.md) for what to expect and [concert-manual.md](concert-manual.md) for the sound check.

**With VB-Cable:** on `AudioSourceInput` tick *Use Microphone*. Device names are printed to the Console at start; paste the VB-Cable one (typically `CABLE Output (VB-Audio Virtual Cable)`) into *Microphone Device*. To avoid hearing it twice, create an Audio Mixer, set its group to -80 dB, and use that as the AudioSource output (verify that the analysis still receives audio). A stereo cable gives voices 1 and 2 only; the others show `--`.

## 4. What to check and report back

- Console errors or compile errors.
- Does a sung note show a stable, correct pitch and note name?
- Do the voice rows update smoothly, with `--` for channels that carry no signal?
- Do the gain `-`/`+` buttons and *Calibrate levels* work, and are the values still there after stopping and restarting Play?
- Does the display show the right mode and a sensible mix level/centroid (Voice Count 1 vs 4)?
- Audio-thread CPU in the Profiler (Window > Analysis > Profiler).

When you add scripts or scenes in Unity, commit them together with their `.meta` files. `Library/`, `Temp/`, `Logs/`, `UserSettings/` are git-ignored.
