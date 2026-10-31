using System;
using Haptics.Dsp;
using UnityEngine;

/// <summary>
/// Scrolling time-series plots: one pitch plot with a coloured line per singer, and one plot per spectral feature
/// of the summed signal (the single source in mono). Add it to the same GameObject as <see cref="ChoirHost"/>.
/// Samples the latest frame once per Unity <c>Update</c>, so the time axis is display frames, not DSP frames.
/// </summary>
[RequireComponent(typeof(ChoirHost))]
public class FeatureVisualizer : MonoBehaviour
{
    [SerializeField] private bool show = true;
    [Tooltip("Samples kept per plot (one per rendered frame; about 4 s at 60 fps).")]
    [SerializeField] private int historyLength = 256;
    [SerializeField] private int panelHeight = 56;
    [Tooltip("Size multiplier. 0 = automatic from screen height.")]
    [SerializeField] private float uiScale = 0f;
    [Tooltip("Left edge of the plots in unscaled pixels; leaves room for the ChoirHost HUD.")]
    [SerializeField] private float leftOffset = 380f;

    private sealed class Panel
    {
        public string Name;
        public Func<VoiceFrame, float> GetVoice;     // per-singer panel (pitch)
        public Func<FeatureFrame, float> GetFeature; // mix panel
        public bool Signed;                // symmetric range around 0 (skewness)
        public float FixedLo, FixedHi;     // used when FixedHi > FixedLo, otherwise auto-range
        public float AutoFloor;            // smallest auto range, so silence does not blow up noise
        public float Lo, Hi;
        public int SeriesCount;
        public float[][] History;          // [series][sample]
        public Texture2D Tex;
        public Color32[] Pixels;
    }

    private static readonly Color32 Background = new Color32(20, 20, 24, 220);
    private static readonly Color32 Grid = new Color32(60, 60, 70, 255);
    private static readonly Color32 OnsetTint = new Color32(255, 220, 90, 90);
    private static readonly Color32 MixColor = new Color32(255, 255, 255, 255);

    private ChoirHost _host;
    private Panel[] _panels;
    private bool[] _onsets;
    private Color32[] _voiceColors;
    private int _head;              // next write position == oldest sample
    private int _voiceCount;
    private GUIStyle _label;

    private void Awake()
    {
        _host = GetComponent<ChoirHost>();
    }

    private void OnDestroy()
    {
        ReleaseTextures();
    }

    private void ReleaseTextures()
    {
        if (_panels == null) return;
        foreach (var p in _panels)
            if (p.Tex != null) Destroy(p.Tex);
        _panels = null;
    }

