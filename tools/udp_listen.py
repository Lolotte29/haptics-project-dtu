"""Prints the envelope packets sent by UdpHapticSender, so you can test without an ESP.

    python tools/udp_listen.py            # listens on port 4210
    python tools/udp_listen.py 4210

In Unity the sender target must be this computer: 127.0.0.1, same port. Stop with Ctrl+C.
Packet format: see docs/esp-wifi.md.
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
env = 0.0

try:
    while True:
        try:
            data, addr = sock.recvfrom(64)
        except socket.timeout:
            data = None
        now = time.time()
        if data is not None:
            if len(data) != 5 or data[0] != MAGIC:
                ignored += 1
            else:
                seq, raw = struct.unpack_from("<HH", data, 1)
                env = raw / 65535
                if last_seq is not None:
                    gap = (seq - last_seq) & 0xFFFF
                    if 1 < gap < 0x8000:
                        lost += gap - 1
                last_seq = seq
                received += 1
                window_count += 1
        if now - window_start >= 0.5:
            rate = window_count / (now - window_start)
            bar = "#" * int(env * 40)
            print(f"\r{rate:6.0f} pkt/s  env {env:4.2f} |{bar:<40}| received {received} lost {lost} ignored {ignored}   ", end="", flush=True)
            window_start, window_count = now, 0
except KeyboardInterrupt:
    print()
