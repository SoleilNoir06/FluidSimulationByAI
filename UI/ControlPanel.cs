using System.Numerics;
using FluidSimulationByAI.App;
using FluidSimulationByAI.Rendering;
using FluidSimulationByAI.Simulation;
using Raylib_cs;

namespace FluidSimulationByAI.UI;

/// <summary>Left-hand control panel (scrollable, collapsible sections).</summary>
public sealed class ControlPanel
{
    private readonly FluidApp _app;
    private readonly Dictionary<string, bool> _open = new()
    {
        ["tools"] = true, ["scene"] = true, ["physics"] = true, ["render"] = true,
    };

    private float _scroll, _scrollTarget;
    private float _contentHeight = 4000; // measured on the first frame

    private const float Pad = 18f;
    private const float Gap = 10f;

    public ControlPanel(FluidApp app) => _app = app;

    public void ScrollTo(float y) => _scroll = _scrollTarget = y;

    public void Draw(int screenW, int screenH)
    {
        float maxH = screenH - 2 * FluidApp.Margin;
        float h = MathF.Min(_contentHeight, maxH);
        var panel = new Rectangle(FluidApp.Margin, FluidApp.Margin, FluidApp.PanelWidth, h);
        Gui.Block(panel);

        Draw_Shadow(panel);
        UI.Draw.RoundRect(panel, Theme.PanelRadius, Theme.Panel);
        UI.Draw.RoundRectLines(panel, Theme.PanelRadius, 1f, Theme.PanelBorder);

        // Scrolling
        float overflow = MathF.Max(0, _contentHeight - maxH);
        if (UI.Draw.Contains(panel, Gui.Mouse) && Gui.Wheel != 0)
            _scrollTarget -= Gui.Wheel * 60f;
        _scrollTarget = Math.Clamp(_scrollTarget, 0, overflow);
        _scroll += (_scrollTarget - _scroll) * (1f - MathF.Exp(-Gui.Dt * 16f));

        Raylib.BeginScissorMode((int)panel.X, (int)panel.Y + 1, (int)panel.Width, (int)panel.Height - 2);
        float x = panel.X + Pad;
        float w = panel.Width - 2 * Pad;
        float y = panel.Y + Pad - _scroll;
        float top = y;

        y = Header(x, y, w);
        y = Transport(x, y, w);
        y = Section("tools", "Tools", x, y, w, Tools);
        y = Section("scene", "Scene", x, y, w, ScenePart);
        y = Section("physics", "Physics", x, y, w, Physics);
        y = Section("render", "Rendering", x, y, w, RenderPart);

        Fonts.Draw("Right-click a slider to reset it", new Vector2(x, y + 2), 11, Theme.TextMuted);
        y += 18;
        _contentHeight = y - top + Pad * 1.3f;
        Raylib.EndScissorMode();

        // Scroll indicator
        if (overflow > 1)
        {
            float ratio = h / _contentHeight;
            float barH = MathF.Max(30, (h - 16) * ratio);
            float barY = panel.Y + 8 + (h - 16 - barH) * (_scroll / overflow);
            UI.Draw.RoundRect(new Rectangle(panel.X + panel.Width - 6, barY, 3, barH), 1.5f, Theme.WithAlpha(Theme.Text, 0.15f));
        }
    }

    private static void Draw_Shadow(Rectangle r) => UI.Draw.Shadow(r, Theme.PanelRadius, 18, 0.45f);

    // ------------------------------------------------------------------

