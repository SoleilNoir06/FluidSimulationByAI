using System.Numerics;
using FluidSimulationByAI.Rendering;
using OpenTK.Graphics.OpenGL4;

namespace FluidSimulationByAI.Simulation;

/// <summary>
/// GPU SPH solver (OpenGL 4.3 compute shaders) for 2D or 3D.
///
/// Every sub-step:
///   1. external   – gravity + brush force, predicted positions
///   2. clear/hash – count particles per grid cell (cell size = smoothing radius)
///   3. scan       – prefix sum of counts → start index of each cell
///   4. scatter/reorder – counting sort: particle data reordered by cell (ping-pong buffers)
///   5. density    – density and near-density from neighbouring cells only
///   6. pressure   – pressure, near pressure and viscosity → new velocities
///   7. integrate  – positions, box collisions
/// The grid makes the neighbour search O(N) instead of O(N²).
/// </summary>
public sealed class GpuFluidSim : IDisposable
{
    private const int Group = 256;
    private const int ScanBlock = 1024;
    private const int MaxCells = ScanBlock * ScanBlock;

    public int Dim { get; }
    public int Capacity { get; }
    public int Count { get; private set; }

    /// <summary>Size of the container, centred on the origin (Z ignored in 2D).</summary>
    public Vector3 BoundsSize { get; set; }

    // Particle state (ping-pong for the counting sort)
    private readonly int[] _pos = new int[2], _vel = new int[2], _pred = new int[2];
    private int _cur;
    private readonly int _dens, _velTmp, _keys, _local, _perm;
    private int _counts, _starts, _blocks;
    private int _cellCapacity;

    private int _numCells;
    private (int X, int Y, int Z) _gridDim;

    private readonly ShaderProgram _external, _clear, _hash, _scanBlock, _scanSums, _scanAdd,
        _scatter, _reorder, _density, _pressure, _integrate;
    private readonly ShaderProgram[] _all;

    // CPU scratch used by spawn/erase
    private Vector4[] _scratchPos = Array.Empty<Vector4>();
    private Vector4[] _scratchVel = Array.Empty<Vector4>();

    public int PositionBuffer => _pos[_cur];
    public int VelocityBuffer => _vel[_cur];
    public int DensityBuffer => _dens;

    public GpuFluidSim(int dim, int capacity, Vector3 boundsSize)
    {
        Dim = dim;
        Capacity = capacity;
        BoundsSize = boundsSize;

        string common = ShaderProgram.ReadSource(Path.Combine("Sim", "common.glsl"));
        ShaderProgram Load(string file)
        {
            string body = ShaderProgram.ReadSource(Path.Combine("Sim", file));
            string src = $"#version 430 core\n#define DIM {dim}\n{common}\n#line 1\n{body}";
            return ShaderProgram.Compute($"{file} (DIM={dim})", src);
        }

        _external = Load("external.comp");
        _clear = Load("clear.comp");
        _hash = Load("hash.comp");
        _scanBlock = Load("scan_block.comp");
        _scanSums = Load("scan_sums.comp");
        _scanAdd = Load("scan_add.comp");
        _scatter = Load("scatter.comp");
        _reorder = Load("reorder.comp");
        _density = Load("density.comp");
        _pressure = Load("pressure.comp");
        _integrate = Load("integrate.comp");
        _all = new[] { _external, _clear, _hash, _scanBlock, _scanSums, _scanAdd, _scatter, _reorder, _density, _pressure, _integrate };

        for (int k = 0; k < 2; k++)
        {
            _pos[k] = GpuBuffer.Create(capacity * 16);
            _vel[k] = GpuBuffer.Create(capacity * 16);
            _pred[k] = GpuBuffer.Create(capacity * 16);
        }
        _dens = GpuBuffer.Create(capacity * 8);
        _velTmp = GpuBuffer.Create(capacity * 16);
        _keys = GpuBuffer.Create(capacity * 4);
        _local = GpuBuffer.Create(capacity * 4);
        _perm = GpuBuffer.Create(capacity * 4);
        _blocks = GpuBuffer.Create(ScanBlock * 4);
    }

    // ------------------------------------------------------------------
    // Particle management
    // ------------------------------------------------------------------

    public void Clear() => Count = 0;

    /// <summary>Appends particles (clamped to capacity). Returns how many were added.</summary>
    public int Add(ReadOnlySpan<Vector4> positions, ReadOnlySpan<Vector4> velocities, float restDensity)
    {
        int n = Math.Min(positions.Length, Capacity - Count);
        if (n <= 0) return 0;

        EnsureScratch(n);
        positions[..n].CopyTo(_scratchPos);
        velocities[..n].CopyTo(_scratchVel);
        GpuBuffer.Upload(_pos[_cur], Count, _scratchPos, n);
        GpuBuffer.Upload(_vel[_cur], Count, _scratchVel, n);

        // Plausible densities so colouring is sensible even while paused.
        var dens = new Vector2[n];
        Array.Fill(dens, new Vector2(restDensity, restDensity));
        GpuBuffer.Upload(_dens, Count, dens, n);

        Count += n;
        return n;
    }

