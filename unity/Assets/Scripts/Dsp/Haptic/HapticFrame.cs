namespace Haptics.Dsp
{
    /// <summary>One haptic command, broadcast identically to every bracelet.</summary>
    public struct HapticFrame
    {
        /// <summary>Vibration strength 0..1.</summary>
        public float Intensity;
        /// <summary>True for one frame when a transient should be felt.</summary>
        public bool Pulse;
    }
}
