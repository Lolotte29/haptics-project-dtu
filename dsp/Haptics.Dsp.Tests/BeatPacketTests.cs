using Haptics.Dsp;
using Xunit;

public class BeatPacketTests
{
    [Fact]
    public void RoundTripsIntensityDepthAndRate()
    {
        var buf = new byte[BeatPacket.Size];
        BeatPacket.Write(buf, 80, 0.5f, 12.5f);
        Assert.True(BeatPacket.TryRead(buf, buf.Length, out int intensity, out float depth, out float rate));
        Assert.Equal(80, intensity);
        Assert.Equal(0.5f, depth, 2);
        Assert.Equal(12.5f, rate, 3);
    }

    [Fact]
    public void FirstByteIsTheIntensitySoOldFirmwareStillWorks()
    {
        var buf = new byte[BeatPacket.Size];
        BeatPacket.Write(buf, 55, 1f, 40f);
        Assert.Equal(55, buf[0]);
    }

    [Theory]
    [InlineData(-5, 0)]
    [InlineData(0, 0)]
    [InlineData(127, 127)]
    [InlineData(500, 127)]
    public void IntensityIsClampedToTheRtpRange(int input, int expected)
    {
        var buf = new byte[BeatPacket.Size];
        BeatPacket.Write(buf, input, 0f, 0f);
        Assert.Equal(expected, buf[0]);
    }

    [Theory]
    [InlineData(float.NaN, 0)]
    [InlineData(-1f, 0)]
    [InlineData(0f, 0)]
    [InlineData(1f, 255)]
    [InlineData(7f, 255)]
    public void DepthIsClampedAndQuantised(float input, int expected) =>
        Assert.Equal(expected, BeatPacket.DepthToByte(input));

    [Theory]
    [InlineData(float.NaN, 0)]
    [InlineData(-3f, 0)]
    [InlineData(3f, 6)]
    [InlineData(60f, 120)]
    [InlineData(500f, 255)]
    public void RateIsInHalfHertzStepsAndClamped(float input, int expected) =>
        Assert.Equal(expected, BeatPacket.RateToByte(input));

    [Fact]
    public void ShortPacketsAreRejected()
    {
        Assert.False(BeatPacket.TryRead(new byte[] { 50 }, 1, out _, out _, out _));
        Assert.False(BeatPacket.TryRead(new byte[] { 50, 1, 2 }, 2, out _, out _, out _));
    }
}
