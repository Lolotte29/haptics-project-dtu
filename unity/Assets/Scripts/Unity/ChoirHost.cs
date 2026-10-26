using System;
using Haptics.Dsp;
using UnityEngine;

/// <summary>
/// Owns the analysis pipeline (<see cref="Choir"/>). Any input component calls
/// <see cref="Push"/> with interleaved audio (voice i reads channel i). Shows a debug HUD.
/// </summary>
public class ChoirHost : MonoBehaviour
{
    [SerializeField] private int voiceCount = 4;
    [SerializeField] private bool showHud = true;
    [Tooltip("HUD size multiplier. 0 = automatic from screen height; try 2-3 if the text is still small.")]
    [SerializeField] private float hudScale = 0f;

    private Choir _choir;

    public VoiceFrame[] Voices => _choir?.Voices;

    private void Awake()
    {
        _choir = new Choir(AudioSettings.outputSampleRate, voiceCount);
    }

    /// <summary>Called from the audio thread by an input component.</summary>
    public void Push(float[] interleaved, int frames, int channels)
    {
        if (_choir == null) return;
        _choir.Process(interleaved, frames, channels);
    }

    private void OnGUI()
    {
        if (!showHud || _choir == null) return;
        // IMGUI is sized in pixels, so scale it up on high-resolution screens.
        float scale = hudScale > 0f ? hudScale : Mathf.Max(1f, Screen.height / 540f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

        var voices = _choir.Voices;
        GUILayout.BeginArea(new Rect(10, 10, 360, 160), GUI.skin.box);
        for (int i = 0; i < voices.Length; i++)
        {
            var v = voices[i];
            string note = v.Voiced ? NoteName(v.PitchHz) : "--";
            GUILayout.Label($"Voice {i + 1}: {note,-5} {v.PitchHz,6:0.0} Hz  level {v.Rms:0.000}");
        }
        GUILayout.Label($"Voiced {_choir.VoicedCount}/{voices.Length}");
        GUILayout.EndArea();
    }

    private static readonly string[] Names = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

    private static string NoteName(float hz)
    {
        int midi = (int)Math.Round(69 + 12 * Math.Log(hz / 440.0, 2));
        return Names[((midi % 12) + 12) % 12] + (midi / 12 - 1);
    }
}
