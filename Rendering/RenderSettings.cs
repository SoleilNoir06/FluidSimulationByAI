namespace FluidSimulationByAI.Rendering;

public enum RenderStyle { Particles = 0, Liquid = 1 }
public enum ColorMode { Speed = 0, Density = 1 }

/// <summary>Visual options, shared by both views except where indexed by dimension.</summary>
public sealed class RenderSettings
{
    public RenderStyle Style2D = RenderStyle.Particles;
    public RenderStyle Style3D = RenderStyle.Liquid;
    public int Palette;
    public ColorMode ColorMode = ColorMode.Speed;
    public float ParticleScale = 1f;
    public float SpeedMax2D = 7f;
    public float SpeedMax3D = 3.5f;
    public bool AutoRotate;
    public bool ShowBounds = true;

    public RenderStyle Style(int dim) => dim == 2 ? Style2D : Style3D;

    public void SetStyle(int dim, RenderStyle s)
    {
        if (dim == 2) Style2D = s; else Style3D = s;
    }

    public float SpeedMax(int dim) => dim == 2 ? SpeedMax2D : SpeedMax3D;
}
