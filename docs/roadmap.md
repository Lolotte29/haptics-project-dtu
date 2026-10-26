# What to implement, in order

Each step is small and leaves the project working. Don't start a step before the previous one is verified.

## 1. Verify what exists (blocking everything else)
- [ ] Open `unity/` in Unity, fix compile errors ([unity-setup.md](unity-setup.md)).
- [ ] Run the HUD with a clip, then VB-Cable. Check pitch/roughness look sane.
- [ ] Measure YIN + FFT cost for 4 voices on the audio thread.

## 2. Haptic output path (so something can be felt)
- [ ] `HapticMapperTests`: silence -> 0, loud -> high, onset -> pulse, attack/release smoothing.
- [ ] A `LogHapticSink` (or on-screen) to prove `ChoirHost.Sink` works.
- [ ] Decide with the user: latency budget, BLE vs Classic, bracelets and actuators, how one stream is broadcast to many.
- [ ] `EspBluetoothSink : IHapticSink` once those are answered.

## 3. Real multi-mic input
- [ ] Decide interface: one multichannel device or several devices.
- [ ] New input adapter (e.g. Lasp) next to `AudioSourceInput`; `AudioSourceInput` may down-mix channels.

## 4. Better features
- [ ] Calibrate `ChoirAnalyzer.Scale` and `HapticMapper` weights on real singing.
- [ ] Harmonic tension: key estimation + key-relative chord distance (Lerdahl) or spiral array (Chew). Design not chosen.

## 5. Later
- [ ] Per-wearer routing, only if "everyone gets the same signal" stops being true.
