# What to implement, piece by piece

Do one step at a time. Each step is one small script plus a test in `dsp/Haptics.Dsp.Tests`, and leaves the project working. Script names are in [scripts.md](scripts.md).

## 0. Done
- [x] Spectral features (RMS, centroid, spread, skewness, entropy, ZCR, flux, onset), YIN pitch per voice, choir wiring (features on the mix only).
- [x] YIN switchable per source (any source count) + features of the summed mix, `IHarmonyAnalyzer` plug-in point (it only sees sources with YIN on).

## 1. Verify what exists (do first)
- [ ] Open `unity/` in Unity, fix compile errors ([unity-setup.md](unity-setup.md)).
- [ ] Sing into it (clip, then VB-Cable): are pitch and note names stable and right?
- [ ] Measure YIN + FFT cost for 4 voices on the audio thread.

## 2. Pitch -> notes
- [x] `PitchClass`: Hz -> MIDI note, pitch class 0..11, cents off (tested). `ChoirHost` still has its own private `NoteName`; switch it over when convenient.
- [x] `PitchClassHistogram` (fast / slow), `TuningTracker` (drift), `HarmonyTracker` (pitch smoothing, hysteresis, per-voice state). Synthetic tests only.

Test material for sections 2-5: Dagstuhl ChoirSet quartets via `MultiClipInput`. Scope: Western tonal (classical) music only, which is also what key estimation and the tension models are validated for.

## 3. Chord recognition
- [ ] `ChordRecognizer`: from the voiced pitch classes, find root + quality (major, minor, dim, aug, sus, 7th). Implement `IHarmonyAnalyzer` and assign it to `Choir.HarmonyAnalyzer`. Start with template matching. Decide: what to output with 2 voices or noisy pitches.

## 4. Key estimation
- [x] `KeyEstimator`: slow histogram correlated with the 24 Temperley key profiles -> tonic + mode + confidence, with switch hysteresis. Shown in the HUD. Synthetic tests only.
- [ ] Check the key and tuning readout on the Dagstuhl quartets in the editor; tune `SwitchSeconds`, histogram time constants and `MinEvidence` if it is slow or flips.

## 5. Harmonic tension
- [ ] `HarmonicTension`: chord in key -> 0..1. Pick a model first (Lerdahl key-relative chord distance or spiral array, Chew/Herremans), note the choice in CLAUDE.md. Then extend `HarmonyFrame` with key and tension (already exposed as `Choir.Harmony`).
- [ ] Show tension in the `ChoirHost` display (key and tuning are already there).

## 6. Later, after tension works
- [ ] Haptic mapping (tension + loudness -> intensity + onset pulse), smoothing.
- [ ] Real multi-mic input (one multichannel device or several; e.g. Lasp), since `AudioSourceInput` may down-mix. Stand-in for demos and testing: `MultiClipInput` (per-singer mono files), written but not yet tried in the editor.
- [ ] Bluetooth/ESP sink. Open questions: latency budget, BLE vs Classic, bracelets/actuators, one stream to many wearers.