    private float Header(float x, float y, float w)
    {
        // Logo: small gradient drop
        var c = new Vector2(x + 14, y + 16);
        Raylib.DrawCircleGradient(c, 15, Theme.Accent, Theme.Accent2);
        Raylib.DrawCircleV(c + new Vector2(-4, -4), 4, Theme.WithAlpha(Theme.Text, 0.55f));

        Fonts.Draw("Fluid Lab", new Vector2(x + 38, y), 20, Theme.Text, Weight.Bold);
        Fonts.Draw("Real-time SPH simulation · GPU", new Vector2(x + 38, y + 24), 12, Theme.TextMuted);

        if (Gui.Button("help", new Rectangle(x + w - 30, y + 2, 30, 30), "", Icon.Help, tooltip: "Keyboard shortcuts (F1)"))
            _app.HelpVisible = !_app.HelpVisible;
        y += 50;

        int dim = Gui.Segmented("dim", new Rectangle(x, y, w, 38), new[] { "2D", "3D" }, _app.Dim - 2,
            new[] { Icon.Square, Icon.Cube }) + 2;
        if (dim != _app.Dim) _app.SetDim(dim);
        return y + 38 + Gap;
    }

    private float Transport(float x, float y, float w)
    {
        float bh = 36;
        float small = 44;
        var play = new Rectangle(x, y, w - 2 * (small + 8), bh);
        if (Gui.Button("play", play, _app.Paused ? "Play" : "Pause", _app.Paused ? Icon.Play : Icon.Pause, primary: true,
                tooltip: "Space"))
            _app.TogglePause();
        if (Gui.Button("reset", new Rectangle(play.X + play.Width + 8, y, small, bh), "", Icon.Reset, tooltip: "Reset the scene (R)"))
            _app.ResetScene();
        if (Gui.Button("step", new Rectangle(play.X + play.Width + 16 + small, y, small, bh), "", Icon.Step, tooltip: "Step one frame (→)"))
            _app.StepOnce();
        return y + bh + 8;
    }

    private float Section(string id, string title, float x, float y, float w, Func<float, float, float, float> body)
    {
        y += 6;
        Raylib.DrawRectangleRec(new Rectangle(x, y, w, 1), Theme.Divider);
        y += 8;
        bool open = _open[id];
        if (Gui.SectionHeader("sec_" + id, new Rectangle(x, y, w, 26), title, open))
            _open[id] = open = !open;
        y += 30;
        if (open) y = body(x, y, w) + 4;
        return y;
    }

    // ------------------------------------------------------------------

    private float Tools(float x, float y, float w)
    {
        (Tool t, string name, Icon icon, Color col, string key)[] tools =
        {
            (Tool.Add, "Add", Icon.Add, Theme.ToolAdd, "1"),
            (Tool.Attract, "Attract", Icon.Attract, Theme.ToolAttract, "2"),
            (Tool.Repel, "Repel", Icon.Repel, Theme.ToolRepel, "3"),
            (Tool.Erase, "Erase", Icon.Erase, Theme.ToolErase, "4"),
        };
        float tw = (w - 3 * 8) / 4f;
        for (int i = 0; i < tools.Length; i++)
        {
            var r = new Rectangle(x + i * (tw + 8), y, tw, 64);
            if (Gui.ToolTile("tool" + i, r, tools[i].name, tools[i].icon, tools[i].col, _app.Tool == tools[i].t, tools[i].key))
                _app.Tool = tools[i].t;
        }
        y += 64 + 14;

        int d = _app.Dim - 2;
        bool is2D = _app.Dim == 2;
        y = SliderRow("brushR", y, x, w, "Brush radius", ref _app.BrushRadius[d], is2D ? 0.3f : 0.15f, is2D ? 5f : 2f,
            "0.00", is2D ? 1.6f : 0.7f, tip: is2D ? "Scroll over the simulation" : "Shift + scroll over the simulation");

        if (_app.Tool is Tool.Attract or Tool.Repel)
            y = SliderRow("brushF", y, x, w, "Force", ref _app.BrushForce[d], 5f, is2D ? 300f : 150f, "0", is2D ? 90f : 45f,
                tip: "Strength of the attraction / repulsion");
        else if (_app.Tool == Tool.Add)
            y = SliderRow("spawn", y, x, w, "Flow rate", ref _app.SpawnRate, 0.1f, 4f, "0.0", 1f, unit: "×",
                tip: "Particles added per second");

        string hint = is2D ? "Right click: inverse action" : "Right drag: orbit the camera";
        Fonts.Draw(hint, new Vector2(x, y - 2), 11, Theme.TextMuted);
        return y + 16;
    }

