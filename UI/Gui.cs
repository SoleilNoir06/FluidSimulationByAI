using System.Globalization;
using System.Numerics;
using FluidSimulationByAI.Rendering;
using Raylib_cs;

namespace FluidSimulationByAI.UI;

/// <summary>
/// Minimal immediate-mode GUI: widgets are drawn and handle input in the same call.
/// Hover/press transitions are smoothed with per-widget animation values.
/// </summary>
public static class Gui
{
    public static Vector2 Mouse;
    public static bool Pressed, Down, Released, RightPressed;
    public static float Wheel;
    public static float Dt;

    /// <summary>Widget currently captured by a drag (e.g. a slider).</summary>
    public static string? Active;
    private static string? _hovered;
    private static string? _hoveredPrev;
    private static float _hoverTime;

    private static readonly Dictionary<string, float> Anims = new();
    private static readonly List<Rectangle> Blockers = new();
    private static readonly List<Rectangle> BlockersPrev = new();
    private static string? _tooltip;
    private static Vector2 _tooltipAnchor;

    public static bool WantsMouse
    {
        get
        {
            if (Active != null) return true;
            foreach (var r in BlockersPrev)
                if (UI.Draw.Contains(r, Mouse)) return true;
            return false;
        }
    }

    public static void BeginFrame(float dt)
    {
        Dt = dt;
        Mouse = Raylib.GetMousePosition();
        Pressed = Raylib.IsMouseButtonPressed(MouseButton.Left);
        Down = Raylib.IsMouseButtonDown(MouseButton.Left);
        Released = Raylib.IsMouseButtonReleased(MouseButton.Left);
        RightPressed = Raylib.IsMouseButtonPressed(MouseButton.Right);
        Wheel = Raylib.GetMouseWheelMove();

        BlockersPrev.Clear();
        BlockersPrev.AddRange(Blockers);
        Blockers.Clear();

        _hoveredPrev = _hovered;
        _hovered = null;
        _tooltip = null;
    }

    public static void EndFrame()
    {
        if (!Down) Active = null;

        if (_hovered != null && _hovered == _hoveredPrev) _hoverTime += Dt;
        else _hoverTime = 0;

        Raylib.SetMouseCursor(_hovered != null || Active != null ? MouseCursor.PointingHand : MouseCursor.Default);
    }

    /// <summary>Registers an area that should swallow mouse input (panels, overlays).</summary>
    public static void Block(Rectangle r) => Blockers.Add(r);

    public static float Anim(string id, float target, float speed = 14f)
    {
        Anims.TryGetValue(id, out float v);
        v += (target - v) * (1f - MathF.Exp(-Dt * speed));
        if (MathF.Abs(target - v) < 0.001f) v = target;
        Anims[id] = v;
        return v;
    }

    public static void SetAnim(string id, float value) => Anims[id] = value;

    private static bool Hover(string id, Rectangle r, string? tooltip = null)
    {
        bool h = UI.Draw.Contains(r, Mouse) && (Active == null || Active == id);
        if (h)
        {
            _hovered = id;
            if (tooltip != null && _hoverTime > 0.55f && Active == null && _hoveredPrev == id)
            {
                _tooltip = tooltip;
                _tooltipAnchor = new Vector2(r.X + r.Width, r.Y + r.Height * 0.5f);
            }
        }
        return h;
    }

    // ------------------------------------------------------------------
    // Widgets
    // ------------------------------------------------------------------

