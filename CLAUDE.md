# Project context

Real-time musical feature extraction in Unity for a haptics project (DTU). Read this before changing anything.

## Goal

Analyse live singing and turn it into features that will drive haptic actuators.

- **Concept:** a music-enhancement idea. Singers wear nothing. Selected **audience members** wear haptic bracelets (ESP, Bluetooth) and feel the music as vibration. Very likely **every wearer gets the same signal**: one broadcast stream derived from all voices, no per-wearer routing.
- **Primary use case:** a 4-person women's choir, **one mic per singer** (4 mono channels). Don't hard-code "4" or a device: channel count and source are configuration.
- **Real-time:** analysis runs on the audio thread. No allocations, locks, logging or Unity API calls there.

## Status of the hardware

- Input today: **VB-Audio Virtual Cable** (stereo). Later: several mics straight into the computer, either one multichannel interface or several devices (undecided).
- Output: ESP32 bracelets over **Wi-Fi/UDP** (replaces Bluetooth). `ChoirHapticAdvancedBridge` (`ChoirHostBridge.cs`) sends one intensity byte to one or several IPs; the source is switchable between *Envelope* (envelope, flux, onsets), *Harmony* (harmonic tension) and *EnvelopeBeat* (envelope with a tension-driven beating on top); the envelope path works with the ESP (per the author), the other two are untested on the bracelet. The ESP firmware is `tools/beetle_wifi_unity.ino` (two LRAs, amplitude only) or the newer `tools/beetle_wifi_beating/` (3-byte packet: it makes the beating itself, has a safety timeout, Wi-Fi credentials in an ignored `secrets.h`; untested on hardware). Every tunable and time constant is listed in `docs/tuning-parameters.md`. See `docs/esp-wifi.md`.

Decided so far: 4 mics plugged into one computer (how is open, see `docs/mic-input.md`); latency "as good as possible" (no number); probably fewer than 5 bracelets with up to 4 actuators each; Bluetooth is a later problem.

