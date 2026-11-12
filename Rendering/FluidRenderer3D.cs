using System.Numerics;
using FluidSimulationByAI.Simulation;
using OpenTK.Graphics.OpenGL4;
using Raylib_cs;
using PixelFormat = OpenTK.Graphics.OpenGL4.PixelFormat;
using RlColor = Raylib_cs.Color;

namespace FluidSimulationByAI.Rendering;

/// <summary>
/// 3D view: container + floor drawn with Raylib, particles drawn either as lit sphere impostors
/// or as a continuous liquid surface (screen-space fluid rendering).
/// </summary>
public sealed class FluidRenderer3D : IDisposable
{
    private readonly ShaderProgram _spheres = ShaderProgram.FromFiles("sphere.vert", "sphere.frag");
    private readonly ShaderProgram _depth = ShaderProgram.FromFiles("sphere.vert", "sphere_depth.frag");
    private readonly ShaderProgram _thick = ShaderProgram.FromFiles("sphere.vert", "sphere_thick.frag");
    private readonly ShaderProgram _blur = ShaderProgram.FromFiles("fullscreen.vert", "bilateral.frag");
    private readonly ShaderProgram _composite = ShaderProgram.FromFiles("fullscreen.vert", "fluid3d.frag");
    private readonly ParticleVao _vao = new();
    private readonly int _emptyVao = GL.GenVertexArray();

    private readonly RenderTarget _depthRt = new(PixelInternalFormat.R32f, PixelFormat.Red, withDepth: true, linear: false);
    private readonly RenderTarget _blurA = new(PixelInternalFormat.R32f, PixelFormat.Red, withDepth: false, linear: false);
    private readonly RenderTarget _blurB = new(PixelInternalFormat.R32f, PixelFormat.Red, withDepth: false, linear: false);
    private readonly RenderTarget _thickRt = new(PixelInternalFormat.Rgba16f, PixelFormat.Rgba, withDepth: false, linear: true);

    // Surface smoothing, expressed in multiples of the rest spacing between particles.
    private const float BlurRadius = 3f;
    private const float BlurDepthTolerance = 3f;
    private const int BlurIterations = 3;

    private static readonly Vector3 LightWorld = Vector3.Normalize(new Vector3(-0.35f, 0.85f, 0.4f));

    public void Draw(GpuFluidSim sim, OrbitCamera cam, SimSettings s, RenderSettings rs, GradientTextures gradients,
        int screenW, int screenH, int renderW, int renderH)
    {
        float aspect = screenW / (float)Math.Max(1, screenH);
        Matrix4x4 view = cam.View;
        Matrix4x4 proj = cam.Projection(aspect);

        Raylib.BeginMode3D(cam.ToRaylib());
        Rlgl.SetMatrixProjection(proj);
        Rlgl.SetMatrixModelView(view);

        DrawContainer(sim.BoundsSize, rs.ShowBounds);
        Rlgl.DrawRenderBatchActive();

        if (sim.Count > 0)
        {
            float spacing = s.RestSpacing(3);
            float pointScale = renderH / cam.TanHalfFov;
            Vector3 lightView = Vector3.Normalize(OrbitCamera.TransformDir(view, LightWorld));
            int grad = gradients[rs.Palette];

            GL.Enable(EnableCap.ProgramPointSize);
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Less);
            GL.DepthMask(true);

            if (rs.Style3D == RenderStyle.Particles)
            {
                GL.Disable(EnableCap.Blend);
                SetupSpheres(_spheres, view, proj, spacing * 0.5f * rs.ParticleScale, pointScale, grad, rs, s);
                _spheres.Set("uLightDir", lightView);
                _vao.Draw(sim);
            }
            else
            {
                DrawLiquid(sim, cam, view, proj, aspect, spacing, pointScale, lightView, grad, rs, s, renderW, renderH);
            }
        }