    public static bool Button(string id, Rectangle r, string label, Icon icon = Icon.None, bool primary = false,
        Color? accent = null, string? tooltip = null)
    {
        bool hover = Hover(id, r, tooltip);
        bool clicked = hover && Pressed;
        float a = Anim(id + "#h", hover ? 1f : 0f);
        float p = Anim(id + "#p", hover && Down ? 1f : 0f, 24f);

        Color acc = accent ?? Theme.Accent;
        Color bg = primary
            ? Theme.Mix(Theme.WithAlpha(acc, 0.85f), acc, a)
            : Theme.Mix(Theme.Surface, Theme.SurfaceHover, a);
        var rr = UI.Draw.Inset(r, p * 1.0f);
        UI.Draw.RoundRect(rr, Theme.ControlRadius, bg);
        if (!primary) UI.Draw.RoundRectLines(rr, Theme.ControlRadius, 1f, Theme.WithAlpha(Theme.Text, 0.05f + 0.05f * a));

        Color fg = primary ? new Color(8, 12, 22, 255) : Theme.Mix(Theme.TextDim, Theme.Text, a);
        const int fs = 14;
        var tm = label.Length > 0 ? Fonts.Measure(label, fs, Weight.Medium) : Vector2.Zero;
        float iconW = icon != Icon.None ? 16f : 0f;
        float gap = icon != Icon.None && label.Length > 0 ? 8f : 0f;
        float total = iconW + gap + tm.X;
        float x = rr.X + (rr.Width - total) * 0.5f;
        if (icon != Icon.None) Icons.Draw(icon, new Vector2(x + 8, rr.Y + rr.Height * 0.5f), 15f, fg);
        if (label.Length > 0) Fonts.Draw(label, new Vector2(x + iconW + gap, rr.Y + (rr.Height - tm.Y) * 0.5f), fs, fg, Weight.Medium);
        return clicked;
    }

    public static int Segmented(string id, Rectangle r, string[] options, int index, Icon[]? icons = null)
    {
        UI.Draw.RoundRect(r, Theme.ControlRadius + 2, new Color(0, 0, 0, 70));
        UI.Draw.RoundRectLines(r, Theme.ControlRadius + 2, 1f, Theme.WithAlpha(Theme.Text, 0.05f));

        float w = r.Width / options.Length;
        float pos = Anim(id + "#pos", index, 16f);
        var pill = new Rectangle(r.X + 3 + pos * w, r.Y + 3, w - 6, r.Height - 6);
        UI.Draw.RoundRect(pill, Theme.ControlRadius, Theme.WithAlpha(Theme.Accent, 0.22f));
        UI.Draw.RoundRectLines(pill, Theme.ControlRadius, 1f, Theme.WithAlpha(Theme.Accent, 0.55f));

        int result = index;
        for (int i = 0; i < options.Length; i++)
        {
            var cell = new Rectangle(r.X + i * w, r.Y, w, r.Height);
            string cid = $"{id}#{i}";
            bool hover = Hover(cid, cell);
            if (hover && Pressed) result = i;
            float sel = MathF.Max(0, 1f - MathF.Abs(pos - i));
            float h = Anim(cid + "#h", hover ? 1 : 0);
            Color fg = Theme.Mix(Theme.Mix(Theme.TextDim, Theme.Text, h * 0.6f), Theme.Text, sel);

            const int fs = 14;
            var tm = Fonts.Measure(options[i], fs, Weight.SemiBold);
            bool hasIcon = icons != null && icons[i] != Icon.None;
            float total = tm.X + (hasIcon ? 24 : 0);
            float x = cell.X + (cell.Width - total) * 0.5f;
            if (hasIcon) Icons.Draw(icons![i], new Vector2(x + 8, cell.Y + cell.Height * 0.5f), 15f, fg);
            Fonts.Draw(options[i], new Vector2(x + (hasIcon ? 24 : 0), cell.Y + (cell.Height - tm.Y) * 0.5f), fs, fg, Weight.SemiBold);
        }
        return result;
    }

