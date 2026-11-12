using System.Numerics;
using Raylib_cs;

namespace FluidSimulationByAI.UI;

public enum Weight { Regular, Medium, SemiBold, Bold }

/// <summary>
/// Inter font family, rasterised lazily at each exact pixel size used (crisp text, no scaling blur).
/// </summary>
public static class Fonts
{
    private static readonly Dictionary<(Weight, int), Font> Cache = new();
    private static int[]? _codepoints;

    private static string FileFor(Weight w) => w switch
    {
        Weight.Medium => "Inter-Medium.ttf",
        Weight.SemiBold => "Inter-SemiBold.ttf",
        Weight.Bold => "Inter-Bold.ttf",
        _ => "Inter-Regular.ttf",
    };

    private static int[] Codepoints()
    {
        if (_codepoints != null) return _codepoints;
        var list = new List<int>();
        for (int c = 32; c <= 126; c++) list.Add(c);
        for (int c = 160; c <= 255; c++) list.Add(c);
        list.AddRange(new[] { 0x0152, 0x0153, 0x2013, 0x2014, 0x2019, 0x201C, 0x201D, 0x2022, 0x2026, 0x2190, 0x2191, 0x2192, 0x2193, 0x2194, 0x2009, 0x202F });
        return _codepoints = list.ToArray();
    }

    public static Font Get(Weight w, int size)
    {
        if (Cache.TryGetValue((w, size), out var f)) return f;
        string path = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", FileFor(w));
        if (File.Exists(path))
        {
            var cps = Codepoints();
            f = Raylib.LoadFontEx(path, size, cps, cps.Length);
            Raylib.SetTextureFilter(f.Texture, TextureFilter.Bilinear);
        }
        else
        {
            f = Raylib.GetFontDefault();
        }
        Cache[(w, size)] = f;
        return f;
    }

    public static void Draw(string text, Vector2 pos, int size, Color color, Weight w = Weight.Regular)
    {
        var p = new Vector2(MathF.Round(pos.X), MathF.Round(pos.Y));
        Raylib.DrawTextEx(Get(w, size), text, p, size, 0, color);
    }

    public static Vector2 Measure(string text, int size, Weight w = Weight.Regular) =>
        Raylib.MeasureTextEx(Get(w, size), text, size, 0);

    /// <summary>Draws text vertically centred in a rectangle, aligned left / centre / right.</summary>
    public static void DrawIn(string text, Rectangle r, int size, Color color, Weight w = Weight.Regular, int align = 0)
    {
        var m = Measure(text, size, w);
        float x = align switch
        {
            1 => r.X + (r.Width - m.X) * 0.5f,
            2 => r.X + r.Width - m.X,
            _ => r.X,
        };
        Draw(text, new Vector2(x, r.Y + (r.Height - m.Y) * 0.5f), size, color, w);
    }

    public static void UnloadAll()
    {
        var def = Raylib.GetFontDefault();
        foreach (var f in Cache.Values)
            if (f.Texture.Id != def.Texture.Id) Raylib.UnloadFont(f);
        Cache.Clear();
    }
}
