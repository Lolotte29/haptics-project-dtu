using UnityEngine;

/// <summary>
/// One dark, readable look for every IMGUI overlay of the project (ChoirHost HUD, plots, haptic bridge controls).
/// The default Unity skin is light grey with translucent boxes, so white text on a bright camera background was unreadable.
/// Call <see cref="Apply"/> at the start of each <c>OnGUI</c>; it changes the shared <c>GUI.skin</c> once.
/// </summary>
public static class HudStyle
{
    public static readonly Color PanelColor = new Color(0.04f, 0.05f, 0.07f, 0.92f);
    public static readonly Color BarBackground = new Color(0.18f, 0.20f, 0.25f, 1f);

    private static Texture2D _panel, _button, _buttonHover, _buttonActive, _buttonOn;

    public static void Apply()
    {
        if (_panel != null) return; // already done (Unity's null check also catches a destroyed texture)

        _panel = Solid(PanelColor);
        _button = Solid(new Color(0.20f, 0.22f, 0.28f, 1f));
        _buttonHover = Solid(new Color(0.28f, 0.31f, 0.39f, 1f));
        _buttonActive = Solid(new Color(0.14f, 0.16f, 0.21f, 1f));
        _buttonOn = Solid(new Color(0.16f, 0.38f, 0.62f, 1f)); // selected toolbar button

        var skin = GUI.skin;
        Color text = new Color(0.93f, 0.95f, 0.98f, 1f);

        skin.box.normal.background = _panel;
        skin.box.normal.textColor = text;

        skin.label.normal.textColor = text;

        skin.button.normal.background = _button;
        skin.button.hover.background = _buttonHover;
        skin.button.active.background = _buttonActive;
        skin.button.onNormal.background = _buttonOn;
        skin.button.onHover.background = _buttonOn;
        skin.button.onActive.background = _buttonActive;
        foreach (var state in new[] { skin.button.normal, skin.button.hover, skin.button.active,
                                      skin.button.onNormal, skin.button.onHover, skin.button.onActive })
            state.textColor = text;

        foreach (var state in new[] { skin.toggle.normal, skin.toggle.hover, skin.toggle.active,
                                      skin.toggle.onNormal, skin.toggle.onHover, skin.toggle.onActive })
            state.textColor = text;
    }

    /// <summary>Draws a level bar: a lighter track so it shows on the dark panel, then the filled part.</summary>
    public static void DrawBar(Rect r, float fraction, Color fill)
    {
        var old = GUI.color;
        GUI.color = BarBackground;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = fill;
        GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(fraction), r.height), Texture2D.whiteTexture);
        GUI.color = old;
    }

    private static Texture2D Solid(Color c)
    {
        var t = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
        t.SetPixel(0, 0, c);
        t.Apply();
        return t;
    }
}
