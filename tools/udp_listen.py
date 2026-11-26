"""Prints the packets sent by Unity, so you can test without an ESP.

    python tools/udp_listen.py            # listens on port 4210
    python tools/udp_listen.py 4210

In Unity (ChoirHapticAdvancedBridge) the target must be this computer: 127.0.0.1, same port. Stop with Ctrl+C.
Understands these formats, see docs/esp-wifi.md:
  - 1 byte  = intensity 0..127 (ChoirHapticAdvancedBridge, the one in use)
  - 3 bytes = intensity, beat depth (0..255 = 0..1), beat rate (0.5 Hz steps) (bridge with Beat On Esp)
  - 5 bytes = marker 0xA5 + sequence + 16-bit envelope (older UdpHapticSender)
The bridge only sends when the value changes or every keep-alive interval, so pkt/s is lower than its send rate.
"""
import socket
import struct
import sys
import time

MAGIC = 0xA5
port = int(sys.argv[1]) if len(sys.argv) > 1 else 4210

sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
sock.bind(("0.0.0.0", port))
sock.settimeout(0.5)
print(f"Listening on UDP port {port} ... (Ctrl+C to stop)")

last_seq = None
received = lost = ignored = 0
window_start = time.time()
window_count = 0
level = 0.0      # 0..1 for the bar
text = ""

try:
    while True:
        try:
            data, addr = sock.recvfrom(64)
        except socket.timeout:
            data = None
        now = time.time()
        if data is not None:
            if len(data) == 1:
                level = min(data[0] / 127, 1.0)
                text = f"intensity {data[0]:3d}"
                received += 1
                window_count += 1
            elif len(data) == 3:
                level = min(data[0] / 127, 1.0)
                text = f"int {data[0]:3d} d{data[1] / 255:3.1f} {data[2] / 2:4.1f}Hz"
                received += 1
                window_count += 1
            elif len(data) == 5 and data[0] == MAGIC:
                seq, raw = struct.unpack_from("<HH", data, 1)
                level = raw / 65535
                text = f"env {level:4.2f}"
                if last_seq is not None:
                    gap = (seq - last_seq) & 0xFFFF
                    if 1 < gap < 0x8000:
                        lost += gap - 1
                last_seq = seq
                received += 1
                window_count += 1
            else:
                ignored += 1
        if now - window_start >= 0.5:
            rate = window_count / (now - window_start)
            bar = "#" * int(level * 40)
            print(f"\r{rate:6.0f} pkt/s  {text:<14} |{bar:<40}| received {received} lost {lost} ignored {ignored}   ", end="", flush=True)
            window_start, window_count = now, 0
except KeyboardInterrupt:
    print()
