using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using RlColor = Raylib_cs.Color;

namespace FluidSimulationByAI.Rendering;

/// <summary>A named colour ramp, sampled on the CPU (UI) and uploaded as a 1D lookup texture (GPU).</summary>
public sealed class ColorGradient
{
    public string Name { get; }
    public (float T, Vector3 C)[] Stops { get; }

    public ColorGradient(string name, params string[] hexStops)
    {
        Name = name;
        Stops = new (float, Vector3)[hexStops.Length];
        for (int i = 0; i < hexStops.Length; i++)
            Stops[i] = (i / (float)(hexStops.Length - 1), Hex(hexStops[i]));
    }

    private static Vector3 Hex(string h)
    {
        h = h.TrimStart('#');
        return new Vector3(Convert.ToInt32(h[..2], 16), Convert.ToInt32(h[2..4], 16), Convert.ToInt32(h[4..6], 16)) / 255f;
    }

    public Vector3 Sample(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        for (int i = 1; i < Stops.Length; i++)
        {
            if (t <= Stops[i].T)
            {
                var a = Stops[i - 1];
                var b = Stops[i];
                float k = (t - a.T) / MathF.Max(1e-6f, b.T - a.T);
                k = k * k * (3 - 2 * k); // smooth blending between stops
                return Vector3.Lerp(a.C, b.C, k);
            }
        }
        return Stops[^1].C;
    }

    public RlColor SampleColor(float t, byte alpha = 255)
    {
        var c = Sample(t);
        return new RlColor((byte)(c.X * 255), (byte)(c.Y * 255), (byte)(c.Z * 255), alpha);
    }

    public static readonly ColorGradient[] Presets =
    {
        new("Spectrum", "#1b3fd6", "#1aa6e0", "#3fe0b0", "#c8f04a", "#ffcf3a", "#ff5a36"),
        new("Ocean",    "#081a4d", "#0f4fa8", "#1e90d6", "#4fd2e8", "#c9f6ff"),
        new("Magma",   "#1a0b3b", "#5b1a78", "#b8326e", "#f0703a", "#fde59a"),
        new("Neon",     "#2b0d7a", "#7a1fd1", "#e1308f", "#ff8a5c", "#59e3ff"),
        new("Aurora",   "#0b3550", "#0f7c80", "#25c29a", "#9ef07a", "#f4ffa8"),
        new("Ice",      "#1a2748", "#34579c", "#6f9ee0", "#b8d6ff", "#ffffff"),
    };
}

/// <summary>GPU lookup textures for every preset (256x1, linear filtering).</summary>
public sealed class GradientTextures : IDisposable
{
    private readonly int[] _textures;

    public GradientTextures()
    {
        _textures = new int[ColorGradient.Presets.Length];
        var pixels = new byte[256 * 4];
        for (int g = 0; g < _textures.Length; g++)
        {
            var grad = ColorGradient.Presets[g];
            for (int i = 0; i < 256; i++)
            {
                var c = grad.Sample(i / 255f);
                pixels[i * 4 + 0] = (byte)(c.X * 255);
                pixels[i * 4 + 1] = (byte)(c.Y * 255);
                pixels[i * 4 + 2] = (byte)(c.Z * 255);
                pixels[i * 4 + 3] = 255;
            }
            int tex = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, tex);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, 256, 1, 0, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            _textures[g] = tex;
        }
        GL.BindTexture(TextureTarget.Texture2D, 0);
    }

    public int this[int index] => _textures[Math.Clamp(index, 0, _textures.Length - 1)];

    public void Dispose() => GL.DeleteTextures(_textures.Length, _textures);
}
