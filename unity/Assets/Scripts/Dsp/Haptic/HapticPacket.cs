using System;

namespace Haptics.Dsp
{
    /// <summary>
    /// Wire format of the UDP packet sent to the ESP bracelets (5 bytes, little-endian, see docs/esp-wifi.md):
    /// byte 0 = <see cref="Magic"/>, bytes 1-2 = sequence number (wraps at 65535), bytes 3-4 = envelope 0..65535.
    /// Pure data conversion, allocation-free.
    /// </summary>
    public static class HapticPacket
    {
        public const int Size = 5;
        public const byte Magic = 0xA5;

        /// <summary>Writes a packet into <paramref name="buffer"/> (at least <see cref="Size"/> bytes). Envelope is clamped to 0..1.</summary>
        public static void Write(byte[] buffer, ushort sequence, float envelope)
        {
            if (buffer == null || buffer.Length < Size) throw new ArgumentException("buffer too small", nameof(buffer));
            ushort level = Quantize(envelope);
            buffer[0] = Magic;
            buffer[1] = (byte)(sequence & 0xFF);
            buffer[2] = (byte)(sequence >> 8);
            buffer[3] = (byte)(level & 0xFF);
            buffer[4] = (byte)(level >> 8);
        }

        /// <summary>Parses a packet; false if too short or the magic byte is wrong.</summary>
        public static bool TryRead(byte[] buffer, int length, out ushort sequence, out float envelope)
        {
            sequence = 0;
            envelope = 0f;
            if (buffer == null || length < Size || buffer[0] != Magic) return false;
            sequence = (ushort)(buffer[1] | (buffer[2] << 8));
            envelope = (buffer[3] | (buffer[4] << 8)) / 65535f;
            return true;
        }

        /// <summary>Maps 0..1 to 0..65535; NaN and negatives give 0.</summary>
        public static ushort Quantize(float envelope)
        {
            if (!(envelope > 0f)) return 0;
            if (envelope >= 1f) return ushort.MaxValue;
            return (ushort)(envelope * 65535f + 0.5f);
        }
    }
}
