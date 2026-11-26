# ESP Wi-Fi link (for the ESP side)

Unity turns the live features of the choir (amplitude envelope, spectral flux, onsets) into **one intensity byte** and sends it over **UDP** to every bracelet. Every ESP receives the same bytes. There is no reply and no connection: just listen on a port and use the newest value.

The component in use is `ChoirHapticAdvancedBridge` (file `ChoirHostBridge.cs`). The older `UdpHapticSender` (5-byte packet with a sequence number, own send thread) is still in the project but is **not** used in the scene; see the end of this file.

## What the ESP must do

1. Join the Wi-Fi network the Unity computer is on.
2. Get each ESP's IP address and write them in the Unity interface (several are allowed).
3. Listen on a UDP port (**4210** by default).
4. For each packet: if it is exactly 1 byte, use it as the vibration strength (0 = off, up to the configured maximum, 110 by default; 127 is the largest value the bridge can send).
5. **Safety timeout:** if no packet arrives for about 300 ms, set the vibration to 0 (a suggestion; Unity resends the value at least every 100 ms, see below, so a healthy link never triggers it).

Tips for low latency: call `WiFi.setSleep(false)` (the default power save adds 100 ms or more), never block the loop (poll `parsePacket()`), and always use the newest packet only.

## Packet format

**1 byte**, unsigned: the intensity. No marker and no sequence number, so the ESP cannot tell a stray UDP packet from ours or detect a lost one; keep the bracelets on a private network.

Optional **3-byte packet** (`BeatPacket.cs`), sent only when the source is *Env + beat* and **Beat On Esp** is on: byte 0 = intensity (same as above), byte 1 = beat depth 0..255 (= 0..1), byte 2 = beat rate in 0.5 Hz steps. The firmware `tools/beetle_wifi_beating` makes the amplitude wobble itself (raised cosine, peaks keep the level, dips go down by the depth) and updates the actuators at 500 Hz. A 1-byte packet means no beating. Firmware that only reads the first byte (the older `beetle_wifi_unity.ino`) still works and plays the level without the beating.

Timing:
- Unity computes a new value at `Send Rate Hz` (default 50) and sends it **when it changed**, and otherwise **at least every `Keep Alive Ms`** (default 100). A lost packet is therefore repaired within 100 ms, and the stream never goes silent while Unity runs.
- Silence is sent as `0`, also repeated every keep-alive interval.
- The value is already smoothed in Unity (attack 15 ms, release 90 ms by default); the ESP does not need to filter it.

## Network setup

- Could be nice to have our own router for the festival, also we could have a fixed IP address for each ESP if we always work on the same network (I think)
- The Unity computer and all ESPs must be on the same network.
- Unity sends to each ESP's IP and port directly (no broadcast), through one socket, so one unreachable bracelet does not disturb the others.

## Checklist when nothing arrives

- Console says `no valid target, not sending`: the first IP field is empty or wrong and the list has no valid entry.
- Console says `UDP error: <ip>:<port>: ...`: the computer has no network or that IP is unreachable. Each distinct error is logged once, and the other bracelets keep working.
- Console says `ignoring invalid target`: that entry is not an IPv4 address or the port is outside 1-65535.
- Component enabled and no error, but the ESP sees nothing: wrong IP or port, different network or subnet, router client isolation, or a firewall.
- Vibration is delayed or jerky: Wi-Fi sleep still on, weak signal, or a busy network.

### The following is mainly for Loïc
## Unity side: `ChoirHapticAdvancedBridge`

Add it to the same GameObject as `ChoirHost` (or assign the host in the Inspector).