    /// <summary>Removes every particle inside a sphere (circle in 2D). Returns how many were removed.</summary>
    public int RemoveInside(Vector3 centre, float radius)
    {
        if (Count == 0) return 0;
        EnsureScratch(Count);
        GpuBuffer.Download(_pos[_cur], _scratchPos, Count);
        GpuBuffer.Download(_vel[_cur], _scratchVel, Count);

        float r2 = radius * radius;
        int w = 0;
        for (int i = 0; i < Count; i++)
        {
            var p = _scratchPos[i];
            float dx = p.X - centre.X, dy = p.Y - centre.Y, dz = Dim == 3 ? p.Z - centre.Z : 0f;
            if (dx * dx + dy * dy + dz * dz < r2) continue;
            _scratchPos[w] = p;
            _scratchVel[w] = _scratchVel[i];
            w++;
        }

        int removed = Count - w;
        if (removed > 0)
        {
            GpuBuffer.Upload(_pos[_cur], 0, _scratchPos, w);
            GpuBuffer.Upload(_vel[_cur], 0, _scratchVel, w);
            Count = w;
        }
        return removed;
    }

    private void EnsureScratch(int n)
    {
        if (_scratchPos.Length >= n) return;
        int size = Math.Max(n, Math.Min(Capacity, Math.Max(1024, _scratchPos.Length * 2)));
        _scratchPos = new Vector4[size];
        _scratchVel = new Vector4[size];
    }

    // ------------------------------------------------------------------
    // Simulation
    // ------------------------------------------------------------------

    private void UpdateGrid(float h)
    {
        int gx = Math.Max(1, (int)MathF.Ceiling(BoundsSize.X / h));
        int gy = Math.Max(1, (int)MathF.Ceiling(BoundsSize.Y / h));
        int gz = Dim == 3 ? Math.Max(1, (int)MathF.Ceiling(BoundsSize.Z / h)) : 1;

        // A two-level scan handles up to 1M cells; the UI keeps h large enough to stay below that.
        if ((long)gx * gy * gz > MaxCells)
            throw new InvalidOperationException($"Grid too large ({gx}x{gy}x{gz}); increase the smoothing radius.");

        _gridDim = (gx, gy, gz);
        _numCells = gx * gy * gz;

        if (_numCells > _cellCapacity)
        {
            if (_cellCapacity > 0)
            {
                GL.DeleteBuffer(_counts);
                GL.DeleteBuffer(_starts);
            }
            _cellCapacity = Math.Max(_numCells, _cellCapacity * 3 / 2);
            _counts = GpuBuffer.Create(_cellCapacity * 4);
            _starts = GpuBuffer.Create(_cellCapacity * 4);
        }
    }

    private void SetCommon(ShaderProgram p, SimSettings s, float dt, in Interaction ia, float effectiveH)
    {
        float h = effectiveH;
        float pi = MathF.PI;
        p.Use();
        p.Set("uNumParticles", Count);
        p.Set("uDt", dt);
        p.Set("uBoundsSize", Dim == 2 ? new Vector3(BoundsSize.X, BoundsSize.Y, 0f) : BoundsSize);
        p.Set("uH", h);
        p.Set("uTargetDensity", s.TargetDensity);
        p.Set("uPressureMult", s.PressureMultiplier);
        p.Set("uNearPressureMult", s.NearPressureMultiplier);
        p.Set("uViscosity", s.Viscosity);
        p.Set("uGravity", s.Gravity);
        p.Set("uCollisionDamping", s.CollisionDamping);
        p.Set("uGridDim", _gridDim.X, _gridDim.Y, _gridDim.Z);
        p.Set("uNumCells", _numCells);
        p.Set("uInteractPos", ia.Position);
        p.Set("uInteractRadius", ia.Radius);
        p.Set("uInteractStrength", ia.Strength);

        if (Dim == 2)
        {
            p.Set("uKSpikyPow2", 6f / (pi * MathF.Pow(h, 4)));
            p.Set("uKSpikyPow3", 10f / (pi * MathF.Pow(h, 5)));
            p.Set("uKSpikyPow2Deriv", 12f / (pi * MathF.Pow(h, 4)));
            p.Set("uKSpikyPow3Deriv", 30f / (pi * MathF.Pow(h, 5)));
            p.Set("uKPoly6", 4f / (pi * MathF.Pow(h, 8)));
        }
        else
        {
            p.Set("uKSpikyPow2", 15f / (2f * pi * MathF.Pow(h, 5)));
            p.Set("uKSpikyPow3", 15f / (pi * MathF.Pow(h, 6)));
            p.Set("uKSpikyPow2Deriv", 15f / (pi * MathF.Pow(h, 5)));
            p.Set("uKSpikyPow3Deriv", 45f / (pi * MathF.Pow(h, 6)));
            p.Set("uKPoly6", 315f / (64f * pi * MathF.Pow(h, 9)));
        }
    }

