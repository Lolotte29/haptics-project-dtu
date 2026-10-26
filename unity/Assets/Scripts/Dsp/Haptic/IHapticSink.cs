namespace Haptics.Dsp
{
    /// <summary>Where haptic commands go. Implement for logging/visualisation now, Bluetooth/ESP later.</summary>
    public interface IHapticSink
    {
        void Send(HapticFrame frame);
    }
}
