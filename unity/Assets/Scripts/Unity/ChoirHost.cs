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
    [Header("Display")]
    [Tooltip("Press H in the Game view to show or hide the HUD (and G for the plots).")]
    [SerializeField] private bool showHud = true;
    [Tooltip("HUD size multiplier. 0 = automatic from screen height; try 2-3 if the text is still small.")]
    [SerializeField] private float hudScale = 0f;
    [Tooltip("Replace the bright default camera background with a dark one so the HUD text is readable. Applied at Play, the scene is not changed.")]
    [SerializeField] private bool darkBackground = true;

    [Header("Voices")]

    [Tooltip("Starting trim per channel in dB (a value saved from the HUD overrides it). Extra entries are ignored.")]
    [SerializeField] private float[] gainDb = new float[4];
    [Tooltip("Run pitch tracking (YIN) on each source at start; can be toggled in the HUD. Missing entries count as on. Harmony will only use sources that are on.")]
    [SerializeField] private bool[] pitchEnabled = { true, true, true, true };
    [Tooltip("Lowest pitch YIN searches for, per voice, in Hz (same order as the channels; missing entries = 100). Bass needs about 60-65 " +
             "(D2 is 73 Hz), tenor about 100, alto about 150, soprano about 200. A lower value costs more CPU and makes octave errors " +
             "more likely, so raise it for the higher voices. Applied at Play.")]
    [SerializeField] private float[] pitchMinHz = { 65f, 65f, 65f, 65f };
    [Tooltip("Highest pitch YIN searches for, per voice, in Hz (missing entries = 1100). Soprano up to about 1100, bass about 500.")]
    [SerializeField] private float[] pitchMaxHz = { 1100f, 1100f, 1100f, 1100f };
    [Tooltip("Channel RMS level that fills the HUD level bar. Typical sung level is about half of it.")]
    [SerializeField] private float meterFullScaleRms = 0.2f;

    [Header("Harmony")]
    [Tooltip("Estimate pitch classes, tuning drift and the key from the voices with YIN on (Western tonal music only). Shown in the HUD.")]
    [SerializeField] private bool estimateKey = true;

    [Header("Amplitude envelope (FeatureFrame.Envelope)")]
    [Tooltip("How fast the envelope rises, in ms (time constant). Small = punchy. 0 = instant.")]
    [Min(0f)] [SerializeField] private float envelopeAttackMs = 5f;
    [Tooltip("How fast the envelope falls, in ms (time constant). Large = smooth, no flutter. 0 = instant. Keep it small " +
             "(about 10) when the haptic bridge is used: the bridge smooths again (its own Attack/Release), so a large value here is double smoothing.")]
    [Min(0f)] [SerializeField] private float envelopeReleaseMs = 10f;

    private Choir _choir;
    private HarmonyTracker _harmony;

    public VoiceFrame[] Voices => _choir?.Voices;

    /// <summary>Spectral features of the summed signal (or of the single source). Default frame before Awake.</summary>
    public FeatureFrame Mix => _choir != null ? _choir.Mix : default;

    /// <summary>Live amplitude envelope of the mix, 0..1. Safe to read from any thread (no Unity API involved).</summary>
    public float Envelope => _choir != null ? _choir.EnvelopeLive : 0f;

    /// <summary>Latest harmony result (key, tension). Tension is 0 and the key -1 when harmony analysis is off or nothing sounds.
    /// Read from the main thread; with an input that pushes from the audio thread the fields may come from two neighbouring steps, which is harmless.</summary>
    public HarmonyFrame Harmony => _choir != null ? _choir.Harmony : default;

    /// <summary>True when a harmony analyzer is attached (<c>estimateKey</c> on, or one set through <see cref="HarmonyAnalyzer"/>).</summary>
    public bool HarmonyAvailable => _choir != null && _choir.HarmonyAnalyzer != null;

    /// <summary>Plug in chord recognition before Awake finishes or any time later; it sees only the sources whose YIN is on.</summary>
    public IHarmonyAnalyzer HarmonyAnalyzer
    {
        get => _choir?.HarmonyAnalyzer;
        set { if (_choir != null) _choir.HarmonyAnalyzer = value; }
    }

    private void Awake()
    {
        _choir = new Choir(AudioSettings.outputSampleRate, voiceCount, 512, pitchMinHz, pitchMaxHz);
        AttachHarmony();
        ApplyGains();
        ApplyPitchEnabled();
        ApplyEnvelope();
    }

    private void Start()
    {
        if (!darkBackground) return;
        var cam = Camera.main;
        if (cam == null) return;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.06f, 0.07f, 0.09f);
    }

    // Editing the fields in the Inspector while playing takes effect immediately.
    private void OnValidate() => ApplyEnvelope();

    private void AttachHarmony()
    {
        _harmony = estimateKey ? new HarmonyTracker(voiceCount) : null;
        _choir.HarmonyAnalyzer = _harmony;
    }

    private void ApplyEnvelope()
    {
        if (_choir == null) return;
        _choir.EnvelopeAttackMs = envelopeAttackMs;
        _choir.EnvelopeReleaseMs = envelopeReleaseMs;
    }

    private void ApplyGains()
    {
        for (int i = 0; i < voiceCount; i++)
        {
            float db = i < gainDb.Length ? gainDb[i] : 0f;
            _choir.SetGainDb(i, PlayerPrefs.GetFloat(GainKey + i, db));
        }
    }

    private void ApplyPitchEnabled()
    {
        for (int i = 0; i < voiceCount; i++)
            _choir.SetPitchEnabled(i, i >= pitchEnabled.Length || pitchEnabled[i]);
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
        _choir = new Choir(sampleRate, voiceCount, 512, pitchMinHz, pitchMaxHz);
        AttachHarmony();
        if (old != null)
        {
            for (int i = 0; i < voiceCount; i++)
            {
                _choir.SetGain(i, old.GetGain(i)); // keep the trim set in the HUD
                _choir.SetPitchEnabled(i, old.GetPitchEnabled(i));
            }
        }
        else
        {
            ApplyGains();
            ApplyPitchEnabled();
        }
        ApplyEnvelope();
    }

    /// <summary>Called by an input component: from the audio thread (clip playback) or the main thread (microphone).</summary>
    public void Push(float[] interleaved, int frames, int channels)
    {
        if (_choir == null) return;
        _choir.Process(interleaved, frames, channels);
    }

    /// <summary>Right edge of the HUD in screen pixels (0 when it is hidden). The plots use it to stay clear of the HUD.</summary>
    public float HudRightEdgePx { get; private set; }

    /// <summary>Hotkeys in the Game view: H hides or shows the HUD.</summary>
    private void OnGUI()
    {
        var e = Event.current;
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.H) { showHud = !showHud; e.Use(); }

        if (!showHud || _choir == null) { HudRightEdgePx = 0f; return; }
        HudStyle.Apply();
        // IMGUI is sized in pixels, so scale it up on high-resolution screens.
        float scale = hudScale > 0f ? hudScale : Mathf.Max(1f, Screen.height / 540f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

        var voices = _choir.Voices;
        const float hudWidth = 520f;
        HudRightEdgePx = (10f + hudWidth) * scale;
        GUILayout.BeginArea(new Rect(10, 10, hudWidth, 190 + 26 * voices.Length), GUI.skin.box);
        for (int i = 0; i < voices.Length; i++)
        {
            var v = voices[i];
            float peak = _choir.GetPeak(i);
            bool clip = peak >= 0.99f;
            string note = v.Voiced ? NoteName(v.PitchHz) : "--";
            GUILayout.BeginHorizontal();
            bool pitchOn = GUILayout.Toggle(_choir.GetPitchEnabled(i), "YIN", GUILayout.Width(50));
            if (pitchOn != _choir.GetPitchEnabled(i)) _choir.SetPitchEnabled(i, pitchOn);
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

        GUILayout.Label($"Voiced {_choir.VoicedCount}/{voices.Length}");
        GUILayout.Label($"{(voices.Length == 1 ? "Source" : "Mix")}: centroid {_choir.Mix.SpectralCentroid:0} Hz   level {_choir.Mix.Rms:0.000}");
        var h = _choir.Harmony;
        if (_harmony != null)
        {
            string key = h.KeyTonic >= 0
                ? $"{Names[h.KeyTonic]} {(h.KeyIsMinor ? "minor" : "major")} (confidence {h.KeyConfidence:0.00})"
                : "-- (listening)";
            GUILayout.Label($"Key {key}   tuning {_harmony.Tuning.OffsetSemitones * 100.0:+0;-0;0} cents");
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Tension {h.Tension:0.00}  (key {h.TensionKeyDistance:0.00}, dissonance {h.TensionDissonance:0.00})", GUILayout.Width(330));
            DrawBar(h.Tension, Color.Lerp(Color.green, Color.red, h.Tension), 150f);
            GUILayout.EndHorizontal();
        }
        if (h.RootPitchClass >= 0)
            GUILayout.Label($"Chord root {Names[h.RootPitchClass]} ({h.Confidence:0.00})");
        GUILayout.Label("H: hide this panel   G: plots");
        GUILayout.EndArea();
    }

    private static void DrawBar(float fraction, Color color, float width)
    {
        Rect r = GUILayoutUtility.GetRect(width, 16f, GUILayout.Width(width));
        HudStyle.DrawBar(r, fraction, color);
    }

    private static readonly string[] Names = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

    private static string NoteName(float hz)
    {
        int midi = (int)Math.Round(69 + 12 * Math.Log(hz / 440.0, 2));
        return Names[((midi % 12) + 12) % 12] + (midi / 12 - 1);
    }
}
