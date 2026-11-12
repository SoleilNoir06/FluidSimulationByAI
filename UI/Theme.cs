using System.Numerics;
using Raylib_cs;

namespace FluidSimulationByAI.UI;

public static class Theme
{
    // Background
    public static readonly Color BgTop = new(14, 19, 33, 255);
    public static readonly Color BgBottom = new(5, 7, 13, 255);

    // Surfaces
    public static readonly Color Panel = new(15, 19, 31, 236);
    public static readonly Color PanelBorder = new(255, 255, 255, 20);
    public static readonly Color Surface = new(255, 255, 255, 10);
    public static readonly Color SurfaceHover = new(255, 255, 255, 20);
    public static readonly Color SurfaceActive = new(255, 255, 255, 32);
    public static readonly Color Divider = new(255, 255, 255, 14);

    // Text
    public static readonly Color Text = new(234, 238, 246, 255);
    public static readonly Color TextDim = new(152, 161, 182, 255);
    public static readonly Color TextMuted = new(98, 107, 130, 255);

    // Accents
    public static readonly Color Accent = new(92, 176, 255, 255);
    public static readonly Color Accent2 = new(126, 112, 255, 255);
    public static readonly Color ToolAdd = new(70, 214, 186, 255);
    public static readonly Color ToolAttract = new(98, 156, 255, 255);
    public static readonly Color ToolRepel = new(255, 146, 92, 255);
    public static readonly Color ToolErase = new(240, 98, 146, 255);
    public static readonly Color Good = new(88, 214, 141, 255);
    public static readonly Color Warn = new(255, 196, 87, 255);

    public const float PanelRadius = 14f;
    public const float ControlRadius = 8f;

    public static Color WithAlpha(Color c, float a) => new(c.R, c.G, c.B, (byte)Math.Clamp(a * 255f, 0f, 255f));

    public static Color Mix(Color a, Color b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new Color(
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t),
            (byte)(a.A + (b.A - a.A) * t));
    }
}

/// <summary>Drawing primitives with pixel-based corner radii.</summary>
public static class Draw
{
    private static float Roundness(Rectangle r, float radius)
    {
        float m = MathF.Min(r.Width, r.Height);
        return m <= 0 ? 0 : Math.Clamp(2f * radius / m, 0f, 1f);
    }

    public static void RoundRect(Rectangle r, float radius, Color c)
    {
        if (c.A == 0 || r.Width <= 0 || r.Height <= 0) return;
        Raylib.DrawRectangleRounded(r, Roundness(r, radius), 10, c);
    }

    public static void RoundRectLines(Rectangle r, float radius, float thickness, Color c)
    {
        if (c.A == 0 || r.Width <= 0 || r.Height <= 0) return;
        Raylib.DrawRectangleRoundedLinesEx(r, Roundness(r, radius), 10, thickness, c);
    }

    /// <summary>Soft drop shadow made of a few expanding translucent rounded rectangles.</summary>
    public static void Shadow(Rectangle r, float radius, float spread = 14f, float strength = 0.35f)
    {
        const int layers = 6;
        for (int i = layers; i >= 1; i--)
        {
            float t = i / (float)layers;
            float e = spread * t;
            var rr = new Rectangle(r.X - e, r.Y - e + spread * 0.25f, r.Width + 2 * e, r.Height + 2 * e);
            RoundRect(rr, radius + e, new Color((byte)0, (byte)0, (byte)0, (byte)(strength * 255 * (1 - t) / layers * 2.2f)));
        }
    }

    /// <summary>Filled triangle with automatic winding (Raylib culls clockwise triangles).</summary>
    public static void Triangle(Vector2 a, Vector2 b, Vector2 c, Color col)
    {
        float cross = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
        if (cross > 0) (b, c) = (c, b);
        Raylib.DrawTriangle(a, b, c, col);
    }

    public static Rectangle Inset(Rectangle r, float d) => new(r.X + d, r.Y + d, r.Width - 2 * d, r.Height - 2 * d);

    public static bool Contains(Rectangle r, Vector2 p) =>
        p.X >= r.X && p.X < r.X + r.Width && p.Y >= r.Y && p.Y < r.Y + r.Height;
}
