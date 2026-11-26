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

## Key and tuning (HUD line "Key ... tuning ...")
- Needs YIN on for at least some voices. It reads "-- (listening)" until about a second of pitched singing has been heard.
- With a tonal piece (e.g. the Dagstuhl quartets) the key should settle within roughly 5-10 s and stay put. A key that flips repeatedly means the piece is ambiguous or the pitch is unstable: look at the voices' note names first.
- "tuning" is the drift of the choir against equal temperament in cents; it should wander slowly (seconds), not jump. A large steady value is fine if the recording is tuned off A=440.
- Relative major/minor (C major / A minor) can be confused when the piece avoids the leading tone. Confidence is then low.
- Only meaningful for Western tonal music.

## Tension (HUD "Tension" bar)
- Rises on chords far from the key (dominant seventh, chromatic or foreign chords) and on clusters of close notes; low on the tonic triad. It follows the notes of the last half second, so it lags a chord change slightly.
- The two parts are shown: "key" (how far the notes are from the tonic triad) and "dissonance" (how crowded the notes are). A single held note has high key distance unless it is the tonic: this is expected.
- Before a key is found (first seconds) only the dissonance part is used.
- Constants are first guesses; if it is always high or always flat on a recording, say so and they will be retuned.

