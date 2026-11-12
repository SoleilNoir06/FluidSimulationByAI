using OpenTK.Graphics.OpenGL4;

namespace FluidSimulationByAI.Rendering;

/// <summary>Off-screen colour target (optionally with a depth buffer) that follows the window size.</summary>
public sealed class RenderTarget : IDisposable
{
    public int Fbo { get; private set; }
    public int Texture { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }

    private int _depthRb;
    private readonly PixelInternalFormat _internal;
    private readonly PixelFormat _format;
    private readonly bool _withDepth;
    private readonly bool _linear;

    public RenderTarget(PixelInternalFormat internalFormat, PixelFormat format, bool withDepth, bool linear)
    {
        _internal = internalFormat;
        _format = format;
        _withDepth = withDepth;
        _linear = linear;
    }

    public void EnsureSize(int w, int h)
    {
        if (w == Width && h == Height && Fbo != 0) return;
        Release();
        Width = Math.Max(1, w);
        Height = Math.Max(1, h);

        Texture = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, Texture);
        GL.TexImage2D(TextureTarget.Texture2D, 0, _internal, Width, Height, 0, _format, PixelType.Float, IntPtr.Zero);
        var filter = _linear ? (int)TextureMinFilter.Linear : (int)TextureMinFilter.Nearest;
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, filter);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, filter);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        GL.BindTexture(TextureTarget.Texture2D, 0);

        Fbo = GL.GenFramebuffer();
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, Fbo);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, Texture, 0);
        if (_withDepth)
        {
            _depthRb = GL.GenRenderbuffer();
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _depthRb);
            GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.DepthComponent24, Width, Height);
            GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, RenderbufferTarget.Renderbuffer, _depthRb);
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0);
        }
        var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (status != FramebufferErrorCode.FramebufferComplete)
            Console.Error.WriteLine($"[RenderTarget] incomplete framebuffer: {status}");
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    public void Bind()
    {
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, Fbo);
        GL.Viewport(0, 0, Width, Height);
    }

    private void Release()
    {
        if (Fbo != 0) GL.DeleteFramebuffer(Fbo);
        if (Texture != 0) GL.DeleteTexture(Texture);
        if (_depthRb != 0) GL.DeleteRenderbuffer(_depthRb);
        Fbo = Texture = _depthRb = 0;
    }

    public void Dispose() => Release();
}
