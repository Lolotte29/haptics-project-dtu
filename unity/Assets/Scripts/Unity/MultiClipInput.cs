using System.Collections;
using Haptics.Dsp;
using UnityEngine;

/// <summary>
/// Input adapter for separate recordings, one mono clip per singer (e.g. the Dagstuhl ChoirSet quartets):
/// clip i becomes channel i / voice i of <see cref="ChoirHost"/>. It plays the clips so you hear the choir and
/// feeds the analysis from the same clips on the same audio clock, so sound and analysis stay in step.
/// The analysis side only ever calls <see cref="ChoirHost.Push"/> with an interleaved N-channel buffer, so a
/// future live-microphone adapter (one multichannel device, or several devices) is a sibling component that
/// does the same; nothing downstream changes.
/// Clip import settings: Load Type = Decompress On Load (so GetData works), same sample rate and length for all.
/// </summary>
public class MultiClipInput : MonoBehaviour
{
    [SerializeField] private ChoirHost host;
    [Tooltip("One mono clip per singer, in voice order (voice 1 first). All must be synchronised, same sample rate.")]
    [SerializeField] private AudioClip[] clips;
    [SerializeField] private bool loop = true;
    [Tooltip("Plays the clips so you can hear them. Analysis runs either way.")]
    [SerializeField] private bool monitor = true;
    [Range(0f, 1f)] [SerializeField] private float monitorVolume = 0.8f;
    [Tooltip("Stereo spread of the singers when monitoring: 0 = all centre, 1 = first voice hard left, last hard right.")]
    [Range(0f, 1f)] [SerializeField] private float monitorSpread = 0.5f;

    private const int ChunksPerSecond = 100;       // 10 ms analysis chunks
    private const double StartDelaySeconds = 0.3;  // lets every AudioSource start on the same DSP tick

    private int _rate, _length, _chunkFrames;
    private long _totalRead;      // frames handed to the analysis since start (not wrapped)
    private double _startDsp;
    private float[][] _scratch;   // per clip: GetData target (clip may have several channels; channel 0 is used)
    private float[] _interleaved; // chunk x clips
    private bool _running;

    private IEnumerator Start()
    {
        if (host == null || clips == null || clips.Length == 0)
        {
            Debug.LogError("MultiClipInput: assign a ChoirHost and at least one clip.");
            yield break;
        }
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] == null) { Debug.LogError($"MultiClipInput: clip {i + 1} is empty."); yield break; }
            clips[i].LoadAudioData();
        }
        foreach (var c in clips) while (c.loadState == AudioDataLoadState.Loading) yield return null;

        _rate = clips[0].frequency;
        _length = clips[0].samples;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i].loadState != AudioDataLoadState.Loaded)
            { Debug.LogError($"MultiClipInput: clip '{clips[i].name}' failed to load (use Decompress On Load)."); yield break; }
            if (clips[i].frequency != _rate)
            { Debug.LogError($"MultiClipInput: '{clips[i].name}' is {clips[i].frequency} Hz, expected {_rate} Hz."); yield break; }
            if (clips[i].samples < _length) _length = clips[i].samples; // use the common length
        }
        if (host.Voices != null && host.Voices.Length != clips.Length)
            Debug.LogWarning($"MultiClipInput: {clips.Length} clips but ChoirHost has {host.Voices.Length} voices " +
                             "(extra voices stay silent, extra clips are ignored).");

        host.Configure(_rate);
        _chunkFrames = Mathf.Max(1, _rate / ChunksPerSecond);
        _scratch = new float[clips.Length][];
        for (int i = 0; i < clips.Length; i++) _scratch[i] = new float[_chunkFrames * clips[i].channels];
        _interleaved = new float[_chunkFrames * clips.Length];

        _startDsp = AudioSettings.dspTime + StartDelaySeconds;
        if (monitor) StartMonitoring();
        _totalRead = 0;
        _running = true;
        Debug.Log($"MultiClipInput: {clips.Length} clips, {_rate} Hz, {(double)_length / _rate:0.0} s.");
    }

    private void StartMonitoring()
    {
        for (int i = 0; i < clips.Length; i++)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.clip = clips[i];
            src.loop = loop;
            src.volume = monitorVolume / Mathf.Sqrt(clips.Length);
            src.spatialBlend = 0f;
            src.panStereo = clips.Length > 1 ? Mathf.Lerp(-1f, 1f, i / (float)(clips.Length - 1)) * monitorSpread : 0f;
            src.PlayScheduled(_startDsp);
        }
    }

    private void Update()
    {
        if (!_running) return;

        // Where playback is now, in frames, on the audio clock. The analysis follows this position.
        double elapsed = AudioSettings.dspTime - _startDsp;
        if (elapsed < 0) return;
        long target = (long)(elapsed * _rate);

        // More than ~1 s behind (editor paused): skip ahead instead of analysing stale audio.
        if (target - _totalRead > _rate) _totalRead = target;

        while (target - _totalRead >= _chunkFrames)
        {
            int pos = (int)(_totalRead % _length);
            if (pos + _chunkFrames > _length) // tail shorter than a chunk: skip it, stay aligned with the loop
            {
                _totalRead += _length - pos;
                continue;
            }
            if (!loop && _totalRead + _chunkFrames > _length) { _running = false; return; }

            for (int c = 0; c < clips.Length; c++)
            {
                clips[c].GetData(_scratch[c], pos);
                ChannelMap.Copy(_scratch[c], clips[c].channels, 0, _interleaved, clips.Length, c, _chunkFrames);
            }
            _totalRead += _chunkFrames;
            host.Push(_interleaved, _chunkFrames, clips.Length);
        }
    }
}
