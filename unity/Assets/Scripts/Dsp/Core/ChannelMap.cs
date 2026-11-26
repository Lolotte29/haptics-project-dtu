namespace Haptics.Dsp
{
    /// <summary>
    /// Copies one channel of an interleaved buffer into one channel of another interleaved buffer.
    /// Lets an input adapter assemble the N-channel stream that <see cref="Choir"/> expects from separate
    /// sources (mono files, one device per microphone, ...). Allocation-free.
    /// </summary>
    public static class ChannelMap
    {
        /// <summary>
        /// dst[f * dstChannels + dstChannel] = src[f * srcChannels + srcChannel] for f in 0..frames-1.
        /// Other channels of <paramref name="dst"/> are left untouched.
        /// </summary>
        public static void Copy(float[] src, int srcChannels, int srcChannel,
                                float[] dst, int dstChannels, int dstChannel, int frames)
        {
            int s = srcChannel, d = dstChannel;
            for (int f = 0; f < frames; f++)
            {
                dst[d] = src[s];
                s += srcChannels;
                d += dstChannels;
            }
        }
    }
}
