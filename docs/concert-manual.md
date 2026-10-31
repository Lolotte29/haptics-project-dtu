# Scene and concert manual

Procedure for setting up and running the system live. Draft: written without hardware, to be revised after the first rehearsal.

## Purpose of the gain settings

The haptic features are computed from the **sum of the four voices**, so the level of each microphone determines how strongly that singer contributes. A channel that is too loud dominates the level and spectrum of the sum. The per-channel pitch (YIN) also requires a suitable level on each channel. The channels must therefore be level-matched before the performance.

Two gain stages are involved:

| Stage | Where | Purpose | Cannot be replaced by |
|-------|-------|---------|-----------------------|
| Hardware gain | Preamp / audio interface | Sets the input headroom: loud enough to be well above noise, low enough not to clip. | The in-app gain: once the input has clipped, the signal is distorted and software cannot restore it. |
| In-app gain (calibration and `-`/`+`) | `ChoirHost` | Matches the levels of the four channels to each other, so each singer contributes equally. | The hardware gain: it adjusts only after the input, so it cannot fix clipping or poor signal-to-noise. |

If the hardware gains are already set so that the singers are at similar levels, the calibration will only make small corrections, but it remains the means of verifying and saving the final values.

## Configuration

1. Open the Unity scene (`Choir` with **ChoirHost**, input object with the input component). Confirm that *Voice Count* matches the number of singers.
2. Project Settings > Audio > DSP Buffer Size = *Best latency*.
3. Select the input device (see [mic-input.md](mic-input.md) for the open questions).

## Sound check (about 5 minutes)

1. Start the scene. The panel at the top left shows one row per voice: note, pitch, level bar, peak value and gain.
2. **Hardware gain.** Each singer sings, one at a time, at the loudest level expected in the performance. Adjust the preamp/interface gain until the peak value reads about 0.5-0.8. If `CLIP` appears (red bar), reduce the hardware gain.
3. **Calibration.** Press **Calibrate levels**. All singers sing together at performance volume for 10-15 seconds. Press **Finish calibration**. Each channel's average level is set to the target (`Calibration Target Rms`, default 0.1) and the gains are saved.
4. **Fine adjustment.** `-` and `+` change a channel by 1 dB. The level bars should be about equal for normal singing. **Save gains** stores the values on that computer (PlayerPrefs). **Reset gains** returns all channels to 0 dB.
5. **Silence check.** With all singers quiet, the level bars and the mix level should be near zero. A channel that shows a level in silence is picking up the room or neighbouring singers: reposition the microphone or lower its hardware gain.

## During the performance

- Gains are not changed while singing; adjust only between pieces.
- Monitor each channel for `CLIP`. Also monitor the pitch field of each voice row (see below): `--` while that singer is singing indicates a level that is too low, or excessive noise.
- If a microphone fails or is very noisy, press `-` on that channel until it reads about -40 dB. It then no longer affects the sum.

## Reading the voice rows

Each row of the panel at the top left has the form `V1 C4  261.6 Hz`: voice number, note name, pitch in Hz. When no pitch is detected, the note shows `--` and the pitch reads `0.0 Hz` (for example `V2 --    0.0 Hz`). This is the normal state when a singer is silent, and the unusual one only when that singer is singing.

## After the performance

- The gains remain saved on the machine. Record the final values and the interface settings in the rehearsal log so that the setup can be reproduced.
