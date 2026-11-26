using Haptics.Dsp;
using Xunit;

public class ChannelMapTests
{
    [Fact]
    public void InterleavesFourMonoSourcesIntoFourChannels()
    {
        const int frames = 3;
        var dst = new float[frames * 4];
        for (int c = 0; c < 4; c++)
        {
            var mono = new float[frames];
            for (int f = 0; f < frames; f++) mono[f] = c * 10 + f;
            ChannelMap.Copy(mono, 1, 0, dst, 4, c, frames);
        }
        Assert.Equal(new float[] { 0, 10, 20, 30, 1, 11, 21, 31, 2, 12, 22, 32 }, dst);
    }

    [Fact]
    public void PicksRequestedChannelOfStereoSourceAndLeavesOthersAlone()
    {
        var stereo = new float[] { 1, -1, 2, -2 }; // L R L R
        var dst = new float[] { 9, 9, 9, 9, 9, 9 }; // 3 channels, 2 frames
        ChannelMap.Copy(stereo, 2, 1, dst, 3, 2, 2);
        Assert.Equal(new float[] { 9, 9, -1, 9, 9, -2 }, dst);
    }
}