    private Panel NewPanel(string name, int n, int series, float fixedLo = 0f, float fixedHi = 0f, float autoFloor = 0f, bool signed = false)
    {
        var tex = new Texture2D(n, panelHeight, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        var panel = new Panel
        {
            Name = name, Signed = signed, FixedLo = fixedLo, FixedHi = fixedHi, AutoFloor = autoFloor,
            Lo = fixedLo, Hi = fixedHi > fixedLo ? fixedHi : autoFloor,
            SeriesCount = series, History = new float[series][],
            Tex = tex, Pixels = new Color32[n * panelHeight]
        };
        for (int s = 0; s < series; s++)
        {
            panel.History[s] = new float[n];
            for (int i = 0; i < n; i++) panel.History[s][i] = float.NaN;
        }
        return panel;
    }

    private void Init(int voiceCount)
    {
        ReleaseTextures();
        _voiceCount = voiceCount;
        int n = Mathf.Max(32, historyLength);

        _voiceColors = new Color32[voiceCount];
        for (int s = 0; s < voiceCount; s++)
            _voiceColors[s] = Color.HSVToRGB((s * 0.23f) % 1f, 0.75f, 1f);
        _onsets = new bool[n];

        _panels = new[]
        {
            // The only per-singer analysis: pitch. Everything below is computed once, on the mix.
            WithVoice(NewPanel("Pitch per singer (MIDI note)", n, voiceCount, 40f, 90f),
                f => f.PitchHz > 0f ? 69f + 12f * Mathf.Log(f.PitchHz / 440f, 2f) : float.NaN),
            WithFeature(NewPanel("RMS level", n, 1, autoFloor: 0.02f), f => f.Rms),
            WithFeature(NewPanel("Centroid (Hz)", n, 1, autoFloor: 500f), f => f.SpectralCentroid),
            WithFeature(NewPanel("Spread (Hz)", n, 1, autoFloor: 200f), f => f.SpectralSpread),
            WithFeature(NewPanel("Skewness", n, 1, autoFloor: 1f, signed: true), f => f.SpectralSkewness),
            WithFeature(NewPanel("Entropy (0..1)", n, 1, 0f, 1f), f => f.SpectralEntropy),
            WithFeature(NewPanel("Zero-crossing rate", n, 1, autoFloor: 0.05f), f => f.ZeroCrossingRate),
            WithFeature(NewPanel("Spectral flux (+ onsets)", n, 1, autoFloor: 0.05f), f => f.SpectralFlux),
        };
        _head = 0;
    }

    private static Panel WithVoice(Panel p, Func<VoiceFrame, float> get) { p.GetVoice = get; return p; }
    private static Panel WithFeature(Panel p, Func<FeatureFrame, float> get) { p.GetFeature = get; return p; }

    private void Update()
    {
        if (!show || _host == null) return;
        var voices = _host.Voices;
        if (voices == null || voices.Length == 0) return;
        if (_panels == null || voices.Length != _voiceCount) Init(voices.Length);

        int n = _onsets.Length;
        var mix = _host.Mix;
        foreach (var panel in _panels)
        {
            if (panel.GetVoice != null)
                for (int s = 0; s < panel.SeriesCount; s++)
                    panel.History[s][_head] = panel.GetVoice(voices[s]);
            else
                panel.History[0][_head] = panel.GetFeature(mix);
        }
        _onsets[_head] = mix.Onset;
        _head = (_head + 1) % n;

        for (int p = 0; p < _panels.Length; p++)
        {
            UpdateRange(_panels[p], n);
            Redraw(p, n);
        }
    }

    private static void UpdateRange(Panel panel, int n)
    {
        if (panel.FixedHi > panel.FixedLo) return;

        // Peak of what is currently on screen, with a floor so silence does not amplify noise.
        float max = panel.AutoFloor;
        for (int s = 0; s < panel.SeriesCount; s++)
        {
            var h = panel.History[s];
            for (int i = 0; i < n; i++)
            {
                float v = h[i];
                if (float.IsNaN(v)) continue;
                float a = panel.Signed ? Mathf.Abs(v) : v;
                if (a > max) max = a;
            }
        }
        panel.Hi = max;
        panel.Lo = panel.Signed ? -max : 0f;
    }

    private void Redraw(int p, int n)
    {
        var panel = _panels[p];
        var px = panel.Pixels;
        int h = panelHeight;

        for (int i = 0; i < px.Length; i++) px[i] = Background;

        DrawHLine(px, n, h, panel.Signed ? ValueToY(0f, panel) : h / 2);

        if (p == _panels.Length - 1)
        {
            for (int x = 0; x < n; x++)
            {
                if (!_onsets[(_head + x) % n]) continue;
                for (int y = 0; y < h; y++) px[y * n + x] = OnsetTint;
            }
        }

        for (int s = 0; s < panel.SeriesCount; s++)
        {
            var hist = panel.History[s];
            var color = panel.GetVoice != null ? _voiceColors[s] : MixColor;
            float prev = float.NaN;
            for (int x = 0; x < n; x++)
            {
                float v = hist[(_head + x) % n];
                if (!float.IsNaN(v))
                {
                    int y1 = ValueToY(v, panel);
                    int y0 = float.IsNaN(prev) ? y1 : ValueToY(prev, panel);
                    int lo = Mathf.Min(y0, y1), hi = Mathf.Max(y0, y1);
                    for (int y = lo; y <= hi; y++) px[y * n + x] = color;
                }
                prev = v;
            }
        }

        panel.Tex.SetPixels32(px);
        panel.Tex.Apply(false);
    }

    private int ValueToY(float v, Panel panel)
    {
        float span = panel.Hi - panel.Lo;
        float t = span > 1e-9f ? (v - panel.Lo) / span : 0f;
        return Mathf.Clamp(Mathf.RoundToInt(t * (panelHeight - 1)), 0, panelHeight - 1);
    }

    private static void DrawHLine(Color32[] px, int width, int height, int y)
    {
        y = Mathf.Clamp(y, 0, height - 1);
        for (int x = 0; x < width; x++) px[y * width + x] = Grid;
    }

    private void OnGUI()
    {
        if (!show || _panels == null) return;
        if (_label == null)
            _label = new GUIStyle(GUI.skin.label) { fontSize = 11, padding = new RectOffset(2, 2, 0, 0) };

        float scale = uiScale > 0f ? uiScale : Mathf.Max(1f, Screen.height / 540f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

        const float labelHeight = 15f;
        const int columns = 2;
        float plotW = _panels[0].Tex.width;
        float cellW = plotW + 8f;
        float cellH = panelHeight + labelHeight + 6f;

        // Legend: singer colours apply to the pitch plot only; the other plots are the mix (white).
        float lx = leftOffset;
        for (int s = 0; s < _voiceCount; s++)
        {
            _label.normal.textColor = _voiceColors[s];
            GUI.Label(new Rect(lx, 4f, 50f, labelHeight), "V" + (s + 1), _label);
            lx += 44f;
        }
        _label.normal.textColor = MixColor;
        GUI.Label(new Rect(lx, 4f, 140f, labelHeight), _voiceCount == 1 ? "Source" : "Mix (sum)", _label);

        for (int p = 0; p < _panels.Length; p++)
        {
            var panel = _panels[p];
            int col = p % columns, row = p / columns;
            float x = leftOffset + col * cellW;
            float y = 4f + labelHeight + row * cellH;

            _label.normal.textColor = Color.white;
            string range = panel.Signed ? $"+-{panel.Hi:0.##}" : $"{panel.Lo:0.##}..{panel.Hi:0.##}";
            GUI.Label(new Rect(x, y, plotW, labelHeight), $"{panel.Name}   {range}", _label);
            GUI.DrawTexture(new Rect(x, y + labelHeight, plotW, panelHeight), panel.Tex);
        }
    }
}