- **Targets:** `Esp32 IP` + `Port` is the first bracelet (these two fields stay so older scenes keep working; leave the IP empty to use only the list). **Additional Targets** is a list with one IP and port per further bracelet. Duplicates are ignored. Every bracelet gets the same byte.
- **Send Rate Hz:** how often a new value is computed (the frame rate also limits it). **Keep Alive Ms:** minimum resend interval for an unchanged value, 0 = only on change.
- **Source (switch while playing, Inspector or the buttons at the bottom left of the Game view):**
  - *Envelope*: loudness + spectral flux + onset punch, then the shaping below (unchanged behaviour, the default).
  - *Harmony*: the harmonic tension of the voices (needs **Estimate Key** on the ChoirHost, otherwise the Game view warns). Intensity = `Harmony Base` + (1 - `Harmony Base`) x tension, times a presence factor from the envelope so it goes to 0 when nobody sings, smoothed with `Harmony Attack/Release Ms` (200 / 600 ms by default; the tension itself already lags about 0.5 s). No onset punch in this mode. `Harmony Gain` scales the tension if it rarely gets high.
  - *EnvelopeBeat* ("Env + beat" button): the envelope path above, with a **beating** on top. A raised-cosine wobble multiplies the level: the peaks keep the envelope level and the dips go down by `Beat Max Depth` (0.7 = to 30%) at full tension. Depth and speed follow the smoothed tension: no beating below `Beat Threshold` (0.15, a calm chord is a plain envelope), speed from `Beat Rate Min Hz` (3) to `Beat Rate Max Hz` (12) at full tension. It never exceeds the plain envelope and is silent when the envelope is, so the loudness stays in charge. The wobble is sampled at the update rate (about the frame rate, at most the send rate), so keep the maximum rate below about a quarter of it.
  - **Beat On Esp** (3-byte packet): see "Optional 3-byte packet" below.
  - The line under the buttons shows the number of targets and the value being sent (and the current beat depth and speed).
- **Feature Weights / Shaping / Smoothing** (envelope source): envelope + flux + onset bonus, then noise gate, gamma, soft clip (tanh) and attack/release smoothing; **Max Intensity** is the headroom (the largest byte value that is ever sent).
- Changes to targets, rate and keep-alive apply when the component is disabled and enabled again (or on the next Play).
- Console messages: invalid targets (once), UDP errors (each distinct error once).

## ESP side in this repository

`tools/beetle_wifi_unity.ino`: an ESP32-C3 (DFRobot Beetle) with a TCA9548A multiplexer and two DRV2605 drivers (channels 6 and 7) for two LRAs, in open-loop real-time (RTP) mode at a fixed 170 Hz. It reads 1-byte packets, keeps the newest, clamps to 0..127 and writes it to both actuators. So the only thing Unity can control is the **amplitude**, and both actuators always get the same value. Not yet in the firmware: the safety timeout described above (the last value stays if Unity stops) and keeping the Wi-Fi credentials out of the file.

### New firmware: `tools/beetle_wifi_beating/`
An Arduino sketch folder (open `beetle_wifi_beating.ino`). **Setup:** copy `secrets.example.h` to `secrets.h` in that folder and write your Wi-Fi name and password there; `secrets.h` is ignored by version control. Same hardware and drive settings as the older file, plus: the 3-byte packet with the ESP-side beating (500 Hz updates, 400 kHz I2C), a 300 ms safety timeout (vibration off when Unity stops), and the IP printed every 10 s on the serial monitor. Untested on hardware: check the serial monitor for `IP:` and `(receiving)`, then raise `Beat Rate Max Hz` step by step to find what the actuator can render.

## Test without an ESP

1. In Unity set **Esp32 IP** to `127.0.0.1` (empty the extra list) and press Play with audio running.
2. In a terminal at the repository root: `python tools/udp_listen.py`
3. You should see an `intensity` value and bar moving. The packet rate is lower than the send rate because unchanged values are only resent every keep-alive interval (about 10 pkt/s at silence or a steady note, more when the level moves).

To test with a real ESP, use its IP instead of `127.0.0.1`.

## Older sender (`UdpHapticSender`, not used in the scene)

Own thread, fixed rate (default 200 pkt/s), a list of targets, status line in the Game view, output gain. Packet of 5 bytes, little-endian: byte 0 marker `0xA5`; bytes 1-2 uint16 sequence number (+1 per packet, wraps); bytes 3-4 uint16 envelope (0..65535). The ESP drops a packet whose sequence is not newer: `(int16_t)(seq - lastSeq) <= 0`. It carries only the envelope (no flux or onset shaping). Kept because its sequence number and threaded timing are more robust; `tools/udp_listen.py` also understands this format. To use it, disable the bridge (the ESP must then parse 5 bytes).