        GlState.RestoreForRaylib(renderW, renderH);
        GL.Enable(EnableCap.DepthTest); // still inside Raylib's 3D mode
        Raylib.EndMode3D();
    }

    private void SetupSpheres(ShaderProgram p, Matrix4x4 view, Matrix4x4 proj, float radius, float pointScale,
        int grad, RenderSettings rs, SimSettings s)
    {
        p.Use();
        GlState.SetupShaderCommon(p, grad, rs, 3, s.TargetDensity);
        p.Set("uView", view);
        p.Set("uProj", proj);
        p.Set("uRadius", radius);
        p.Set("uPointScale", pointScale);
    }

    private void DrawLiquid(GpuFluidSim sim, OrbitCamera cam, Matrix4x4 view, Matrix4x4 proj, float aspect,
        float spacing, float pointScale, Vector3 lightView, int grad, RenderSettings rs, SimSettings s,
        int renderW, int renderH)
    {
        _depthRt.EnsureSize(renderW, renderH);
        _blurA.EnsureSize(renderW, renderH);
        _blurB.EnsureSize(renderW, renderH);
        _thickRt.EnsureSize(renderW, renderH);

        float scale = rs.ParticleScale;

        // 1. Nearest surface depth.
        _depthRt.Bind();
        GL.Disable(EnableCap.Blend);
        GL.ClearColor(1e5f, 0, 0, 0);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        SetupSpheres(_depth, view, proj, spacing * 0.75f * scale, pointScale, grad, rs, s);
        _vao.Draw(sim);

        // 2. Thickness + colour (additive, no depth test).
        _thickRt.Bind();
        GL.ClearColor(0, 0, 0, 0);
        GL.Clear(ClearBufferMask.ColorBufferBit);
        GL.Disable(EnableCap.DepthTest);
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.One, BlendingFactor.One);
        SetupSpheres(_thick, view, proj, spacing * 1.1f * scale, pointScale, grad, rs, s);
        _vao.Draw(sim);
        GL.Disable(EnableCap.Blend);

        // 3. Bilateral smoothing of the depth (2 separable iterations).
        GL.BindVertexArray(_emptyVao);
        _blur.Use();
        _blur.Set("uDepth", 0);
        _blur.Set("uWorldRadius", spacing * BlurRadius * scale);
        _blur.Set("uPointScale", pointScale);
        _blur.Set("uFalloff", 1f / (spacing * BlurDepthTolerance * scale));
        GL.ActiveTexture(TextureUnit.Texture0);
        int src = _depthRt.Texture;
        for (int it = 0; it < BlurIterations; it++)
        {
            _blurA.Bind();
            GL.BindTexture(TextureTarget.Texture2D, src);
            _blur.Set("uDir", new Vector2(1f / renderW, 0));
            GL.DrawArrays(PrimitiveType.Triangles, 0, 3);

            _blurB.Bind();
            GL.BindTexture(TextureTarget.Texture2D, _blurA.Texture);
            _blur.Set("uDir", new Vector2(0, 1f / renderH));
            GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
            src = _blurB.Texture;
        }

        // 4. Shade onto the screen, depth-tested against the container lines.
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        GL.Viewport(0, 0, renderW, renderH);
        GL.Enable(EnableCap.DepthTest);
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        _composite.Use();
        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, src);
        GL.ActiveTexture(TextureUnit.Texture1);
        GL.BindTexture(TextureTarget.Texture2D, _thickRt.Texture);
        _composite.Set("uDepth", 0);
        _composite.Set("uThick", 1);
        _composite.Set("uTexel", new Vector2(1f / renderW, 1f / renderH));
        _composite.Set("uTanHalfFov", cam.TanHalfFov);
        _composite.Set("uAspect", aspect);
        _composite.Set("uProjShift", cam.CurrentShift);
        _composite.Set("uProj", proj);
        _composite.Set("uInvView", Raymath.MatrixInvert(view));
        _composite.Set("uLightDir", lightView);
        _composite.Set("uAbsorb", 0.55f);
        GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
        GL.BindTexture(TextureTarget.Texture2D, 0);
        GL.ActiveTexture(TextureUnit.Texture0);
    }

    private static void DrawContainer(Vector3 size, bool showBounds)
    {
        Vector3 h = size * 0.5f;
        float y = -h.Y;

        // Floor
        Raylib.DrawPlane(new Vector3(0, y - 0.002f, 0), new Vector2(size.X, size.Z), new RlColor(22, 28, 44, 255));
        var grid = new RlColor(48, 58, 84, 255);
        const float step = 0.5f;
        for (float x = -h.X; x <= h.X + 1e-3f; x += step)
            Raylib.DrawLine3D(new Vector3(x, y, -h.Z), new Vector3(x, y, h.Z), grid);
        for (float z = -h.Z; z <= h.Z + 1e-3f; z += step)
            Raylib.DrawLine3D(new Vector3(-h.X, y, z), new Vector3(h.X, y, z), grid);

        if (showBounds)
            Raylib.DrawCubeWiresV(Vector3.Zero, size, new RlColor(120, 140, 190, 150));
    }

    public void Dispose()
    {
        _spheres.Dispose();
        _depth.Dispose();
        _thick.Dispose();
        _blur.Dispose();
        _composite.Dispose();
        _vao.Dispose();
        GL.DeleteVertexArray(_emptyVao);
        _depthRt.Dispose();
        _blurA.Dispose();
        _blurB.Dispose();
        _thickRt.Dispose();
    }
}