    /// <summary>
    /// Labelled slider. Drag to change, right-click to restore the default value.
    /// Returns true while the value changes.
    /// </summary>
    public static bool Slider(string id, Rectangle r, string label, ref float value, float min, float max,
        string format, float defaultValue, bool logarithmic = false, string? tooltip = null, string unit = "")
    {
        bool hover = Hover(id, r, tooltip);
        if (hover && Pressed) Active = id;
        if (hover && RightPressed) value = defaultValue;

        var track = new Rectangle(r.X, r.Y + r.Height - 9, r.Width, 4);
        bool changed = false;
        if (Active == id && Down)
        {
            float t = Math.Clamp((Mouse.X - track.X) / track.Width, 0f, 1f);
            float nv = FromT(t, min, max, logarithmic);
            if (Raylib.IsKeyDown(KeyboardKey.LeftShift)) nv = MathF.Round(nv / StepFor(min, max)) * StepFor(min, max);
            if (nv != value) { value = nv; changed = true; }
        }

        float h = Anim(id + "#h", hover || Active == id ? 1f : 0f);
        float tv = ToT(value, min, max, logarithmic);
        float ta = Anim(id + "#t", tv, 22f);

        Fonts.Draw(label, new Vector2(r.X, r.Y), 13, Theme.Mix(Theme.TextDim, Theme.Text, h * 0.7f), Weight.Medium);
        string vs = value.ToString(format, CultureInfo.InvariantCulture) + unit;
        var vm = Fonts.Measure(vs, 13, Weight.SemiBold);
        bool isDefault = MathF.Abs(value - defaultValue) <= MathF.Abs(defaultValue) * 1e-3f + 1e-6f;
        Fonts.Draw(vs, new Vector2(r.X + r.Width - vm.X, r.Y), 13, isDefault ? Theme.TextDim : Theme.Accent, Weight.SemiBold);

        UI.Draw.RoundRect(track, 2, Theme.WithAlpha(Theme.Text, 0.09f));
        var fill = new Rectangle(track.X, track.Y, track.Width * ta, track.Height);
        if (fill.Width > 0.5f)
            Raylib.DrawRectangleGradientEx(fill, Theme.Accent2, Theme.Accent2, Theme.Accent, Theme.Accent);

        // Default marker
        float td = ToT(defaultValue, min, max, logarithmic);
        Raylib.DrawRectangleRec(new Rectangle(track.X + track.Width * td - 0.5f, track.Y - 2, 1, track.Height + 4), Theme.WithAlpha(Theme.Text, 0.18f));

        var knob = new Vector2(track.X + track.Width * ta, track.Y + track.Height * 0.5f);
        float kr = 6f + 1.5f * h;
        Raylib.DrawCircleV(knob, kr + 3, Theme.WithAlpha(Theme.Accent, 0.18f * h));
        Raylib.DrawCircleV(knob, kr, Theme.Text);
        Raylib.DrawCircleV(knob, kr - 2.5f, Theme.Mix(Theme.Accent, Theme.Text, 0.15f));
        return changed;
    }

    public static bool SliderInt(string id, Rectangle r, string label, ref int value, int min, int max, int defaultValue, string? tooltip = null)
    {
        float v = value;
        bool c = Slider(id, r, label, ref v, min, max, "0", defaultValue, false, tooltip);
        value = (int)MathF.Round(v);
        return c;
    }

    private static float StepFor(float min, float max) => MathF.Pow(10, MathF.Floor(MathF.Log10(max - min)) - 1);

    private static float ToT(float v, float min, float max, bool log) => log
        ? Math.Clamp(MathF.Log(v / min) / MathF.Log(max / min), 0f, 1f)
        : Math.Clamp((v - min) / (max - min), 0f, 1f);

    private static float FromT(float t, float min, float max, bool log) => log
        ? min * MathF.Pow(max / min, t)
        : min + (max - min) * t;

    public static bool Toggle(string id, Rectangle r, string label, ref bool value, string? tooltip = null)
    {
        bool hover = Hover(id, r, tooltip);
        bool changed = false;
        if (hover && Pressed) { value = !value; changed = true; }
        float h = Anim(id + "#h", hover ? 1 : 0);
        float on = Anim(id + "#on", value ? 1 : 0, 16f);

        Fonts.DrawIn(label, r, 13, Theme.Mix(Theme.TextDim, Theme.Text, 0.4f + 0.6f * h), Weight.Medium);
        var sw = new Rectangle(r.X + r.Width - 36, r.Y + (r.Height - 20) * 0.5f, 36, 20);
        UI.Draw.RoundRect(sw, 10, Theme.Mix(Theme.WithAlpha(Theme.Text, 0.12f), Theme.Accent, on));
        var knob = new Vector2(sw.X + 10 + on * 16, sw.Y + 10);
        Raylib.DrawCircleV(knob, 7.5f, Theme.Text);
        return changed;
    }

