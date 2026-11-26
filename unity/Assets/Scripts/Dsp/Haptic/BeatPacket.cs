using System;

namespace Haptics.Dsp
{
    /// <summary>
    /// 3-byte UDP packet for ESP firmware that makes the beating itself (tools/beetle_wifi_beating):
    /// byte 0 = intensity 0..127 (the same value the plain 1-byte packet carries), byte 1 = beat depth 0..255 (= 0..1),
    /// byte 2 = beat rate in steps of 0.5 Hz (0..127.5 Hz). Firmware that only reads the first byte (the older
    /// tools/beetle_wifi_unity.ino) still works: it sees the intensity and ignores the rest.
    /// Pure data conversion, allocation-free.
    /// </summary>
    public static class BeatPacket
    {
        public const int Size = 3;
        public const int MaxIntensity = 127;

        /// <summary>Writes a packet into <paramref name="buffer"/> (at least <see cref="Size"/> bytes). Inputs are clamped.</summary>
        public static void Write(byte[] buffer, int intensity, float depth, float rateHz)
        {
            if (buffer == null || buffer.Length < Size) throw new ArgumentException("buffer too small", nameof(buffer));
            buffer[0] = (byte)Math.Max(0, Math.Min(MaxIntensity, intensity));
            buffer[1] = DepthToByte(depth);
            buffer[2] = RateToByte(rateHz);
        }

        /// <summary>Parses a packet; false if shorter than <see cref="Size"/>.</summary>
        public static bool TryRead(byte[] buffer, int length, out int intensity, out float depth, out float rateHz)
        {
            intensity = 0; depth = 0f; rateHz = 0f;
            if (buffer == null || length < Size) return false;
            intensity = buffer[0];
            depth = buffer[1] / 255f;
            rateHz = buffer[2] * 0.5f;
            return true;
        }

        /// <summary>0..1 to 0..255; NaN and negatives give 0.</summary>
        public static byte DepthToByte(float depth)
        {
            if (!(depth > 0f)) return 0;
            if (depth >= 1f) return 255;
            return (byte)(depth * 255f + 0.5f);
        }

        /// <summary>Hz to half-Hz steps, 0..255; NaN and negatives give 0.</summary>
        public static byte RateToByte(float rateHz)
        {
            if (!(rateHz > 0f)) return 0;
            if (rateHz >= 127.5f) return 255;
            return (byte)(rateHz * 2f + 0.5f);
        }
    }
}
