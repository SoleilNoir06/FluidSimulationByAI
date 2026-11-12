using System.Numerics;
using System.Runtime.InteropServices;
using OpenTK;
using OpenTK.Graphics.OpenGL4;

namespace FluidSimulationByAI.Rendering;

/// <summary>
/// Lets OpenTK resolve OpenGL entry points from the context that Raylib (GLFW) already created.
/// </summary>
public sealed class WglBindingsContext : IBindingsContext
{
    [DllImport("opengl32.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
    private static extern IntPtr wglGetProcAddress(string name);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
    private static extern IntPtr GetProcAddress(IntPtr module, string name);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandleW(string name);

    private readonly IntPtr _opengl32 = GetModuleHandleW("opengl32.dll");

    public IntPtr GetProcAddress(string procName)
    {
        IntPtr addr = wglGetProcAddress(procName);
        // wglGetProcAddress may return small sentinel values instead of NULL on failure.
        long v = addr.ToInt64();
        if (v is 0 or 1 or 2 or 3 or -1)
            addr = GetProcAddress(_opengl32, procName);
        return addr;
    }
}

/// <summary>A linked GL program with a cached uniform-location lookup.</summary>
public sealed class ShaderProgram : IDisposable
{
    public int Handle { get; }
    public string Name { get; }
    private readonly Dictionary<string, int> _locations = new();

    private ShaderProgram(int handle, string name)
    {
        Handle = handle;
        Name = name;
    }

    public static string ShaderDir => Path.Combine(AppContext.BaseDirectory, "Shaders");

    /// <summary>Reads a shader file, expanding <c>#include "file"</c> lines (resolved next to the file).</summary>
    public static string ReadSource(string relativePath)
    {
        string full = Path.Combine(ShaderDir, relativePath);
        string dir = Path.GetDirectoryName(relativePath) ?? "";
        var sb = new System.Text.StringBuilder();
        foreach (string line in File.ReadAllLines(full))
        {
            string t = line.Trim();
            if (t.StartsWith("#include"))
            {
                string inc = t["#include".Length..].Trim().Trim('"');
                sb.AppendLine(ReadSource(Path.Combine(dir, inc)));
            }
            else sb.AppendLine(line);
        }
        return sb.ToString();
    }

    /// <summary>Compute program assembled from a header (version, defines, shared code) and a body file.</summary>
    public static ShaderProgram Compute(string name, string fullSource)
    {
        int cs = CompileStage(ShaderType.ComputeShader, fullSource, name);
        return Link(name, cs);
    }

    public static ShaderProgram Graphics(string name, string vertSource, string fragSource)
    {
        int vs = CompileStage(ShaderType.VertexShader, vertSource, name + ".vert");
        int fs = CompileStage(ShaderType.FragmentShader, fragSource, name + ".frag");
        return Link(name, vs, fs);
    }

    /// <summary>Loads Shaders/Render/{vert} and Shaders/Render/{frag}.</summary>
    public static ShaderProgram FromFiles(string vert, string frag) =>
        Graphics($"{vert}+{frag}", ReadSource(Path.Combine("Render", vert)), ReadSource(Path.Combine("Render", frag)));

    private static int CompileStage(ShaderType type, string source, string label)
    {
        int s = GL.CreateShader(type);
        GL.ShaderSource(s, source);
        GL.CompileShader(s);
        GL.GetShader(s, ShaderParameter.CompileStatus, out int ok);
        if (ok == 0)
        {
            string log = GL.GetShaderInfoLog(s);
            GL.DeleteShader(s);
            throw new InvalidOperationException($"Shader compile error in '{label}':\n{log}");
        }
        return s;
    }

    private static ShaderProgram Link(string name, params int[] stages)
    {
        int p = GL.CreateProgram();
        foreach (int s in stages) GL.AttachShader(p, s);
        GL.LinkProgram(p);
        GL.GetProgram(p, GetProgramParameterName.LinkStatus, out int ok);
        foreach (int s in stages)
        {
            GL.DetachShader(p, s);
            GL.DeleteShader(s);
        }
        if (ok == 0)
        {
            string log = GL.GetProgramInfoLog(p);
            GL.DeleteProgram(p);
            throw new InvalidOperationException($"Shader link error in '{name}':\n{log}");
        }
        return new ShaderProgram(p, name);
    }

    public void Use() => GL.UseProgram(Handle);

    private int Loc(string name)
    {
        if (!_locations.TryGetValue(name, out int loc))
        {
            loc = GL.GetUniformLocation(Handle, name);
            _locations[name] = loc;
        }
        return loc;
    }

    // Setters silently ignore uniforms optimised away by the driver.
    public void Set(string n, int v) { int l = Loc(n); if (l >= 0) GL.Uniform1(l, v); }
    public void Set(string n, float v) { int l = Loc(n); if (l >= 0) GL.Uniform1(l, v); }
    public void Set(string n, Vector2 v) { int l = Loc(n); if (l >= 0) GL.Uniform2(l, v.X, v.Y); }
    public void Set(string n, Vector3 v) { int l = Loc(n); if (l >= 0) GL.Uniform3(l, v.X, v.Y, v.Z); }
    public void Set(string n, Vector4 v) { int l = Loc(n); if (l >= 0) GL.Uniform4(l, v.X, v.Y, v.Z, v.W); }
    public void Set(string n, int x, int y, int z) { int l = Loc(n); if (l >= 0) GL.Uniform3(l, x, y, z); }

    /// <summary>
    /// Uploads a raymath-style matrix (Raylib-cs Matrix4x4 holds the mathematical matrix row by row),
    /// hence transpose = true so GLSL sees the usual column-vector convention (M * v).
    /// </summary>
    public unsafe void Set(string n, Matrix4x4 m)
    {
        int l = Loc(n);
        if (l < 0) return;
        GL.UniformMatrix4(l, 1, true, (float*)&m);
    }

    public void Dispose() => GL.DeleteProgram(Handle);
}

/// <summary>Small helpers for GPU buffers.</summary>
public static class GpuBuffer
{
    public static int Create(int bytes)
    {
        int b = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, b);
        // Zero-filled so freshly spawned slots never contain NaNs.
        GL.BufferData(BufferTarget.ShaderStorageBuffer, bytes, new byte[Math.Max(bytes, 4)], BufferUsageHint.DynamicDraw);
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
        return b;
    }

    public static void Upload<T>(int buffer, int elementOffset, T[] data, int count) where T : unmanaged
    {
        if (count <= 0) return;
        int size = Marshal.SizeOf<T>();
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, buffer);
        GL.BufferSubData(BufferTarget.ShaderStorageBuffer, (IntPtr)(elementOffset * size), count * size, data);
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
    }

    public static void Download<T>(int buffer, T[] into, int count) where T : unmanaged
    {
        if (count <= 0) return;
        GL.MemoryBarrier(MemoryBarrierFlags.BufferUpdateBarrierBit);
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, buffer);
        GL.GetBufferSubData(BufferTarget.ShaderStorageBuffer, IntPtr.Zero, count * Marshal.SizeOf<T>(), into);
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
    }

    public static void Bind(int index, int buffer) =>
        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, index, buffer);
}
