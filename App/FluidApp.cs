using System.Numerics;
using FluidSimulationByAI.Rendering;
using FluidSimulationByAI.Simulation;
using FluidSimulationByAI.UI;
using OpenTK.Graphics.OpenGL4;
using Raylib_cs;
using RlColor = Raylib_cs.Color;

namespace FluidSimulationByAI.App;

public enum Tool { Add, Attract, Repel, Erase }

/// <summary>Application state, main loop, input handling and frame composition.</summary>
public sealed class FluidApp : IDisposable
{
    // ---------------------------------------------------------------
    // Configuration
    // ---------------------------------------------------------------
    public const int Capacity2D = 120_000;
    public const int Capacity3D = 400_000;
    public const float WorldHeight2D = 13f;
    public static readonly Vector3 Bounds3D = new(8f, 4.5f, 4f);
    public const float Margin = 16f;
    public const float PanelWidth = 316f;

    // ---------------------------------------------------------------
    // State (read and written by the control panel)
    // ---------------------------------------------------------------
    public int Dim = 2;
    public bool Paused;
    public Tool Tool = Tool.Attract;
    public readonly SimSettings[] Settings = { SimSettings.Default2D(), SimSettings.Default3D() };
    public readonly RenderSettings Render = new();
    public readonly SceneKind[] Scene = { SceneKind.DamBreak, SceneKind.DamBreak };
    public readonly float[] BrushRadius = { 1.6f, 0.7f };
    public readonly float[] BrushForce = { 90f, 45f };
    public float SpawnRate = 1f;
    public bool UiVisible = true;
    public bool HelpVisible;

    public SimSettings S => Settings[Dim - 2];
    public GpuFluidSim Sim => Dim == 2 ? _sim2! : _sim3!;
    public float Fps { get; private set; }
    public float StepMs { get; private set; }

    private GpuFluidSim? _sim2, _sim3;
    private FluidRenderer2D? _r2;
    private FluidRenderer3D? _r3;
    private GradientTextures? _gradients;
    private readonly OrbitCamera _camera = new();
    private ControlPanel? _panel;

    private float _panelAnim = 1f;
    private Vector2 _bounds2D;
    private View2D _view2D;
    private bool _stepOnce;

    // Tool interaction
    private bool _toolActive;
    private bool _toolInverse;
    private bool _orbiting;
    private Vector3 _brushPos;
    private Vector3 _prevBrushPos;
    private bool _brushValid;
    private readonly Random _rng = new();

    // Toast
    private string _toast = "";
    private float _toastTime;

    // Optional scripted capture (see CaptureOptions)
    private readonly CaptureOptions? _capture;
    private int _frameIndex;

    public FluidApp(CaptureOptions? capture = null) => _capture = capture;

    private float _fpsAccum;
    private int _fpsFrames;
    private readonly System.Diagnostics.Stopwatch _stepWatch = new();

    // ---------------------------------------------------------------

    public void Run()
    {
        Raylib.SetTraceLogLevel(TraceLogLevel.Warning);
        Raylib.SetConfigFlags(ConfigFlags.Msaa4xHint | ConfigFlags.ResizableWindow | ConfigFlags.VSyncHint);
        Raylib.InitWindow(1600, 920, "Fluid Lab — SPH Fluid Simulation");
        Raylib.SetWindowMinSize(960, 600);
        Raylib.SetExitKey(KeyboardKey.Null);
        // Safety net when the driver ignores VSync: never render faster than the monitor.
        Raylib.SetTargetFPS(Math.Max(60, Raylib.GetMonitorRefreshRate(Raylib.GetCurrentMonitor())));

        GL.LoadBindings(new WglBindingsContext());
        if (!SupportsCompute(out string glInfo))
        {
            ShowFatal($"OpenGL compute shaders unavailable (OpenGL 4.3 or ARB extensions required). Driver: {glInfo}");
            return;
        }

        try
        {
            _gradients = new GradientTextures();
            _r2 = new FluidRenderer2D();
            _r3 = new FluidRenderer3D();
            UpdateLayout(0f, instant: true);
            _sim2 = new GpuFluidSim(2, Capacity2D, new Vector3(_bounds2D, 0f));
            _sim3 = new GpuFluidSim(3, Capacity3D, Bounds3D);
            _panel = new ControlPanel(this);
            if (_capture?.Scroll is { } sc) _panel.ScrollTo(sc);
            LoadScene(2, Scene[0], quiet: true);
            LoadScene(3, Scene[1], quiet: true);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            ShowFatal(ex.Message);
            return;
        }

        Toast("Welcome! Left click to interact · F1 for help");
        _capture?.Apply(this);

        while (!Raylib.WindowShouldClose())
        {
            Frame();
            if (_capture != null && _frameIndex >= _capture.Frames) break;
        }
    }

