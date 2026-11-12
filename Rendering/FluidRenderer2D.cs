using System.Numerics;
using FluidSimulationByAI.Simulation;
using OpenTK.Graphics.OpenGL4;
using Raylib_cs;
using PixelFormat = OpenTK.Graphics.OpenGL4.PixelFormat;

namespace FluidSimulationByAI.Rendering;

/// <summary>Maps the 2D world (y up, centred box) to screen pixels (y down).</summary>
public readonly struct View2D
{
    public readonly Vector2 Origin;   // screen position of world (0,0)
    public readonly float Ppu;        // pixels per world unit

    public View2D(Vector2 origin, float ppu) { Origin = origin; Ppu = ppu; }

    public Vector2 WorldToScreen(Vector2 w) => new(Origin.X + w.X * Ppu, Origin.Y - w.Y * Ppu);
    public Vector2 ScreenToWorld(Vector2 s) => new((s.X - Origin.X) / Ppu, (Origin.Y - s.Y) / Ppu);

    public Matrix4x4 Projection(float screenW, float screenH)
    {
        float left = -Origin.X / Ppu, right = (screenW - Origin.X) / Ppu;
        float top = Origin.Y / Ppu, bottom = (Origin.Y - screenH) / Ppu;
        return Raymath.MatrixOrtho(left, right, bottom, top, -1, 1);
    }
}

public sealed class FluidRenderer2D : IDisposable
{
    private readonly ShaderProgram _points = ShaderProgram.FromFiles("points2d.vert", "points2d.frag");
    private readonly ShaderProgram _blob = ShaderProgram.FromFiles("points2d.vert", "blob2d.frag");
    private readonly ShaderProgram _liquid = ShaderProgram.FromFiles("fullscreen.vert", "liquid2d.frag");
    private readonly ParticleVao _vao = new();
    private readonly int _emptyVao = GL.GenVertexArray();
    private readonly RenderTarget _field = new(PixelInternalFormat.Rgba16f, PixelFormat.Rgba, withDepth: false, linear: true);

    private const float LiquidThreshold = 0.6f;

    public void Draw(GpuFluidSim sim, View2D view, SimSettings s, RenderSettings rs, GradientTextures gradients,
        int screenW, int screenH, int renderW, int renderH, Rectangle clip)
    {
        if (sim.Count == 0) return;

        Rlgl.DrawRenderBatchActive(); // keep Raylib's queued 2D shapes underneath
        float spacing = s.RestSpacing(2);
        float dpi = renderH / (float)Math.Max(1, screenH);
        Matrix4x4 mvp = view.Projection(screenW, screenH);
        int grad = gradients[rs.Palette];

        GL.Enable(EnableCap.ProgramPointSize);
        GL.Disable(EnableCap.DepthTest);

        // Keep the fluid inside its container on screen.
        float sx = renderW / (float)Math.Max(1, screenW);
        GL.Scissor((int)(clip.X * sx), (int)((screenH - clip.Y - clip.Height) * dpi), (int)MathF.Ceiling(clip.Width * sx), (int)MathF.Ceiling(clip.Height * dpi));

        if (rs.Style2D == RenderStyle.Particles)
        {
            GL.Enable(EnableCap.ScissorTest);
            float size = MathF.Max(1.5f, spacing * 0.95f * view.Ppu * dpi * rs.ParticleScale);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            _points.Use();
            GlState.SetupShaderCommon(_points, grad, rs, 2, s.TargetDensity);
            _points.Set("uMvp", mvp);
            _points.Set("uPointSize", size);
            _vao.Draw(sim);
        }
        else
        {
            // 1. Splat soft kernels into a float field.
            _field.EnsureSize(renderW, renderH);
            _field.Bind();
            GL.ClearColor(0, 0, 0, 0);
            GL.Clear(ClearBufferMask.ColorBufferBit);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.One, BlendingFactor.One);

            float size = spacing * 3.6f * view.Ppu * dpi * rs.ParticleScale;
            _blob.Use();
            GlState.SetupShaderCommon(_blob, grad, rs, 2, s.TargetDensity);
            _blob.Set("uMvp", mvp);
            _blob.Set("uPointSize", size);
            _vao.Draw(sim);

            // 2. Threshold + shade onto the screen.
            GL.Enable(EnableCap.ScissorTest);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.Viewport(0, 0, renderW, renderH);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            _liquid.Use();
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _field.Texture);
            _liquid.Set("uField", 0);
            _liquid.Set("uTexel", new Vector2(1f / renderW, 1f / renderH));
            _liquid.Set("uThreshold", LiquidThreshold);
            GL.BindVertexArray(_emptyVao);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
        }

        GL.Disable(EnableCap.ScissorTest);
        GlState.RestoreForRaylib(renderW, renderH);
    }

    public void Dispose()
    {
        _points.Dispose();
        _blob.Dispose();
        _liquid.Dispose();
        _vao.Dispose();
        GL.DeleteVertexArray(_emptyVao);
        _field.Dispose();
    }
}
