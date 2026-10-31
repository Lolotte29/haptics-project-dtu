# Scene and concert manual

Checklist for running the system live. Draft: written without hardware, so adjust it after the first rehearsal.

## What the gains are for

The haptic features come from the **sum of the four voices**, so the mic levels decide who is felt. If one channel is too loud, that singer dominates the level and spectrum. Per-channel pitch (YIN) also depends on each channel having a sensible level. Therefore: even the channels out before the concert.

## Before the day

1. Open the Unity scene (`Choir` with **ChoirHost**, input object with the input component). Confirm *Voice Count* matches the number of singers.
2. Project Settings > Audio > DSP Buffer Size = *Best latency*.
3. Pick the input device (see [mic-input.md](mic-input.md) for the open questions).

## Sound check (about 5 minutes)

1. Start the scene. The top-left panel shows one row per voice: note, pitch, level bar, peak value, gain.
2. **Hardware gain first.** Ask each singer, one at a time, to sing at their loudest. Turn the preamp/interface gain until the peak value reads around 0.5-0.8. If `CLIP` appears (red bar), lower the hardware gain. The in-app gain cannot repair a clipped input.
3. **Calibrate levels.** Press **Calibrate levels**, ask everyone to sing together at concert volume for 10-15 seconds, then press **Finish calibration**. Each channel's average level is set to the target (`Calibration Target Rms`, default 0.1) and the gains are saved.
4. **Fine-tune by hand.** `-` and `+` change a channel by 1 dB. Make the level bars about the same length for normal singing. **Save gains** stores them (PlayerPrefs on that computer). **Reset gains** returns to 0 dB.
5. **Silence check.** With everyone quiet, level bars and the mix level should be near zero. If a channel shows level in silence, that mic picks up the room or neighbours: move it or lower its hardware gain.

## During the concert

- Do not change gains while singing; trim only between songs.
- Watch for `CLIP` on any channel and for the pitch showing `--` for a singing voice (level too low, or too much noise).
- If a mic fails or is very noisy, press `-` on that channel until it reads about -40 dB. It then no longer affects the sum.

## After

- Gains stay saved on the machine. Note the final values and the interface settings in the rehearsal log so the setup can be repeated.
