# What to expect when testing the features

Use this while checking the on-screen panel in Unity. The numbers are **expectations from the unit tests and typical audio behaviour, not measurements on your setup**; the point is to compare what you see against the shape of the behaviour. Write down where reality differs: that is what we tune next.

Test with a **single voice first** (other channels muted or at -40 dB), then with all four. Sing steady vowels ("aa", "oo"), then phrases.

## What is computed where

| Feature | Computed on | Shown as |
|---------|-------------|----------|
| Pitch / note (YIN), level, onset | each voice separately | `V1 C4 261.6 Hz` and the level bar |
| Loudness, centroid, spread, skewness, entropy, ZCR, flux | the **sum** of all voices | `Sum:` lines |
| Roughness | per-voice pitches combined | `roughness` |
| Haptic intensity / pulse | from the above | slider at the bottom |

## Per-voice checks

| What | Do this | Expect |
|------|---------|--------|
| **Pitch** | Hold a steady note | Stable note name and Hz within about 1%. Brief octave jumps or `--` at note starts, vibrato edges and breaths are normal. Constant jumping means the level is too low or the room too noisy. |
| **Silence** | Stop singing | Pitch `--` and level near 0 within a fraction of a second. |
| **Level bar** | Soft, normal, loud | Bar grows with loudness. After calibration, normal singing reaches about half of the bar. |
| **Peak / CLIP** | Sing as loud as possible | Peak below 1.0. `CLIP` means lower the hardware gain. |
| **Gain** | Press `-` / `+` | Level changes by that many dB (6 dB = half or double). Peak value does not change (it is measured before the gain). |

## Sum checks (standard features)

| Feature | What it means | Expect |
|---------|---------------|--------|
| **loudness** | RMS of the sum | Rises when more singers join. Four equal voices give about twice one voice (uncorrelated signals add in power), not four times. |
| **centroid** (Hz) | Brightness | Low for dark "oo", higher for bright "ee", very high for "s"/"sh". Adding a high-pitched voice raises it. Typical sung vowels: a few hundred to a few thousand Hz. |
| **spread** (Hz) | How wide the spectrum is | Grows when voices are far apart in pitch or the sound is noisy. |
| **skew** | Asymmetry around the centroid | Positive: energy tail toward high frequencies (typical for voice). Mostly useful relative to itself. |
| **entropy** (0..1) | Noisiness | Lower for clear tonal singing, higher for breathy sound and consonants. |
| **ZCR** (0..1) | Zero-crossing rate | Very low for low steady vowels (around 0.01-0.05), jumps for "s"/"sh" and breathiness. |
| **flux** | Change of spectrum | Spikes at note starts and consonants, near 0 on a held chord. |

## Onset and pulse

- A pulse should fire when **any** singer starts a note, including when the others are holding a chord (this is tested with synthetic tones).
- Expect misses on very soft or legato entries (a slurred note change may not count as a new onset) and a few extra pulses on consonants like "t" or "s". If pulses fire all the time, raise `FeatureExtractor.Sensitivity` or `MinFlux`; if they are missed, lower them.

## Roughness

- Two singers on a unison, octave or fifth: low. A semitone or whole tone apart: clearly higher. In the unit tests a tight 4-note cluster scores above 0.3 and a major chord well below that.
- Real voices (vibrato, slightly off-pitch) will not hit those exact values. The `ChoirAnalyzer.Scale` gain scales roughness to 0..1: if it is always close to 0, increase it; if pinned at 1, decrease it.
- Roughness needs at least 2 voices with a detected pitch.

## Haptic intensity

- Silence gives 0. It rises with loudness (and a bit more with roughness) and falls more slowly than it rises (attack faster than release).
- If the intensity is always saturated or very low, change `Loudness Gain` on ChoirHost.

## When something looks wrong

| Symptom | Likely cause |
|---------|--------------|
| A voice stays `--` while singing | Level too low (raise gain), or that channel receives silence (wrong input channel). |
| Two voices show the same note | Microphone bleed: one mic hears the neighbour. Lower the quieter mic's gain or move the mics. |
| Pitch always an octave off | Voice outside the YIN search range (100-1100 Hz by default). |
| Only voice 1 (and 2) respond | The input adapter only delivers 1-2 channels; see [mic-input.md](mic-input.md). |
| Loudness is dominated by one singer | Calibrate levels again. |
