# Tuning parameters and smoothing: where everything is

Values are the defaults in code unless "scene" is written (the saved `main.unity` overrides them). Nothing here was tuned on real choir audio except what is marked as tried.

## The two signal paths and every time constant in them

**Loudness path (the one that works on the bracelet):**

```
audio -> sum of voices (gain 1/sqrt(N)) -> EnvelopeFollower  [ChoirHost: Envelope Attack 5 ms / Release 10 ms (scene; was 100 in code before)]
      -> bridge: weights (envelope, flux) -> Noise Gate -> Gamma -> Master Gain -> tanh
      -> bridge smoothing  [Attack 15 ms / Release 90 ms]
      -> + onset punch     [decays with 60 ms, bonus 0.35]
      -> x Max Intensity (110) -> byte -> ESP
```

Only one real smoothing stage remains here: the bridge's 15/90 ms, applied after the shaping. The follower in front of it is kept almost transparent (5/10 ms); the old code default of 100 ms release made it a second, redundant smoothing, so the default is now 10 ms.

**Harmony path:**

```
YIN pitch per voice -> pitch smoothing 80 ms -> subtract tuning drift (8 s loop) -> round to a note, hysteresis 0.15 semitone
   -> fast histogram (0.5 s)  -> tension (distance to key's tonic triad + dissonance)   -> "Tension" 0..1
   -> slow histogram (10 s)   -> key (needs 1 s of evidence, switches after 1.5 s)
```

The 0.5 s histogram is the one smoothing of the tension. Before this clean-up it was smoothed again in the bridge (300 ms for the beating, 200/600 ms for the Harmony source): those two layers were removed. The tension now goes into the beating directly, and the Harmony source reuses the bridge's 15/90 ms.

## Parameters by place

### ChoirHost (Inspector)
| Parameter | Default | What it does |
|---|---|---|
| Voice Count | 4 (scene) | Channels / singers |
| Gain Db (per voice, also HUD +/-, saved) | 0 | Trim per channel before everything; **the main knob when the signal is too quiet** |
| Pitch Enabled (per voice, HUD) | on | YIN per voice |
| Pitch Min Hz / Pitch Max Hz (per voice) | 65 / 1100 | YIN search range per voice, applied at Play. Bass about 60-65 (D2 = 73 Hz), tenor 100, alto 150, soprano 200; lower costs CPU and risks octave errors, so raise it for the higher voices |
| Dark Background | on | Dark camera colour at Play so the HUD is readable (the scene file is not changed) |
| Estimate Key | on | Switches the harmony analysis on (key, tension) |
| Envelope Attack / Release Ms | 5 / 10 | Loudness follower |
| Meter Full Scale Rms, Hud Scale | 0.2, auto | HUD display only |

### Inside the DSP (code constants, not in the Inspector)
| Where | Parameter | Default |
|---|---|---|
| PitchTracker | SilenceRms / MinPitchConfidence | 0.01 / 0.7 |
| Yin | search range | per voice (see Pitch Min/Max Hz above); the analysis window is at least 768 samples and at least one period of the minimum |
| FeatureExtractor | FFT 1024, hop 512 (about 21 ms at 48 kHz); onset Sensitivity / MinFlux / RefractoryFrames | 1.5 / 0.02 / 4 |
| HarmonyTracker | PitchSmoothSeconds / HysteresisSemitones / HoldSeconds | 0.08 / 0.15 / 0.15 |
| HarmonyTracker | fast / slow histogram time constants | 0.5 s / 10 s |
| TuningTracker | TauSeconds | 8 s |
| KeyEstimator | MinEvidence / SwitchMargin / SwitchSeconds / FullConfidenceMargin | 1 / 0.05 / 1.5 s / 0.15 |
| HarmonicTension | KeyWeight / FullKeyDistance / DissonanceFloor / FullDissonance / MinEvidence | 0.6 / 1.0 / 0.30 / 0.70 / 0.05 |

### Bridge (`ChoirHapticAdvancedBridge`, Inspector)
| Group | Parameter | Default | What it does |
|---|---|---|---|
| Network | Esp32 IP, Port, Additional Targets | | Bracelets (the IP can change on the hotspot) |
| Network | Send Rate Hz (scene 100), Keep Alive Ms | 50, 100 | Update rate (frame rate also limits it); resend interval |
| Source | Source (buttons) | Envelope | Envelope / Harmony / Env + beat |
| Weights | Envelope Weight, Flux Weight, Onset Bonus | 1, 0.15, 0.35 | What the loudness path adds up |
| Shaping | Noise Gate, Gamma, Master Gain, Max Intensity | 0.03, 0.6, 1.5, 110 | Gate, curve, gain, soft clip, headroom |
| Smoothing | Attack, Release, Onset Hold Ms | 15, 90, 60 | The one smoothing stage |
| Harmony source | Harmony Base, Harmony Gain | 0.25, 1 | Intensity on a calm chord, tension scaling. The silence cut reuses Noise Gate |
| Beating | Beat On Esp, Threshold, Max Depth, Rate Min / Max Hz | off, 0.15, 0.7, 3 / 12 | See below |

### ESP firmware (`tools/beetle_wifi_beating`)
`UPDATE_US` 2000 (500 Hz actuator update), `TIMEOUT_MS` 300 (off if no packet), actuator drive: open-loop 170 Hz (`OL_LRA_PERIOD` 60), `OD_CLAMP` 91 (about 2.0 V). The older `beetle_wifi_unity.ino` has the same drive settings and no timeout.

## Beating: where it is made
- **Beat On Esp off:** the bridge multiplies the level by the wobble at about the frame rate and sends 1 byte. Works with any firmware. Keep the top rate below about a quarter of the frame rate.
- **Beat On Esp on:** the bridge sends 3 bytes (level, depth, rate) and the new firmware makes the wobble at 500 Hz. Rates up to about 60 Hz are possible in principle; whether the actuator can render them is untested (an LRA is resonant, so fast amplitude changes may be weak). The older firmware only reads the first byte and then just plays the level.

## Overlaps that remain (kept on purpose, say if you want them reduced)
- **Gate + Gamma + Master Gain + tanh + Max Intensity** all shape the same value. They work together on the bracelet today, so they are untouched; if you want fewer knobs, Gamma and the tanh are the candidates to merge.
- **Flux in the level and the onset punch** both emphasise transients. Set Flux Weight or Onset Bonus to 0 to hear each alone.
- **Noise Gate sets the loudness threshold for both sources** now; if the music is quiet, raise the per-voice Gain Db first instead of lowering the gate.
