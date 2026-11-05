using Haptics.Dsp;
using Xunit;

public class HapticPacketTests
{
    [Fact]
    public void RoundTripKeepsSequenceAndEnvelope()
    {
        var buf = new byte[HapticPacket.Size];
        HapticPacket.Write(buf, 513, 0.25f);
        Assert.True(HapticPacket.TryRead(buf, buf.Length, out ushort seq, out float env));
        Assert.Equal(513, seq);
        Assert.Equal(0.25f, env, 4);
    }

    [Fact]
    public void BytesAreLittleEndianWithMagicFirst()
    {
        var buf = new byte[HapticPacket.Size];
        HapticPacket.Write(buf, 0x0102, 1f);
        Assert.Equal(new byte[] { 0xA5, 0x02, 0x01, 0xFF, 0xFF }, buf);
    }

    [Theory]
    [InlineData(-1f, 0)]
    [InlineData(float.NaN, 0)]
    [InlineData(0f, 0)]
    [InlineData(1f, 65535)]
    [InlineData(7f, 65535)]
    public void EnvelopeIsClamped(float input, int expected) =>
        Assert.Equal(expected, HapticPacket.Quantize(input));

    [Fact]
    public void SequenceWrapsAt16Bits()
    {
        var buf = new byte[HapticPacket.Size];
        HapticPacket.Write(buf, ushort.MaxValue, 0f);
        HapticPacket.TryRead(buf, buf.Length, out ushort seq, out _);
        Assert.Equal(ushort.MaxValue, seq);
        Assert.Equal((ushort)0, (ushort)(seq + 1));
    }

    [Fact]
    public void RejectsShortOrForeignPackets()
    {
        Assert.False(HapticPacket.TryRead(new byte[] { 0xA5, 0, 0 }, 3, out _, out _));
        Assert.False(HapticPacket.TryRead(new byte[] { 0x00, 0, 0, 0, 0 }, 5, out _, out _));
    }

    [Fact]
    public void LiveEnvelopeFollowsInputWithoutWaitingForAnalysisFrame()
    {
        var choir = new Choir(44100, 1);
        var block = new float[64];
        for (int i = 0; i < block.Length; i++) block[i] = 0.5f;
        choir.Process(block, block.Length, 1); // far fewer samples than one analysis frame
        Assert.True(choir.EnvelopeLive > 0.1f);
    }
}