    private void Frame()
    {
        float dt = Raylib.GetFrameTime();
        _fpsAccum += dt; _fpsFrames++;
        if (_fpsAccum >= 0.4f) { Fps = _fpsFrames / _fpsAccum; _fpsAccum = 0; _fpsFrames = 0; }

        Gui.BeginFrame(dt);
        HandleKeyboard();
        UpdateLayout(dt);
        HandlePointer(dt);

        // ---- Simulation ----
        if (!Paused || _stepOnce)
        {
            var ia = CurrentInteraction();
            float frameDt = Math.Clamp(dt, 1f / 240f, 1f / 50f);
            if (_stepOnce || _capture != null) frameDt = 1f / 60f;
            _stepWatch.Restart();
            Sim.Step(S, ia, frameDt);
            StepMs = StepMs * 0.9f + (float)_stepWatch.Elapsed.TotalMilliseconds * 0.1f;
            _stepOnce = false;
        }

        // ---- Rendering ----
        int sw = Raylib.GetScreenWidth(), sh = Raylib.GetScreenHeight();
        int rw = Raylib.GetRenderWidth(), rh = Raylib.GetRenderHeight();

        Raylib.BeginDrawing();
        Raylib.ClearBackground(Theme.BgBottom);
        DrawBackground(sw, sh);

        if (Dim == 2)
        {
            var box = DrawContainer2D();
            _r2!.Draw(_sim2!, _view2D, S, Render, _gradients!, sw, sh, rw, rh, box);
        }
        else
        {
            _camera.Update(dt);
            _r3!.Draw(_sim3!, _camera, S, Render, _gradients!, sw, sh, rw, rh);
        }
        DrawBrush();

        // ---- Interface ----
        if (UiVisible) _panel!.Draw(sw, sh);
        DrawStatus(sw, sh);
        DrawToast(sw, dt);
        if (HelpVisible) DrawHelp(sw, sh);
        Gui.DrawTooltip(sw);
        Gui.EndFrame();

        _frameIndex++;
        if (_capture?.Shot != null && _frameIndex == _capture.Frames)
        {
            Rlgl.DrawRenderBatchActive();
            Raylib.TakeScreenshot(_capture.Shot);
        }
        Raylib.EndDrawing();
    }

    // ---------------------------------------------------------------
    // Layout
    // ---------------------------------------------------------------

    private float LeftInset => Margin + _panelAnim * (PanelWidth + Margin);

    private void UpdateLayout(float dt, bool instant = false)
    {
        float target = UiVisible ? 1f : 0f;
        _panelAnim = instant ? target : _panelAnim + (target - _panelAnim) * (1f - MathF.Exp(-dt * 12f));
        if (MathF.Abs(_panelAnim - target) < 0.001f) _panelAnim = target;

        float sw = Raylib.GetScreenWidth(), sh = Raylib.GetScreenHeight();
        var area = new Rectangle(LeftInset, Margin, sw - LeftInset - Margin, sh - 2 * Margin);

        // 2D: world height is fixed, width follows the free area.
        float ppu = area.Height / WorldHeight2D;
        _bounds2D = new Vector2(MathF.Max(2f, area.Width / ppu), WorldHeight2D);
        _view2D = new View2D(new Vector2(area.X + area.Width * 0.5f, area.Y + area.Height * 0.5f), ppu);
        if (_sim2 != null) _sim2.BoundsSize = new Vector3(_bounds2D, 0f);

        // 3D: centre the box in the free area with a lens shift.
        float centreX = area.X + area.Width * 0.5f;
        _camera.LensShift = centreX / sw * 2f - 1f;
        if (Render.AutoRotate && !_orbiting && Dim == 3) _camera.Orbit(dt * 0.12f, 0);
    }