    private float ScenePart(float x, float y, float w)
    {
        int d = _app.Dim - 2;
        float cw = (w - 8) / 2f;
        for (int i = 0; i < Scenes.All.Length; i++)
        {
            var (kind, name) = Scenes.All[i];
            int col = i % 2, row = i / 2;
            var r = new Rectangle(x + col * (cw + 8), y + row * 40, cw, 34);
            bool sel = _app.Scene[d] == kind;
            if (Chip("scene" + i, r, name, sel)) _app.LoadScene(_app.Dim, kind);
        }
        int rows = (Scenes.All.Length + 1) / 2;
        // Clear button sits in the free slot of the last row when the count is odd.
        y += rows * 40 + 4;

        // Capacity meter
        var sim = _app.Sim;
        float fill = sim.Count / (float)sim.Capacity;
        string txt = $"{FluidApp.FormatInt(sim.Count)} / {FluidApp.FormatInt(sim.Capacity)}";
        Fonts.Draw("Particles", new Vector2(x, y), 13, Theme.TextDim, Weight.Medium);
        var tm = Fonts.Measure(txt, 13, Weight.SemiBold);
        Fonts.Draw(txt, new Vector2(x + w - tm.X, y), 13, Theme.Text, Weight.SemiBold);
        y += 22;
        var bar = new Rectangle(x, y, w, 5);
        UI.Draw.RoundRect(bar, 2.5f, Theme.WithAlpha(Theme.Text, 0.08f));
        float a = Gui.Anim("capfill", fill, 10f);
        if (a > 0.002f)
            UI.Draw.RoundRect(new Rectangle(x, y, MathF.Max(5, w * a), 5), 2.5f, fill > 0.9f ? Theme.Warn : Theme.Accent);
        return y + 16;
    }

    private bool Chip(string id, Rectangle r, string label, bool selected)
    {
        float s = Gui.Anim(id + "#sel", selected ? 1 : 0, 16f);
        bool clicked = Gui.Button(id, r, label);
        if (s > 0.01f)
        {
            UI.Draw.RoundRect(r, Theme.ControlRadius, Theme.WithAlpha(Theme.Accent, 0.14f * s));
            UI.Draw.RoundRectLines(r, Theme.ControlRadius, 1.2f, Theme.WithAlpha(Theme.Accent, 0.7f * s));
            Fonts.DrawIn(label, r, 14, Theme.WithAlpha(Theme.Text, s), Weight.Medium, 1);
        }
        return clicked;
    }

    private float Physics(float x, float y, float w)
    {
        var s = _app.S;
        var def = SimSettings.Default(_app.Dim);
        bool is2D = _app.Dim == 2;

        y = SliderRow("grav", y, x, w, "Gravity", ref s.Gravity, -15f, 40f, "0.0", def.Gravity,
            tip: "Downward acceleration (negative = upward)");
        y = SliderRow("visc", y, x, w, "Viscosity", ref s.Viscosity, is2D ? 0.002f : 0.0001f, is2D ? 1.5f : 0.05f, is2D ? "0.000" : "0.0000",
            def.Viscosity, log: true, tip: "Resistance to flow: water → honey");
        y = SliderRow("press", y, x, w, "Stiffness", ref s.PressureMultiplier, def.PressureMultiplier * 0.1f, def.PressureMultiplier * 4f, "0",
            def.PressureMultiplier, log: true, tip: "Resistance to compression");
        y = SliderRow("near", y, x, w, "Near pressure", ref s.NearPressureMultiplier, def.NearPressureMultiplier * 0.1f, def.NearPressureMultiplier * 5f,
            is2D ? "0.0" : "0.00", def.NearPressureMultiplier, log: true, tip: "Keeps particles from clumping (surface tension)");
        y = SliderRow("damp", y, x, w, "Wall bounciness", ref s.CollisionDamping, 0f, 1f, "0.00", def.CollisionDamping,
            tip: "Energy kept when hitting the container");
        y = SliderRow("time", y, x, w, "Simulation speed", ref s.TimeScale, 0.1f, 2f, "0.00", def.TimeScale, unit: "×",
            tip: "Slow motion / fast forward");
        int sub = s.Substeps;
        var r = new Rectangle(x, y, w, 34);
        Gui.SliderInt("subs", r, "Substeps per frame", ref sub, 1, 8, def.Substeps, "More = more stable but more expensive");
        s.Substeps = sub;
        y += 34 + Gap;

        if (Gui.Button("physreset", new Rectangle(x, y, w, 32), "Restore defaults", Icon.Reset))
            _app.ResetPhysics();
        return y + 32 + 6;
    }

