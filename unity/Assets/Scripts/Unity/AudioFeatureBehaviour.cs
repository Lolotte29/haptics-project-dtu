using Haptics.Dsp;
using UnityEngine;

/// <summary>
/// Attach next to an AudioSource (or on the AudioListener's object). Runs the DSP library on the audio thread
/// and exposes the latest features to the main thread.
/// </summary>
public class AudioFeatureBehaviour : MonoBehaviour
{
    [SerializeField] private int frameSize = 1024;
    [SerializeField] private int hopSize = 512;

    private FeatureExtractor _extractor;
    private float[] _mono = new float[0];

    // Written on the audio thread, read on the main thread. A torn read of a few floats is harmless for visuals/haptics.
    public volatile bool OnsetPending;
    public float Rms { get; private set; }
    public float SpectralCentroid { get; private set; }
    public float SpectralFlux { get; private set; }

    private void Awake()
    {
        _extractor = new FeatureExtractor(AudioSettings.outputSampleRate, frameSize, hopSize);
    }

    private void OnAudioFilterRead(float[] data, int channels)
    {
        if (_extractor == null) return;

        int frames = data.Length / channels;
        if (_mono.Length < frames) _mono = new float[frames];
        for (int i = 0; i < frames; i++)
        {
            float sum = 0f;
            for (int c = 0; c < channels; c++) sum += data[i * channels + c];
            _mono[i] = sum / channels;
        }

        _extractor.Process(_mono, 0, frames, OnFrame);
    }

    private void OnFrame(FeatureFrame f)
    {
        Rms = f.Rms;
        SpectralCentroid = f.SpectralCentroid;
        SpectralFlux = f.SpectralFlux;
        if (f.Onset) OnsetPending = true;
    }

    /// <summary>Returns true once per detected onset (call from Update).</summary>
    public bool ConsumeOnset()
    {
        if (!OnsetPending) return false;
        OnsetPending = false;
        return true;
    }
}
