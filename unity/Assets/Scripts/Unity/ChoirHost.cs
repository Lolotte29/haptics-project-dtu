using System;
using Haptics.Dsp;
using UnityEngine;

/// <summary>
/// Owns the analysis pipeline (<see cref="Choir"/>). Any input component calls
/// <see cref="Push"/> with interleaved audio (voice i reads channel i). Shows a debug / sound-check HUD with
/// per-channel gain trim (set by hand before the concert). Gains are saved in PlayerPrefs between runs.
/// </summary>
public class ChoirHost : MonoBehaviour
{
    private const string GainKey = "choir.gainDb.";

    [SerializeField] private int voiceCount = 4;
    [SerializeField] private bool showHud = true;
    [Tooltip("HUD size multiplier. 0 = automatic from screen height; try 2-3 if the text is still small.")]
    [SerializeField] private float hudScale = 0f;

    [Tooltip("Starting trim per channel in dB (a value saved from the HUD overrides it). Extra entries are ignored.")]
    [SerializeField] private float[] gainDb = new float[4];
    [Tooltip("Channel RMS level that fills the HUD level bar. Typical sung level is about half of it.")]
    [SerializeField] private float meterFullScaleRms = 0.2f;

    private Choir _choir;

    public VoiceFrame[] Voices => _choir?.Voices;

    /// <summary>Spectral features of the summed signal (or of the single source). Default frame before Awake.</summary>
    public FeatureFrame Mix => _choir != null ? _choir.Mix : default;

    /// <summary>Plug in chord recognition before Awake finishes or any time later; used in multi-source mode only.</summary>
    public IHarmonyAnalyzer HarmonyAnalyzer
    {
        get => _choir?.HarmonyAnalyzer;
        set { if (_choir != null) _choir.HarmonyAnalyzer = value; }
    }

    private void Awake()
    {
        _choir = new Choir(AudioSettings.outputSampleRate, voiceCount);
        ApplyGains();
    }

    private void ApplyGains()
    {
        for (int i = 0; i < voiceCount; i++)
        {
            float db = i < gainDb.Length ? gainDb[i] : 0f;
            _choir.SetGainDb(i, PlayerPrefs.GetFloat(GainKey + i, db));
        }
    }

    private void SaveGains()
    {
        for (int i = 0; i < _choir.VoiceCount; i++) PlayerPrefs.SetFloat(GainKey + i, _choir.GetGainDb(i));
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Rebuilds the pipeline for a different sample rate (e.g. a capture device that ignored the requested rate).
    /// Call before the first <see cref="Push"/> of that input, from the thread that will push.
    /// </summary>
    public void Configure(int sampleRate)
    {
        var old = _choir;
        _choir = new Choir(sampleRate, voiceCount);
        if (old != null)
            for (int i = 0; i < voiceCount; i++) _choir.SetGain(i, old.GetGain(i)); // keep the trim set in the HUD
        else
            ApplyGains();
    }

    /// <summary>Called by an input component: from the audio thread (clip playback) or the main thread (microphone).</summary>
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
        GUILayout.BeginArea(new Rect(10, 10, 520, 120 + 26 * voices.Length), GUI.skin.box);
        for (int i = 0; i < voices.Length; i++)
        {
            var v = voices[i];
            float peak = _choir.GetPeak(i);
            bool clip = peak >= 0.99f;
            string note = v.Voiced ? NoteName(v.PitchHz) : "--";
            GUILayout.BeginHorizontal();
            GUILayout.Label($"V{i + 1} {note,-4} {v.PitchHz,6:0.0} Hz", GUILayout.Width(140));
            DrawBar(_choir.GetLevel(i) / meterFullScaleRms, clip ? Color.red : Color.green, 120f);
            GUILayout.Label(clip ? "CLIP" : $"{peak:0.00}", GUILayout.Width(40));
            if (GUILayout.Button("-", GUILayout.Width(26))) _choir.SetGainDb(i, _choir.GetGainDb(i) - 1f);
            GUILayout.Label($"{_choir.GetGainDb(i):+0.0;-0.0;0.0} dB", GUILayout.Width(65));
            if (GUILayout.Button("+", GUILayout.Width(26))) _choir.SetGainDb(i, _choir.GetGainDb(i) + 1f);
            GUILayout.EndHorizontal();
        }

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Save gains")) SaveGains();
        if (GUILayout.Button("Reset gains"))
            for (int i = 0; i < _choir.VoiceCount; i++) _choir.SetGainDb(i, 0f);
        GUILayout.EndHorizontal();

        GUILayout.Label($"Voiced {_choir.VoicedCount}/{voices.Length}   Mode {_choir.Mode}");
        GUILayout.Label($"{(_choir.Mode == ChoirMode.SingleSource ? "Source" : "Mix")}: centroid {_choir.Mix.SpectralCentroid:0} Hz   level {_choir.Mix.Rms:0.000}");
        var h = _choir.Harmony;
        if (h.RootPitchClass >= 0)
            GUILayout.Label($"Chord root {Names[h.RootPitchClass]} ({h.Confidence:0.00})");
        GUILayout.EndArea();
    }

    private static void DrawBar(float fraction, Color color, float width)
    {
        Rect r = GUILayoutUtility.GetRect(width, 16f, GUILayout.Width(width));
        GUI.Box(r, GUIContent.none);
        var old = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(fraction), r.height), Texture2D.whiteTexture);
        GUI.color = old;
    }

    private static readonly string[] Names = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

    private static string NoteName(float hz)
    {
        int midi = (int)Math.Round(69 + 12 * Math.Log(hz / 440.0, 2));
        return Names[((midi % 12) + 12) % 12] + (midi / 12 - 1);
    }
}
