using System.Collections;
using UnityEngine;

/// <summary>
/// Input adapter feeding <see cref="ChoirHost"/> (channel i -> voice i).
/// Either play a clip through the AudioSource, or tick "Use Microphone" to read a capture device
/// (e.g. "CABLE Output (VB-Audio Virtual Cable)") directly from its capture buffer.
/// The microphone path never plays the audio back, so there are no speakers to mute and, importantly, no
/// playback clock that can drift against the capture clock and cut glitches into the signal.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class AudioSourceInput : MonoBehaviour
{
    [SerializeField] private ChoirHost host;
    [SerializeField] private bool useMicrophone;
    [Tooltip("Capture device name; empty = system default. See the console list printed at start.")]
    [SerializeField] private string microphoneDevice = "";

    private const int ChunksPerSecond = 100; // 10 ms chunks; clip length is a whole number of them (no wrap inside a chunk)

    private AudioClip _clip;
    private string _device;
    private float[] _chunk;
    private int _chunkFrames;
    private int _readPos; // next frame to read from the capture clip

    private IEnumerator Start()
    {
        if (!useMicrophone) yield break;

        foreach (var d in Microphone.devices) Debug.Log("Microphone device: " + d);

        _device = string.IsNullOrEmpty(microphoneDevice) ? null : microphoneDevice;
        var clip = Microphone.Start(_device, true, 1, AudioSettings.outputSampleRate);
        if (clip == null)
        {
            Debug.LogError("Microphone.Start failed for device: " + (_device ?? "(default)"));
            yield break;
        }
        while (Microphone.GetPosition(_device) <= 0) yield return null; // wait for capture to start

        // The device may not honour the requested rate or channel count: analyse what we actually get.
        _chunkFrames = Mathf.Max(1, clip.frequency / ChunksPerSecond);
        _chunk = new float[_chunkFrames * clip.channels];
        _readPos = Microphone.GetPosition(_device);
        if (host != null) host.Configure(clip.frequency);
        Debug.Log($"Microphone capture: {clip.frequency} Hz, {clip.channels} channel(s), " +
                  $"{clip.samples} frames. Voices beyond channel {clip.channels} receive silence.");

        _clip = clip; // publish last: Update must not run before the fields above are set
    }

    private void Update()
    {
        if (_clip == null || host == null) return;

        int clipFrames = _clip.samples;
        int write = Microphone.GetPosition(_device);
        int available = (write - _readPos + clipFrames) % clipFrames;

        // Fell more than ~1 s behind (e.g. editor paused): drop the backlog rather than analyse stale audio.
        if (available > clipFrames - 2 * _chunkFrames)
        {
            _readPos = write;
            return;
        }

        while (available >= _chunkFrames)
        {
            _clip.GetData(_chunk, _readPos);
            host.Push(_chunk, _chunkFrames, _clip.channels);
            _readPos = (_readPos + _chunkFrames) % clipFrames;
            available -= _chunkFrames;
        }
    }

    private void OnDestroy()
    {
        if (_clip != null) Microphone.End(_device);
    }

    // Clip playback path (not used for the microphone, which is read directly in Update).
    private void OnAudioFilterRead(float[] data, int channels)
    {
        if (useMicrophone || host == null) return;
        host.Push(data, data.Length / channels, channels);
    }
}
