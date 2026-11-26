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
- **Feature Weights / Shaping / Smoothing:** envelope + flux + onset bonus, then noise gate, gamma, soft clip (tanh) and attack/release smoothing; **Max Intensity** is the headroom (the largest byte value that is ever sent).
- Changes to targets, rate and keep-alive apply when the component is disabled and enabled again (or on the next Play).
- Console messages: invalid targets (once), UDP errors (each distinct error once).

## Test without an ESP

1. In Unity set **Esp32 IP** to `127.0.0.1` (empty the extra list) and press Play with audio running.
2. In a terminal at the repository root: `python tools/udp_listen.py`
3. You should see an `intensity` value and bar moving. The packet rate is lower than the send rate because unchanged values are only resent every keep-alive interval (about 10 pkt/s at silence or a steady note, more when the level moves).

To test with a real ESP, use its IP instead of `127.0.0.1`.

## Older sender (`UdpHapticSender`, not used in the scene)

Own thread, fixed rate (default 200 pkt/s), a list of targets, status line in the Game view, output gain. Packet of 5 bytes, little-endian: byte 0 marker `0xA5`; bytes 1-2 uint16 sequence number (+1 per packet, wraps); bytes 3-4 uint16 envelope (0..65535). The ESP drops a packet whose sequence is not newer: `(int16_t)(seq - lastSeq) <= 0`. It carries only the envelope (no flux or onset shaping). Kept because its sequence number and threaded timing are more robust; `tools/udp_listen.py` also understands this format. To use it, disable the bridge (the ESP must then parse 5 bytes).
