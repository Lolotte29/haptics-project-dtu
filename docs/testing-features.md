# What to expect when testing the features

Use this while checking the on-screen panel in Unity. The numbers are **expectations from the unit tests and typical audio behaviour, not measurements on your setup**; the point is to compare what you see against the shape of the behaviour. Write down where reality differs: that is what we tune next.

Test with a **single voice first** (other channels muted or at -40 dB), then with all four. Sing steady vowels ("aa", "oo"), then phrases.

## What is computed where

| Feature | Computed on | Shown as |
|---------|-------------|----------|
| Pitch / note (YIN) | each voice separately | `V1 C4 261.6 Hz` |
| Channel level, peak | each channel (for gain setting only) | level bar, peak value |
| Level, centroid, spread, skewness, entropy, ZCR, flux, onset | the **sum** of all voices (scaled by 1/sqrt(N)) | `Mix:` line, and the plots in `FeatureVisualizer` if added |

## Per-voice checks

| What | Do this | Expect |
|------|---------|--------|
| **Pitch** | Hold a steady note | Stable note name and Hz within about 1%. Brief octave jumps or `--` at note starts, vibrato edges and breaths are normal. Constant jumping means the level is too low or the room too noisy. |
| **Silence** | Stop singing | Pitch `--` and level near 0 within a fraction of a second. |
| **Level bar** | Soft, normal, loud | Bar grows with loudness. With the gains matched, normal singing reaches about half of the bar. |
| **Peak / CLIP** | Sing as loud as possible | Peak below 1.0. `CLIP` means lower the hardware gain. |
| **Gain** | Press `-` / `+` | Level changes by that many dB (6 dB = half or double). Peak value does not change (it is measured before the gain). |

## Sum checks (standard features)

| Feature | What it means | Expect |
|---------|---------------|--------|
| **level** | RMS of the sum | Rises with how loud the choir sings. The sum is scaled by 1/sqrt(N), so four equal, uncorrelated voices read about the same as one voice alone. |
| **centroid** (Hz) | Brightness | Low for dark "oo", higher for bright "ee", very high for "s"/"sh". Adding a high-pitched voice raises it. Typical sung vowels: a few hundred to a few thousand Hz. |
| **spread** (Hz) | How wide the spectrum is | Grows when voices are far apart in pitch or the sound is noisy. |
| **skew** | Asymmetry around the centroid | Positive: energy tail toward high frequencies (typical for voice). Mostly useful relative to itself. |
| **entropy** (0..1) | Noisiness | Lower for clear tonal singing, higher for breathy sound and consonants. |
| **ZCR** (0..1) | Zero-crossing rate | Very low for low steady vowels (around 0.01-0.05), jumps for "s"/"sh" and breathiness. |
| **flux** | Change of spectrum | Spikes at note starts and consonants, near 0 on a held chord. |

## Onset

- Onsets are detected on the **sum** (flux above a moving average), not per singer. A new voice entering over a sustained chord is only caught if it changes the spectrum enough.
- Expect misses on very soft or legato entries and a few extra onsets on consonants like "t" or "s". If onsets fire all the time, raise `FeatureExtractor.Sensitivity` or `MinFlux`; if they are missed, lower them.

## When something looks wrong

| Symptom | Likely cause |
|---------|--------------|
| A voice stays `--` while singing | Level too low (raise gain), or that channel receives silence (wrong input channel). |
| Two voices show the same note | Microphone bleed: one mic hears the neighbour. Lower the quieter mic's gain or move the mics. |
| Pitch always an octave off | Voice outside the YIN search range (100-1100 Hz by default). |
| Only voice 1 (and 2) respond | The input adapter only delivers 1-2 channels; see [mic-input.md](mic-input.md). |
| The mix level is dominated by one singer | Lower that channel's gain with `-`. |
