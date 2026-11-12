using FluidSimulationByAI.Simulation;
using OpenTK.Graphics.OpenGL4;

namespace FluidSimulationByAI.Rendering;

/// <summary>
/// Exposes the simulation's storage buffers as vertex attributes (no copy):
/// location 0 = position (vec4), 1 = velocity (vec4), 2 = density (vec2).
/// Rebound every frame because the sort ping-pongs between two buffer sets.
/// </summary>
public sealed class ParticleVao : IDisposable
{
    private readonly int _vao = GL.GenVertexArray();

    public void Bind(GpuFluidSim sim)
    {
        GL.BindVertexArray(_vao);

        GL.BindBuffer(BufferTarget.ArrayBuffer, sim.PositionBuffer);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 4, VertexAttribPointerType.Float, false, 16, 0);

        GL.BindBuffer(BufferTarget.ArrayBuffer, sim.VelocityBuffer);
        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, 16, 0);

        GL.BindBuffer(BufferTarget.ArrayBuffer, sim.DensityBuffer);
        GL.EnableVertexAttribArray(2);
        GL.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, 8, 0);

        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
    }

    public void Draw(GpuFluidSim sim)
    {
        Bind(sim);
        GL.DrawArrays(PrimitiveType.Points, 0, sim.Count);
        GL.BindVertexArray(0);
    }

    public void Dispose() => GL.DeleteVertexArray(_vao);
}

/// <summary>GL state helpers to hand the context back to Raylib in the state it expects.</summary>
public static class GlState
{
    public static void RestoreForRaylib(int renderWidth, int renderHeight)
    {
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        GL.Viewport(0, 0, renderWidth, renderHeight);
        GL.UseProgram(0);
        GL.BindVertexArray(0);
        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, 0);
        GL.Enable(EnableCap.Blend);
        GL.BlendEquation(BlendEquationMode.FuncAdd);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        GL.DepthMask(true);
        GL.Disable(EnableCap.ProgramPointSize);
    }

    public static void SetupShaderCommon(ShaderProgram p, int gradientTex, RenderSettings rs, int dim, float targetDensity)
    {
        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, gradientTex);
        p.Set("uGradient", 0);
        p.Set("uColorMode", (int)rs.ColorMode);
        p.Set("uSpeedMax", rs.SpeedMax(dim));
        p.Set("uTargetDensity", targetDensity);
    }
}