    // ---------------------------------------------------------------
    // Input
    // ---------------------------------------------------------------

    private void HandleKeyboard()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Tab)) SetDim(Dim == 2 ? 3 : 2);
        if (Raylib.IsKeyPressed(KeyboardKey.Space)) TogglePause();
        if (Raylib.IsKeyPressed(KeyboardKey.R)) ResetScene();
        if (Raylib.IsKeyPressed(KeyboardKey.Right) || Raylib.IsKeyPressed(KeyboardKey.N)) StepOnce();
        if (Raylib.IsKeyPressed(KeyboardKey.H)) UiVisible = !UiVisible;
        if (Raylib.IsKeyPressed(KeyboardKey.F1) || Raylib.IsKeyPressed(KeyboardKey.Slash)) HelpVisible = !HelpVisible;
        if (Raylib.IsKeyPressed(KeyboardKey.Escape)) HelpVisible = false;
        if (Raylib.IsKeyPressed(KeyboardKey.F11) || Raylib.IsKeyPressed(KeyboardKey.F)) Raylib.ToggleBorderlessWindowed();
        if (Raylib.IsKeyPressed(KeyboardKey.V))
            Render.SetStyle(Dim, Render.Style(Dim) == RenderStyle.Particles ? RenderStyle.Liquid : RenderStyle.Particles);
        if (Raylib.IsKeyPressed(KeyboardKey.C)) Render.Palette = (Render.Palette + 1) % ColorGradient.Presets.Length;

        if (Raylib.IsKeyPressed(KeyboardKey.One) || Raylib.IsKeyPressed(KeyboardKey.Kp1)) Tool = Tool.Add;
        if (Raylib.IsKeyPressed(KeyboardKey.Two) || Raylib.IsKeyPressed(KeyboardKey.Kp2)) Tool = Tool.Attract;
        if (Raylib.IsKeyPressed(KeyboardKey.Three) || Raylib.IsKeyPressed(KeyboardKey.Kp3)) Tool = Tool.Repel;
        if (Raylib.IsKeyPressed(KeyboardKey.Four) || Raylib.IsKeyPressed(KeyboardKey.Kp4)) Tool = Tool.Erase;
    }

    private void HandlePointer(float dt)
    {
        bool overUi = Gui.WantsMouse || HelpVisible;
        Vector2 mouse = Gui.Mouse;
        int i = Dim - 2;

        // Brush radius: Shift + wheel (always), plain wheel in 2D.
        bool shift = Raylib.IsKeyDown(KeyboardKey.LeftShift) || Raylib.IsKeyDown(KeyboardKey.RightShift);
        if (!overUi && Gui.Wheel != 0)
        {
            if (Dim == 2 || shift)
            {
                float max = Dim == 2 ? 5f : 2f, min = Dim == 2 ? 0.3f : 0.15f;
                BrushRadius[i] = Math.Clamp(BrushRadius[i] * MathF.Pow(1.12f, Gui.Wheel), min, max);
            }
            else
            {
                _camera.Zoom(Gui.Wheel);
            }
        }

        if (_capture?.Poke is { } poke)
        {
            bool on = _frameIndex >= poke.From && _frameIndex < poke.To;
            mouse = poke.At;
            Gui.Mouse = poke.At;
            _toolActive = on;
            _toolInverse = false;
            overUi = false;
            if (!on) { _brushValid = false; }
        }

        // Start / stop tool use.
        if (Gui.Pressed && !overUi) { _toolActive = true; _toolInverse = false; }
        if (Dim == 2 && Raylib.IsMouseButtonPressed(MouseButton.Right) && !overUi) { _toolActive = true; _toolInverse = true; }
        bool held = _toolInverse ? Raylib.IsMouseButtonDown(MouseButton.Right) : Raylib.IsMouseButtonDown(MouseButton.Left);
        if (!held && _capture?.Poke == null) _toolActive = false;

        // 3D orbit with the right button.
        if (Dim == 3)
        {
            if (Raylib.IsMouseButtonPressed(MouseButton.Right) && !overUi) _orbiting = true;
            if (!Raylib.IsMouseButtonDown(MouseButton.Right)) _orbiting = false;
            if (_orbiting)
            {
                var d = Raylib.GetMouseDelta();
                _camera.Orbit(-d.X * 0.006f, d.Y * 0.006f);
            }
        }

        // Brush position in world space.
        _prevBrushPos = _brushPos;
        bool wasValid = _brushValid;
        if (Dim == 2)
        {
            var w = _view2D.ScreenToWorld(mouse);
            _brushPos = new Vector3(w, 0f);
            _brushValid = true;
        }
        else
        {
            var ray = _camera.ScreenRay(mouse, Raylib.GetScreenWidth(), Raylib.GetScreenHeight());
            Vector3 n = _camera.Forward;
            float denom = Vector3.Dot(ray.Direction, n);
            _brushValid = MathF.Abs(denom) > 1e-4f;
            if (_brushValid)
            {
                float t = Vector3.Dot(Vector3.Zero - ray.Position, n) / denom;
                var p = ray.Position + ray.Direction * t;
                _brushPos = Vector3.Clamp(p, -Bounds3D * 0.5f, Bounds3D * 0.5f);
            }
        }
        if (!wasValid) _prevBrushPos = _brushPos;

        if (!_toolActive || !_brushValid) return;

        Tool tool = EffectiveTool;
        if (tool == Tool.Add) SpawnParticles(dt);
        else if (tool == Tool.Erase) Sim.RemoveInside(_brushPos, BrushRadius[i]);
    }

    private Tool EffectiveTool => !_toolInverse ? Tool : Tool switch
    {
        Tool.Add => Tool.Erase,
        Tool.Erase => Tool.Add,
        Tool.Attract => Tool.Repel,
        _ => Tool.Attract,
    };

    private Interaction CurrentInteraction()
    {
        if (!_toolActive || !_brushValid) return Interaction.None;
        var tool = EffectiveTool;
        if (tool is not (Tool.Attract or Tool.Repel)) return Interaction.None;
        int i = Dim - 2;
        return new Interaction
        {
            Position = _brushPos,
            Radius = BrushRadius[i],
            Strength = tool == Tool.Attract ? BrushForce[i] : -BrushForce[i],
        };
    }

    private void SpawnParticles(float dt)
    {
        int i = Dim - 2;
        float r = BrushRadius[i] * 0.6f;
        float measure = Dim == 2 ? MathF.PI * r * r : 4f / 3f * MathF.PI * r * r * r;
        float perSecond = SpawnRate * measure * S.TargetDensity * 4f;
        int n = Math.Clamp((int)(perSecond * Math.Min(dt, 0.05f) + _rng.NextSingle()), 0, 4000);
        if (n == 0) return;
        if (Sim.Count >= Sim.Capacity)
        {
            Toast("Maximum capacity reached");
            return;
        }

        Vector3 vel = (_brushPos - _prevBrushPos) / MathF.Max(dt, 1e-3f) * 0.7f;
        if (vel.Length() > 25f) vel = Vector3.Normalize(vel) * 25f;

        var pos = new Vector4[n];
        var v = new Vector4[n];
        for (int k = 0; k < n; k++)
        {
            Vector3 o;
            do
            {
                o = new Vector3(_rng.NextSingle() * 2 - 1, _rng.NextSingle() * 2 - 1, Dim == 3 ? _rng.NextSingle() * 2 - 1 : 0);
            } while (o.LengthSquared() > 1f);
            var p = _brushPos + o * r;
            pos[k] = new Vector4(p, 1f);
            v[k] = new Vector4(vel, 0f);
        }
        Sim.Add(pos, v, S.TargetDensity);
    }

    // ---------------------------------------------------------------
    // Commands (also used by the panel)
    // ---------------------------------------------------------------

    public void SetDim(int dim)
    {
        if (dim == Dim) return;
        Dim = dim;
        _toolActive = false;
        Toast(dim == 2 ? "2D view" : "3D view — right drag to orbit, scroll to zoom");
    }

    public void TogglePause()
    {
        Paused = !Paused;
        Toast(Paused ? "Paused" : "Playing");
    }

    public void StepOnce()
    {
        Paused = true;
        _stepOnce = true;
    }

    public void ResetScene() => LoadScene(Dim, Scene[Dim - 2]);

    public void LoadScene(int dim, SceneKind kind, bool quiet = false)
    {
        var sim = dim == 2 ? _sim2! : _sim3!;
        var s = Settings[dim - 2];
        var list = new List<Vector4>();
        Scenes.Build(kind, dim, sim.BoundsSize, s.RestSpacing(dim), list);
        sim.Clear();
        var pos = list.ToArray();
        sim.Add(pos, new Vector4[pos.Length], s.TargetDensity);
        Scene[dim - 2] = kind;
        if (!quiet) Toast($"Scene: {Scenes.All.First(x => x.Kind == kind).Name}");
    }

    public void ClearParticles()
    {
        Sim.Clear();
        Scene[Dim - 2] = SceneKind.Empty;
    }

    public void ResetPhysics()
    {
        S.CopyFrom(SimSettings.Default(Dim));
        Toast("Physics settings restored");
    }

    public void Toast(string message)
    {
        _toast = message;
        _toastTime = 2.6f;
    }

    // ---------------------------------------------------------------
    // Drawing helpers
    // ---------------------------------------------------------------

    private static void DrawBackground(int sw, int sh)
    {
        Raylib.DrawRectangleGradientV(0, 0, sw, sh, Theme.BgTop, Theme.BgBottom);
        Raylib.DrawCircleGradient(new Vector2(sw / 2f, sh * 0.15f), sw * 0.6f, new RlColor(60, 90, 170, 26), new RlColor(0, 0, 0, 0));
    }

    private Rectangle DrawContainer2D()
    {
        var a = _view2D.WorldToScreen(new Vector2(-_bounds2D.X / 2, _bounds2D.Y / 2));
        var b = _view2D.WorldToScreen(new Vector2(_bounds2D.X / 2, -_bounds2D.Y / 2));
        var r = new Rectangle(a.X, a.Y, b.X - a.X, b.Y - a.Y);
        UI.Draw.RoundRect(r, 10, new RlColor(255, 255, 255, 7));
        if (Render.ShowBounds) UI.Draw.RoundRectLines(r, 10, 1.2f, new RlColor(150, 175, 230, 40));

        // Subtle grid
        var grid = new RlColor(255, 255, 255, 7);
        for (float x = MathF.Ceiling(-_bounds2D.X / 2); x < _bounds2D.X / 2; x += 1f)
        {
            var p = _view2D.WorldToScreen(new Vector2(x, 0));
            Raylib.DrawLineV(new Vector2(p.X, r.Y + 1), new Vector2(p.X, r.Y + r.Height - 1), grid);
        }
        for (float y = MathF.Ceiling(-_bounds2D.Y / 2); y < _bounds2D.Y / 2; y += 1f)
        {
            var p = _view2D.WorldToScreen(new Vector2(0, y));
            Raylib.DrawLineV(new Vector2(r.X + 1, p.Y), new Vector2(r.X + r.Width - 1, p.Y), grid);
        }
        return new Rectangle(r.X - 1, r.Y - 1, r.Width + 2, r.Height + 2);
    }

    private RlColor ToolColor(Tool t) => t switch
    {
        Tool.Add => Theme.ToolAdd,
        Tool.Attract => Theme.ToolAttract,
        Tool.Repel => Theme.ToolRepel,
        _ => Theme.ToolErase,
    };

    private void DrawBrush()
    {
        if (!_brushValid || HelpVisible || (!Raylib.IsCursorOnScreen() && _capture?.Poke == null) || (Gui.WantsMouse && !_toolActive) || _orbiting) return;
        int i = Dim - 2;
        var col = ToolColor(_toolActive ? EffectiveTool : Tool);
        float alpha = _toolActive ? 1f : 0.65f;

        Vector2 c;
        float radiusPx;
        if (Dim == 2)
        {
            c = Gui.Mouse;
            radiusPx = BrushRadius[i] * _view2D.Ppu;
        }
        else
        {
            int sw = Raylib.GetScreenWidth(), sh = Raylib.GetScreenHeight();
            if (!_camera.WorldToScreen(_brushPos, sw, sh, out c)) return;
            if (!_camera.WorldToScreen(_brushPos + _camera.Right * BrushRadius[i], sw, sh, out var edge)) return;
            radiusPx = Vector2.Distance(c, edge);
        }

        Raylib.DrawCircleV(c, radiusPx, Theme.WithAlpha(col, 0.06f * alpha));
        Raylib.DrawRing(c, radiusPx - 1.5f, radiusPx, 0, 360, 64, Theme.WithAlpha(col, 0.85f * alpha));
        Raylib.DrawCircleV(c, 2.5f, Theme.WithAlpha(col, alpha));
    }

    private void DrawStatus(int sw, int sh)
    {
        string fps = $"{Fps:0} FPS";
        string count = $"{FormatInt(Sim.Count)} particles";
        string mode = Dim == 2 ? "2D" : "3D";
        const int fs = 13;
        var mf = Fonts.Measure(fps, fs, Weight.SemiBold);
        var mc = Fonts.Measure(count, fs, Weight.Medium);
        var mm = Fonts.Measure(mode, fs, Weight.Bold);
        float pad = 14, gap = 18;
        float pauseW = Paused ? Fonts.Measure("PAUSED", 11, Weight.Bold).X + 18 + gap : 0;
        float w = pad * 2 + 14 + mf.X + gap + mc.X + gap + mm.X + pauseW;
        var r = new Rectangle(sw - Margin - w, Margin, w, 34);
        Gui.Block(r);
        UI.Draw.RoundRect(r, 17, new RlColor(12, 16, 28, 200));
        UI.Draw.RoundRectLines(r, 17, 1, Theme.PanelBorder);

        float x = r.X + pad;
        float cy = r.Y + r.Height / 2;
        var dot = Fps >= 55 ? Theme.Good : Fps >= 30 ? Theme.Warn : Theme.ToolErase;
        Raylib.DrawCircleV(new Vector2(x + 4, cy), 4, dot);
        x += 14;
        Fonts.Draw(fps, new Vector2(x, cy - mf.Y / 2), fs, Theme.Text, Weight.SemiBold);
        x += mf.X + gap;
        Fonts.Draw(count, new Vector2(x, cy - mc.Y / 2), fs, Theme.TextDim, Weight.Medium);
        x += mc.X + gap;
        Fonts.Draw(mode, new Vector2(x, cy - mm.Y / 2), fs, Theme.Accent, Weight.Bold);
        x += mm.X + gap;
        if (Paused)
        {
            var pm = Fonts.Measure("PAUSED", 11, Weight.Bold);
            var pr = new Rectangle(x, cy - 10, pm.X + 18, 20);
            UI.Draw.RoundRect(pr, 10, Theme.WithAlpha(Theme.Warn, 0.18f));
            Fonts.DrawIn("PAUSED", pr, 11, Theme.Warn, Weight.Bold, 1);
        }

        if (!UiVisible)
        {
            const string hint = "H: show interface   ·   F1: help";
            var hm = Fonts.Measure(hint, 12);
            Fonts.Draw(hint, new Vector2(Margin + 6, sh - Margin - hm.Y), 12, Theme.WithAlpha(Theme.TextDim, 0.7f));
        }
    }

    private void DrawToast(int sw, float dt)
    {
        if (_toastTime <= 0) return;
        _toastTime -= dt;
        float a = Math.Clamp(_toastTime / 0.4f, 0f, 1f) * Math.Clamp((2.6f - _toastTime) / 0.15f, 0f, 1f);
        const int fs = 14;
        var m = Fonts.Measure(_toast, fs, Weight.Medium);
        float centre = (LeftInset + sw - Margin) * 0.5f;
        var r = new Rectangle(centre - m.X / 2 - 18, Margin + 4 - (1 - a) * 10, m.X + 36, 36);
        UI.Draw.Shadow(r, 18, 10, 0.35f * a);
        UI.Draw.RoundRect(r, 18, Theme.WithAlpha(new RlColor(26, 32, 50, 255), 0.94f * a));
        UI.Draw.RoundRectLines(r, 18, 1, Theme.WithAlpha(Theme.Text, 0.1f * a));
        Fonts.DrawIn(_toast, r, fs, Theme.WithAlpha(Theme.Text, a), Weight.Medium, 1);
    }

    private void DrawHelp(int sw, int sh)
    {
        var full = new Rectangle(0, 0, sw, sh);
        Raylib.DrawRectangleRec(full, new RlColor(4, 6, 12, 170));
        Gui.Block(full);

        (string Key, string Desc)[] rows =
        {
            ("Left click", "Use the selected tool"),
            ("Right click (2D)", "Inverse action (attract ↔ repel, add ↔ erase)"),
            ("Right drag (3D)", "Orbit the camera"),
            ("Scroll wheel", "Brush radius (2D) · zoom (3D)"),
            ("Shift + scroll", "Brush radius (3D)"),
            ("1  2  3  4", "Add · Attract · Repel · Erase"),
            ("Tab", "Switch between 2D and 3D"),
            ("Space", "Pause / play"),
            ("→  or  N", "Step one frame"),
            ("R", "Reset the scene"),
            ("V", "Style: particles / liquid"),
            ("C", "Next colour palette"),
            ("H", "Hide / show the interface"),
            ("F  or  F11", "Fullscreen"),
        };

        float w = 620, rowH = 30, h = 96 + rows.Length * rowH + 40;
        var card = new Rectangle((sw - w) / 2, (sh - h) / 2, w, h);
        UI.Draw.Shadow(card, 16, 24, 0.5f);
        UI.Draw.RoundRect(card, 16, new RlColor(18, 23, 38, 252));
        UI.Draw.RoundRectLines(card, 16, 1, Theme.PanelBorder);

        Fonts.Draw("Shortcuts", new Vector2(card.X + 28, card.Y + 24), 22, Theme.Text, Weight.Bold);
        Fonts.Draw("Esc or F1 to close", new Vector2(card.X + 28, card.Y + 54), 13, Theme.TextMuted);

        float y = card.Y + 88;
        foreach (var (key, desc) in rows)
        {
            var km = Fonts.Measure(key, 13, Weight.SemiBold);
            var kr = new Rectangle(card.X + 28, y + 3, km.X + 16, 22);
            UI.Draw.RoundRect(kr, 6, Theme.WithAlpha(Theme.Text, 0.08f));
            Fonts.DrawIn(key, kr, 13, Theme.Text, Weight.SemiBold, 1);
            Fonts.DrawIn(desc, new Rectangle(card.X + 200, y, w - 220, rowH), 14, Theme.TextDim);
            y += rowH;
        }

        if (Gui.Pressed && !UI.Draw.Contains(card, Gui.Mouse)) HelpVisible = false;
    }

    /// <summary>
    /// Raylib creates a 3.3 core context; most drivers still expose compute shaders and SSBOs
    /// through the ARB extensions, which is all we need.
    /// </summary>
    private static bool SupportsCompute(out string info)
    {
        int major = GL.GetInteger(GetPName.MajorVersion), minor = GL.GetInteger(GetPName.MinorVersion);
        info = $"{GL.GetString(StringName.Renderer)} · OpenGL {major}.{minor}";
        if (major > 4 || (major == 4 && minor >= 3)) return true;
        var ext = new HashSet<string>();
        int n = GL.GetInteger(GetPName.NumExtensions);
        for (int i = 0; i < n; i++) ext.Add(GL.GetString(StringNameIndexed.Extensions, i));
        return ext.Contains("GL_ARB_compute_shader") && ext.Contains("GL_ARB_shader_storage_buffer_object");
    }

    public static string FormatInt(int n) => n.ToString("N0", System.Globalization.CultureInfo.InvariantCulture).Replace(',', ' ');

    private void ShowFatal(string message)
    {
        Console.Error.WriteLine("[fatal] " + message);
        int frames = 0;
        while (!Raylib.WindowShouldClose() && (_capture == null || frames++ < 30))
        {
            Raylib.BeginDrawing();
            Raylib.ClearBackground(Theme.BgBottom);
            Fonts.Draw("Unable to start the simulation", new Vector2(40, 40), 24, Theme.Text, Weight.Bold);
            Fonts.Draw(message, new Vector2(40, 84), 15, Theme.TextDim);
            Raylib.EndDrawing();
        }
        Fonts.UnloadAll();
        Raylib.CloseWindow();
    }

    public int GradientCount => ColorGradient.Presets.Length;

    public void Dispose()
    {
        _sim2?.Dispose();
        _sim3?.Dispose();
        _r2?.Dispose();
        _r3?.Dispose();
        _gradients?.Dispose();
        Fonts.UnloadAll();
        if (Raylib.IsWindowReady()) Raylib.CloseWindow();
    }
}