    private static int Groups(int n, int size = Group) => (n + size - 1) / size;

    private static void Barrier() => GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit);

    /// <summary>Advances the simulation by one frame (split into <see cref="SimSettings.Substeps"/>).</summary>
    public void Step(SimSettings s, in Interaction ia, float frameDt)
    {
        if (Count == 0) return;

        int substeps = Math.Max(1, s.Substeps);
        float dt = frameDt * s.TimeScale / substeps;
        UpdateGrid(s.SmoothingRadius);
        float h = s.SmoothingRadius;
        int pGroups = Groups(Count);
        int cGroups = Groups(_numCells);
        int numBlocks = Groups(_numCells, ScanBlock);

        foreach (var prog in _all) SetCommon(prog, s, dt, ia, h);

        for (int step = 0; step < substeps; step++)
        {
            int c = _cur, n = 1 - _cur;

            _external.Use();
            GpuBuffer.Bind(0, _pos[c]); GpuBuffer.Bind(1, _vel[c]); GpuBuffer.Bind(2, _pred[c]);
            GL.DispatchCompute(pGroups, 1, 1);

            _clear.Use();
            GpuBuffer.Bind(0, _counts);
            GL.DispatchCompute(cGroups, 1, 1);
            Barrier();

            _hash.Use();
            GpuBuffer.Bind(0, _pred[c]); GpuBuffer.Bind(1, _keys); GpuBuffer.Bind(2, _local); GpuBuffer.Bind(3, _counts);
            GL.DispatchCompute(pGroups, 1, 1);
            Barrier();

            _scanBlock.Use();
            GpuBuffer.Bind(0, _counts); GpuBuffer.Bind(1, _starts); GpuBuffer.Bind(2, _blocks);
            GL.DispatchCompute(numBlocks, 1, 1);
            Barrier();

            _scanSums.Use();
            _scanSums.Set("uNumBlocks", numBlocks);
            GpuBuffer.Bind(0, _blocks);
            GL.DispatchCompute(1, 1, 1);
            Barrier();

            _scanAdd.Use();
            GpuBuffer.Bind(0, _starts); GpuBuffer.Bind(1, _blocks);
            GL.DispatchCompute(cGroups, 1, 1);
            Barrier();

            _scatter.Use();
            GpuBuffer.Bind(0, _keys); GpuBuffer.Bind(1, _local); GpuBuffer.Bind(2, _starts); GpuBuffer.Bind(3, _perm);
            GL.DispatchCompute(pGroups, 1, 1);
            Barrier();

            _reorder.Use();
            GpuBuffer.Bind(0, _perm);
            GpuBuffer.Bind(1, _pos[c]); GpuBuffer.Bind(2, _vel[c]); GpuBuffer.Bind(3, _pred[c]);
            GpuBuffer.Bind(4, _pos[n]); GpuBuffer.Bind(5, _vel[n]); GpuBuffer.Bind(6, _pred[n]);
            GL.DispatchCompute(pGroups, 1, 1);
            Barrier();
            _cur = n;

            _density.Use();
            GpuBuffer.Bind(0, _pred[n]); GpuBuffer.Bind(1, _dens); GpuBuffer.Bind(2, _starts); GpuBuffer.Bind(3, _counts);
            GL.DispatchCompute(pGroups, 1, 1);
            Barrier();

            _pressure.Use();
            GpuBuffer.Bind(0, _pred[n]); GpuBuffer.Bind(1, _vel[n]); GpuBuffer.Bind(2, _dens);
            GpuBuffer.Bind(3, _starts); GpuBuffer.Bind(4, _counts); GpuBuffer.Bind(5, _velTmp);
            GL.DispatchCompute(pGroups, 1, 1);
            Barrier();

            _integrate.Use();
            GpuBuffer.Bind(0, _pos[n]); GpuBuffer.Bind(1, _vel[n]); GpuBuffer.Bind(2, _velTmp);
            GL.DispatchCompute(pGroups, 1, 1);
            Barrier();
        }

        GL.MemoryBarrier(MemoryBarrierFlags.VertexAttribArrayBarrierBit | MemoryBarrierFlags.ShaderStorageBarrierBit);
        GL.UseProgram(0);
    }

    public void Dispose()
    {
        foreach (var p in _all) p.Dispose();
        GL.DeleteBuffers(2, _pos);
        GL.DeleteBuffers(2, _vel);
        GL.DeleteBuffers(2, _pred);
        GL.DeleteBuffer(_dens);
        GL.DeleteBuffer(_velTmp);
        GL.DeleteBuffer(_keys);
        GL.DeleteBuffer(_local);
        GL.DeleteBuffer(_perm);
        GL.DeleteBuffer(_blocks);
        if (_cellCapacity > 0)
        {
            GL.DeleteBuffer(_counts);
            GL.DeleteBuffer(_starts);
        }
    }
}
