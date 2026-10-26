using System;

namespace Haptics.Dsp
{
    /// <summary>In-place radix-2 FFT and a Hann window. No allocations after construction.</summary>
    public sealed class Fft
    {
        private readonly int _size;
        private readonly float[] _cos;
        private readonly float[] _sin;
        private readonly int[] _rev;
        private readonly float[] _window;
        private readonly float[] _re;
        private readonly float[] _im;

        public int Size => _size;
        public int BinCount => _size / 2 + 1;

        public Fft(int size)
        {
            if (size < 2 || (size & (size - 1)) != 0)
                throw new ArgumentException("FFT size must be a power of two.", nameof(size));

            _size = size;
            _cos = new float[size / 2];
            _sin = new float[size / 2];
            for (int i = 0; i < size / 2; i++)
            {
                double a = -2.0 * Math.PI * i / size;
                _cos[i] = (float)Math.Cos(a);
                _sin[i] = (float)Math.Sin(a);
            }

            int bits = 0;
            while ((1 << bits) < size) bits++;
            _rev = new int[size];
            for (int i = 0; i < size; i++)
            {
                int r = 0;
                for (int b = 0; b < bits; b++)
                    if ((i & (1 << b)) != 0) r |= 1 << (bits - 1 - b);
                _rev[i] = r;
            }

            _window = new float[size];
            for (int i = 0; i < size; i++)
                _window[i] = (float)(0.5 - 0.5 * Math.Cos(2.0 * Math.PI * i / size));

            _re = new float[size];
            _im = new float[size];
        }

        /// <summary>
        /// Windows <paramref name="input"/> (length >= Size) and writes the magnitude spectrum
        /// (BinCount values, normalised so a full-scale sine peaks near 0.5) into <paramref name="magnitudes"/>.
        /// </summary>
        public void Magnitude(float[] input, float[] magnitudes)
        {
            if (input.Length < _size) throw new ArgumentException("Input shorter than FFT size.", nameof(input));
            if (magnitudes.Length < BinCount) throw new ArgumentException("Output too small.", nameof(magnitudes));

            for (int i = 0; i < _size; i++)
            {
                int j = _rev[i];
                _re[j] = input[i] * _window[i];
                _im[j] = 0f;
            }

            for (int len = 2; len <= _size; len <<= 1)
            {
                int half = len / 2;
                int step = _size / len;
                for (int start = 0; start < _size; start += len)
                {
                    for (int k = 0; k < half; k++)
                    {
                        float c = _cos[k * step];
                        float s = _sin[k * step];
                        int a = start + k;
                        int b = a + half;
                        float tr = _re[b] * c - _im[b] * s;
                        float ti = _re[b] * s + _im[b] * c;
                        _re[b] = _re[a] - tr;
                        _im[b] = _im[a] - ti;
                        _re[a] += tr;
                        _im[a] += ti;
                    }
                }
            }

            float norm = 2f / _size;
            for (int i = 0; i < BinCount; i++)
                magnitudes[i] = (float)Math.Sqrt(_re[i] * _re[i] + _im[i] * _im[i]) * norm;
        }
    }
}