    private float RenderPart(float x, float y, float w)
    {
        var rs = _app.Render;
        int dim = _app.Dim;

        Fonts.Draw("Style", new Vector2(x, y), 13, Theme.TextDim, Weight.Medium);
        y += 20;
        int style = Gui.Segmented("style" + dim, new Rectangle(x, y, w, 34), new[] { "Particles", "Liquid" }, (int)rs.Style(dim));
        rs.SetStyle(dim, (RenderStyle)style);
        y += 34 + 14;

        Fonts.Draw("Palette", new Vector2(x, y), 13, Theme.TextDim, Weight.Medium);
        string pname = ColorGradient.Presets[rs.Palette].Name;
        var pm = Fonts.Measure(pname, 13, Weight.SemiBold);
        Fonts.Draw(pname, new Vector2(x + w - pm.X, y), 13, Theme.Text, Weight.SemiBold);
        y += 22;
        int perRow = 3;
        float sw = (w - (perRow - 1) * 8) / perRow;
        for (int i = 0; i < ColorGradient.Presets.Length; i++)
        {
            var r = new Rectangle(x + (i % perRow) * (sw + 8), y + (i / perRow) * 34, sw, 28);
            if (Gui.GradientSwatch("pal" + i, r, ColorGradient.Presets[i], rs.Palette == i)) rs.Palette = i;
        }
        y += ((ColorGradient.Presets.Length + perRow - 1) / perRow) * 34 + 10;

        Fonts.Draw("Colour by", new Vector2(x, y), 13, Theme.TextDim, Weight.Medium);
        y += 20;
        int cm = Gui.Segmented("cmode", new Rectangle(x, y, w, 34), new[] { "Speed", "Density" }, (int)rs.ColorMode);
        rs.ColorMode = (ColorMode)cm;
        y += 34 + 14;

        if (rs.ColorMode == ColorMode.Speed)
        {
            if (dim == 2)
                y = SliderRow("smax2", y, x, w, "Speed scale", ref rs.SpeedMax2D, 1f, 25f, "0.0", 7f, tip: "Speed mapped to the end of the gradient");
            else
                y = SliderRow("smax3", y, x, w, "Speed scale", ref rs.SpeedMax3D, 0.5f, 12f, "0.0", 3.5f, tip: "Speed mapped to the end of the gradient");
        }
        y = SliderRow("psize", y, x, w, "Particle size", ref rs.ParticleScale, 0.4f, 2.5f, "0.00", 1f, unit: "×");

        var tr = new Rectangle(x, y, w, 28);
        Gui.Toggle("bounds", tr, dim == 2 ? "Show outline" : "Show container", ref rs.ShowBounds);
        y += 32;
        if (dim == 3)
        {
            Gui.Toggle("autorot", new Rectangle(x, y, w, 28), "Auto-rotate", ref rs.AutoRotate);
            y += 32;
        }
        return y;
    }

    private static float SliderRow(string id, float y, float x, float w, string label, ref float value, float min, float max,
        string fmt, float def, bool log = false, string? tip = null, string unit = "")
    {
        Gui.Slider(id, new Rectangle(x, y, w, 34), label, ref value, min, max, fmt, def, log, tip, unit);
        return y + 34 + Gap;
    }
}
