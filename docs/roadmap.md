# What to implement, piece by piece

Do one step at a time. Each step is one small script plus a test in `dsp/Haptics.Dsp.Tests`, and leaves the project working. Script names are in [scripts.md](scripts.md).

## 0. Done
- [x] Spectral features (RMS, centroid, spread, skewness, entropy, ZCR, flux, onset), YIN pitch, per-voice and choir wiring.
- [x] Single-source mode (features only) and multi-source mode (YIN per voice + features of the summed mix), `IHarmonyAnalyzer` plug-in point.

## 1. Verify what exists (do first)
- [ ] Open `unity/` in Unity, fix compile errors ([unity-setup.md](unity-setup.md)).
- [ ] Sing into it (clip, then VB-Cable): are pitch and note names stable and right?
- [ ] Measure YIN + FFT cost for 4 voices on the audio thread.

## 2. Pitch -> notes
- [ ] `PitchClass`: Hz -> MIDI note, pitch class 0..11, cents off. Pure maths, easy test. Reuse it in `ChoirHost` instead of its private `NoteName`.

## 3. Chord recognition
- [ ] `ChordRecognizer`: from the voiced pitch classes, find root + quality (major, minor, dim, aug, sus, 7th). Implement `IHarmonyAnalyzer` and assign it to `Choir.HarmonyAnalyzer`. Start with template matching. Decide: what to output with 2 voices or noisy pitches.

## 4. Key estimation
- [ ] `KeyEstimator`: accumulate pitch classes over a few seconds (decaying histogram), correlate with Krumhansl-Schmuckler major/minor profiles -> tonic + mode + confidence.

## 5. Harmonic tension
- [ ] `HarmonicTension`: chord in key -> 0..1. Pick a model first (Lerdahl key-relative chord distance or spiral array, Chew/Herremans), note the choice in CLAUDE.md. Then extend `HarmonyFrame` with key and tension (already exposed as `Choir.Harmony`).
- [ ] Show chord, key and tension in the `ChoirHost` display.

## 6. Later, after tension works
- [ ] Haptic mapping (tension + loudness -> intensity + onset pulse), smoothing.
- [ ] Real multi-mic input (one multichannel device or several; e.g. Lasp), since `AudioSourceInput` may down-mix.
- [ ] Bluetooth/ESP sink. Open questions: latency budget, BLE vs Classic, bracelets/actuators, one stream to many wearers.
