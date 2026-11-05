# ESP Wi-Fi link (for the ESP side)

Unity sends the live amplitude envelope of the choir to every bracelet over **UDP**. Every ESP receives the same packets. There is no reply and no connection: just listen on a port and use the newest value.

## What the ESP must do

1. Join the Wi-Fi network the Unity computer is on (same network, same subnet).
2. Have a **fixed IP** (DHCP reservation in the router, or a static IP) and give it to whoever runs Unity.
3. Listen on a UDP port (default **4210**, any port works if Unity uses the same one).
4. For each packet: check it, take the newest envelope, set the vibration strength.
5. **Safety timeout:** if no valid packet arrives for about 200 ms, set the vibration to 0.

Tips for low latency: call `WiFi.setSleep(false)` (the default power save adds 100 ms or more), never block the loop (poll `parsePacket()`), and always use the newest packet only.

## Packet format

5 bytes, **little-endian** (same as the ESP):

| Byte | Type | Meaning |
|------|------|---------|
| 0 | uint8 | Marker, always `0xA5`. Ignore any packet that does not start with it or is not exactly 5 bytes. |
| 1-2 | uint16 | Sequence number, +1 per packet, wraps from 65535 to 0. Drop a packet if it is older than the last one (`(int16_t)(seq - lastSeq) <= 0`). |
| 3-4 | uint16 | Envelope: 0 = silence, 65535 = full scale. Map to your PWM range. |

Rate: **200 packets per second** by default (set in Unity, 10-1000). The same packet goes to all ESPs. The ESP can ignore the rate; it should just use the latest value.

## Network setup

- **Use your own router or access point** for tests and the concert. School and venue networks often isolate devices from each other (nothing arrives) and add delay.
- The Unity computer and all ESPs must be on that network. Windows may ask to allow Unity through the firewall on first run: allow it for private networks.
- Unity's computer sends to each ESP's IP and port directly (no broadcast).

## Unity side

`UdpHapticSender` is on the same GameObject as `ChoirHost` (or assign it in the Inspector).
- **Targets:** one entry per bracelet, IP and port, e.g. `192.168.1.51` / `4210`.
- **Send Rate Hz**, **Output Gain** (multiplies the envelope before sending; a sung voice is quiet, raise it if the bracelets feel weak).
- Changes to targets, rate and gain apply when the component is disabled and enabled again (or on the next Play).
- The line at the bottom left of the Game view shows target count, packets per second, the envelope sent, and any send error.

## Test without an ESP

1. In Unity set one target `127.0.0.1` / `4210` and press Play with audio running.
2. In a terminal at the repository root: `python tools/udp_listen.py`
3. You should see about 200 pkt/s, a moving `env` bar and `lost 0`. `lost` counts missing sequence numbers.

To test with a real ESP, use its IP instead of `127.0.0.1`; you can still run the listener on the computer to confirm Unity is sending, since the status line already shows that.

## Checklist when nothing arrives

- Unity status line shows 0 pkt/s: the component is disabled or has no valid target (see Console).
- Status line shows `ERROR`: the computer has no network or the IP is unreachable.
- Pkt/s fine but the ESP sees nothing: wrong IP or port, different network or subnet, router client isolation, or a firewall.
- Vibration is delayed or jerky: Wi-Fi sleep still on, weak signal, or a busy network.
