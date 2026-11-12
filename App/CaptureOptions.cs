using System.Globalization;
using System.Numerics;
using FluidSimulationByAI.Rendering;
using FluidSimulationByAI.Simulation;

namespace FluidSimulationByAI.App;

/// <summary>
/// Developer option: run a fixed number of frames with a given setup, save a screenshot and quit.
/// Example: FluidLab --shot shot.png --frames 240 --dim 3 --style liquid --scene drop --poke 900 400 attract 60 200
/// </summary>
public sealed class CaptureOptions
{
    public string? Shot;
    public int Frames = 240;
    public int? Dim;
    public RenderStyle? Style;
    public SceneKind? Scene;
    public int? Palette;
    public ColorMode? ColorMode;
    public bool HideUi;
    public bool Help;
    public float? Scroll;
    public (Vector2 At, Tool Tool, int From, int To)? Poke;

    public static CaptureOptions? Parse(string[] args)
    {
        if (args.Length == 0) return null;
        var o = new CaptureOptions();
        var ci = CultureInfo.InvariantCulture;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--shot": o.Shot = args[++i]; break;
                case "--frames": o.Frames = int.Parse(args[++i], ci); break;
                case "--dim": o.Dim = int.Parse(args[++i], ci); break;
                case "--style": o.Style = args[++i] == "liquid" ? RenderStyle.Liquid : RenderStyle.Particles; break;
                case "--palette": o.Palette = int.Parse(args[++i], ci); break;
                case "--density": o.ColorMode = Rendering.ColorMode.Density; break;
                case "--hide-ui": o.HideUi = true; break;
                case "--help-overlay": o.Help = true; break;
                case "--scroll": o.Scroll = float.Parse(args[++i], ci); break;
                case "--scene":
                    o.Scene = args[++i] switch
                    {
                        "double" => SceneKind.DoubleDam,
                        "drop" => SceneKind.Drop,
                        "pool" => SceneKind.Pool,
                        "empty" => SceneKind.Empty,
                        _ => SceneKind.DamBreak,
                    };
                    break;
                case "--poke":
                {
                    float x = float.Parse(args[++i], ci), y = float.Parse(args[++i], ci);
                    var tool = args[++i] switch
                    {
                        "add" => Tool.Add,
                        "repel" => Tool.Repel,
                        "erase" => Tool.Erase,
                        _ => Tool.Attract,
                    };
                    int from = int.Parse(args[++i], ci), to = int.Parse(args[++i], ci);
                    o.Poke = (new Vector2(x, y), tool, from, to);
                    break;
                }
            }
        }
        return o;
    }

    public void Apply(FluidApp app)
    {
        if (Dim is { } d) app.SetDim(d);
        if (Style is { } s) app.Render.SetStyle(app.Dim, s);
        if (Palette is { } p) app.Render.Palette = p;
        if (ColorMode is { } c) app.Render.ColorMode = c;
        if (HideUi) app.UiVisible = false;
        if (Help) app.HelpVisible = true;
        if (Poke is { } pk) app.Tool = pk.Tool;
        if (Scene is { } sc) app.LoadScene(app.Dim, sc, quiet: true);
    }
}