Open questions (ask the user, don't assume): mic/interface model, latency budget (mic to vibration), number of bracelets and actuators, BLE vs Classic and how one stream is broadcast to many bracelets, how 4 voices combine into one stream.

## Architecture

```
audio in (channel i = singer i)
  -> per-channel gain trim, peak/level meters (Choir.SetGainDb; adjusted by hand in the HUD before the concert)
  -> PitchTracker x N   : YIN pitch per singer, nothing else                    -> VoiceFrame (feeds harmony)
  -> sum of channels    : level + spectral features (FeatureExtractor), once    -> Choir.Mix
        (exists up to here; YIN can be switched on/off per source, with any source count)
  -> chord recognition  : pitch classes of all voices -> chord                   (planned)
  -> pitch classes + key: HarmonyTracker (pitch smoothing, tuning drift, 12-bin histograms, Temperley key profiles)  (exists, synthetic tests only)
  -> harmonic tension   : HarmonicTension, fast histogram in key context -> 0..1   (exists, untuned)
  -> haptic mapping     : tension + loudness -> one intensity + pulse -> Bluetooth (planned, last)
```

`Choir` wraps the voice stage: `Choir.Process(interleavedSamples, frames, channels)` fills `Choir.Voices`. There is no mode: YIN is switchable **per source** (`Choir.SetPitchEnabled(i, bool)`, default on, also for a single source; HUD toggle and `ChoirHost.pitchEnabled`). Sources with YIN off cost no YIN time and stay unvoiced in `Choir.Voices` (pitch is the only per-singer analysis), so the optional `IHarmonyAnalyzer` (`HarmonyTracker` is the implementation, switched on by `ChoirHost.estimateKey`; chord recognition is not implemented yet) only ever sees the sources the user chose. Spectral features of the summed signal (`Choir.Mix`, gain 1/sqrt(N); one source = that channel) always run. Spectral features are deliberately never computed per singer. Planned stages go in `Dsp/Choir/`. Order and checklist: docs/roadmap.md.

- `unity/Assets/Scripts/Dsp/` (subfolders Core/Voice/Choir, see docs/scripts.md; next steps in docs/roadmap.md): Unity-free (own asmdef, `noEngineReferences`). Single source of truth.
- `unity/Assets/Scripts/Unity/`: `ChoirHost` (owns the pipeline, HUD), `AudioSourceInput` (feeds an AudioSource clip, or reads a mic/VB-Cable capture device directly) and `MultiClipInput` (one mono clip per singer, e.g. Dagstuhl ChoirSet stems, played and analysed on the audio clock; unverified in the editor). A live multi-mic adapter would be a sibling calling `ChoirHost.Push`.
- `docs/`: `esp-wifi.md` (Wi-Fi link to the bracelets; Wi-Fi replaces Bluetooth), `unity-setup.md`, `mic-input.md` (input options for the supervisor), `concert-manual.md` (sound check and gains), `testing-features.md` (what to expect per feature).
- `dsp/`: builds those files as `netstandard2.1` and tests them with xUnit (101 tests).

## Honest state

- **Runs in Unity** and compiles. YIN tracked a simple sung scale well; it has not been tried on dense or real choir material. The rest of the DSP is verified on synthetic signals only.
- **Focus now:** a demo on the Dagstuhl quartets (no live choir): key and tension are implemented and shown in the HUD, but nothing sends tension to the bracelets yet (the senders on this branch and on origin/main send envelope-based values only). Chord recognition is still not started: don't start it unless the user asks.
- The per-channel gain HUD (IMGUI) is covered by unit tests of `Choir` only; it has not been looked at in the editor.
- Spectral descriptors follow the MathWorks definitions used by the RASTA toolbox (AAU). Its public code (razvysme/Unity3D_Spectral_Analysis) was reviewed twice and still not adopted. Licence: none in the repo; the user knows the author and considers it fine (get that in writing or a LICENSE file before relying on it). Static single-instance state would be acceptable if it only ran on the summed mix or a single source. Still blocking: `FixedUpdate` waits on `Task.Result` (main-thread stall), the audio thread contends on a lock while 16k samples are copied, the FFT is unwindowed and 16384 samples long (~340 ms, too laggy for haptics), the default 200 Hz low-pass distorts voice descriptors, centroid is smoothed over 1000 updates, "tilt" is really skewness, ZCR is in Hz, the bin range stops at fs/4, and it needs MathNet.Numerics (NuGet) and a MonoBehaviour, so it cannot live in the Unity-free `Dsp/` or be unit-tested. It is usable only as an offline cross-check of our numbers.
- An earlier roughness (Plomp-Levelt) analyser and a haptic mapper were removed on purpose: too early. They are in git history before the "Trim Dsp to features + pitch" commit.
- **Key estimation exists** (`HarmonyTracker`: fast and slow pitch-class histograms, tuning-drift tracker, Temperley profiles with hysteresis); verified on synthetic progressions only, not yet on the DCS recordings. **Tension exists** (`HarmonicTension`, Tonal Interval Space, no chord labels: 0.6 x distance to the key's tonic triad + 0.4 x dissonance; constants are first guesses, synthetic tests only, not yet tuned on real recordings).
- Alternatives to the current tension model, if it proves weak: key-relative chord distance (Lerdahl) or the spiral array (Chew/Herremans); both need chord labels or extra geometry.
- `AudioSourceInput` only sees one AudioSource's channels (Unity may down-mix), so true 4-mic input needs another adapter, e.g. keijiro/Lasp (Unlicense, desktop, per-device/channel input; multi-stream behaviour unverified).
- **Test material and scope for harmony:** the demo uses the Dagstuhl ChoirSet (DCS) quartets (one synchronised mono file per singer, played through `MultiClipInput`); YIN was judged good on them by the user in the editor. The real choir will be classical too. Key and tension models are validated only for Western tonal music, so results on anything else are not meaningful. All harmony code is tested on synthetic progressions; behaviour on DCS needs checking in the HUD.
- YIN cost on the audio thread (4 voices x ~86 frames/s) is unmeasured.

## Rules for agents

- Keep `Dsp/` free of `UnityEngine`, C# 9 / netstandard2.1, allocation-free in the hot path.
- Every DSP change gets a test in `dsp/Haptics.Dsp.Tests`. Run `cd dsp && dotnet test Haptics.Dsp.Tests` (the test project uses `RollForward=Major` because only .NET 10 may be installed).
- Don't hand-write Unity `.meta`, scenes or prefabs. Never commit `Library/`, `Temp/`, `Logs/`, `UserSettings/`. Large audio goes through Git LFS.
- Agents here can't run Unity: say what was verified by unit tests and what needs checking in the editor.
- Work on a branch; no force-push or history rewrite on `main`.
