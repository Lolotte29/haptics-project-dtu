using System;
using Haptics.Dsp;
using UnityEngine;

/// <summary>
/// Owns the analysis pipeline (<see cref="Choir"/> + <see cref="HapticMapper"/>). Any input component calls
/// <see cref="Push"/> with interleaved audio (voice i reads channel i). Shows a debug HUD.
/// </summary>
public class ChoirHost : MonoBehaviour
{
    [SerializeField] private int voiceCount = 4;
    [SerializeField] private bool showHud = true;

    private Choir _choir;
    private readonly HapticMapper _mapper = new HapticMapper();
    private volatile bool _pulsePending;

    // Written on the audio thread, read on the main thread. A torn read of a few floats is harmless here.
    public ChoirFrame Frame { get; private set; }
    public HapticFrame Haptic { get; private set; }
    public VoiceFrame[] Voices => _choir?.Voices;

    /// <summary>Set to receive one HapticFrame per Unity Update (e.g. the future Bluetooth sender).</summary>
    public IHapticSink Sink { get; set; }

    private void Awake()
    {
        _choir = new Choir(AudioSettings.outputSampleRate, voiceCount);
    }

    /// <summary>Called from the audio thread by an input component.</summary>
    public void Push(float[] interleaved, int frames, int channels)
    {
        if (_choir == null) return;
        if (!_choir.Process(interleaved, frames, channels)) return;

        Frame = _choir.Frame;
        Haptic = _mapper.Map(Frame);
        if (Haptic.Pulse) _pulsePending = true;
    }

    private void Update()
    {
        if (Sink == null) return;
        var h = Haptic;
        h.Pulse = _pulsePending;
        _pulsePending = false;
        Sink.Send(h);
    }

    private void OnGUI()
    {
        if (!showHud || _choir == null) return;
        var voices = _choir.Voices;
        GUILayout.BeginArea(new Rect(10, 10, 360, 260), GUI.skin.box);
        for (int i = 0; i < voices.Length; i++)
        {
            var v = voices[i];
            string note = v.Voiced ? NoteName(v.PitchHz) : "--";
            GUILayout.Label($"Voice {i + 1}: {note,-5} {v.PitchHz,6:0.0} Hz  level {v.Rms:0.000}");
        }
        var f = Frame;
        GUILayout.Label($"Roughness {f.Roughness:0.00}   Loudness {f.Loudness:0.000}   Voiced {f.VoicedCount}");
        GUILayout.Label($"Haptic intensity {Haptic.Intensity:0.00}");
        GUILayout.HorizontalSlider(Haptic.Intensity, 0f, 1f);
        GUILayout.EndArea();
    }

    private static readonly string[] Names = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

    private static string NoteName(float hz)
    {
        int midi = (int)Math.Round(69 + 12 * Math.Log(hz / 440.0, 2));
        return Names[((midi % 12) + 12) % 12] + (midi / 12 - 1);
    }
}