    public static bool ToolTile(string id, Rectangle r, string label, Icon icon, Color accent, bool selected, string shortcut)
    {
        bool hover = Hover(id, r, $"{label}  ·  key {shortcut}");
        float h = Anim(id + "#h", hover ? 1 : 0);
        float s = Anim(id + "#s", selected ? 1 : 0, 16f);

        var bg = Theme.Mix(Theme.Mix(Theme.Surface, Theme.SurfaceHover, h), Theme.WithAlpha(accent, 0.16f), s);
        UI.Draw.RoundRect(r, Theme.ControlRadius + 2, bg);
        UI.Draw.RoundRectLines(r, Theme.ControlRadius + 2, 1.2f,
            Theme.Mix(Theme.WithAlpha(Theme.Text, 0.05f), Theme.WithAlpha(accent, 0.85f), s));

        Color fg = Theme.Mix(Theme.Mix(Theme.TextDim, Theme.Text, h), accent, s);
        Icons.Draw(icon, new Vector2(r.X + r.Width * 0.5f, r.Y + 22), 20f, fg);
        Fonts.DrawIn(label, new Rectangle(r.X, r.Y + r.Height - 24, r.Width, 16), 12, Theme.Mix(Theme.TextDim, Theme.Text, MathF.Max(h, s)), Weight.Medium, 1);
        return hover && Pressed;
    }

    public static bool SectionHeader(string id, Rectangle r, string title, bool open)
    {
        bool hover = Hover(id, r);
        float h = Anim(id + "#h", hover ? 1 : 0);
        float o = Anim(id + "#o", open ? 1 : 0, 18f);
        Color fg = Theme.Mix(Theme.TextMuted, Theme.TextDim, h);
        Fonts.DrawIn(title.ToUpperInvariant(), r, 11, fg, Weight.Bold);
        var c = new Vector2(r.X + r.Width - 8, r.Y + r.Height * 0.5f);
        Icons.Draw(o > 0.5f ? Icon.ChevronDown : Icon.ChevronRight, c, 12f, fg);
        return hover && Pressed;
    }

    public static bool GradientSwatch(string id, Rectangle r, ColorGradient g, bool selected)
    {
        bool hover = Hover(id, r, g.Name);
        float h = Anim(id + "#h", hover ? 1 : 0);
        float s = Anim(id + "#s", selected ? 1 : 0, 16f);

        var inner = UI.Draw.Inset(r, 3);
        // Gradient body built from horizontal gradient strips.
        int strips = 12;
        float sw = inner.Width / strips;
        for (int i = 0; i < strips; i++)
        {
            var c0 = g.SampleColor(i / (float)strips);
            var c1 = g.SampleColor((i + 1) / (float)strips);
            var sr = new Rectangle(inner.X + i * sw, inner.Y, sw + 0.6f, inner.Height);
            Raylib.DrawRectangleGradientEx(sr, c0, c0, c1, c1);
        }
        // Rounded mask: paint the corners with the panel colour.
        UI.Draw.RoundRectLines(UI.Draw.Inset(r, 1.5f), 7, 3f, Theme.Panel);
        UI.Draw.RoundRectLines(r, Theme.ControlRadius, 1.5f,
            Theme.Mix(Theme.WithAlpha(Theme.Text, 0.08f + 0.15f * h), Theme.Text, s));
        return hover && Pressed;
    }

    public static void Label(Rectangle r, string text, int size = 13, Color? color = null, Weight w = Weight.Regular, int align = 0) =>
        Fonts.DrawIn(text, r, size, color ?? Theme.TextDim, w, align);

    /// <summary>Draws the pending tooltip; call last so it sits on top of everything.</summary>
    public static void DrawTooltip(int screenW)
    {
        if (_tooltip == null) return;
        const int fs = 13;
        var m = Fonts.Measure(_tooltip, fs);
        float pad = 10;
        var r = new Rectangle(_tooltipAnchor.X + 14, _tooltipAnchor.Y - (m.Y + pad) * 0.5f, m.X + pad * 2, m.Y + pad);
        if (r.X + r.Width > screenW - 8) r.X = screenW - 8 - r.Width;
        UI.Draw.Shadow(r, 8, 8, 0.4f);
        UI.Draw.RoundRect(r, 8, new Color(30, 36, 54, 250));
        UI.Draw.RoundRectLines(r, 8, 1, Theme.WithAlpha(Theme.Text, 0.1f));
        Fonts.Draw(_tooltip, new Vector2(r.X + pad, r.Y + pad * 0.5f), fs, Theme.Text);
    }
}
