using System.Numerics;

namespace FluidSimulationByAI.Simulation;

public enum SceneKind { DamBreak, DoubleDam, Drop, Pool, Empty }

/// <summary>Builds initial particle layouts. Blocks are defined in normalised box coordinates [0,1].</summary>
public static class Scenes
{
    public static readonly (SceneKind Kind, string Name)[] All =
    {
        (SceneKind.DamBreak, "Dam break"),
        (SceneKind.DoubleDam, "Double dam"),
        (SceneKind.Drop, "Drop"),
        (SceneKind.Pool, "Pool"),
        (SceneKind.Empty, "Empty"),
    };

    public static void Build(SceneKind kind, int dim, Vector3 bounds, float spacing, List<Vector4> outPos)
    {
        outPos.Clear();
        var rnd = new Random(1234);
        switch (kind)
        {
            case SceneKind.DamBreak:
                FillBox(dim, bounds, spacing, new Vector3(0f, 0f, 0f), new Vector3(0.38f, 0.85f, 1f), rnd, outPos);
                break;
            case SceneKind.DoubleDam:
                FillBox(dim, bounds, spacing, new Vector3(0f, 0f, 0f), new Vector3(0.24f, 0.8f, 1f), rnd, outPos);
                FillBox(dim, bounds, spacing, new Vector3(0.76f, 0f, 0f), new Vector3(1f, 0.8f, 1f), rnd, outPos);
                break;
            case SceneKind.Drop:
                FillBox(dim, bounds, spacing, new Vector3(0f, 0f, 0f), new Vector3(1f, 0.3f, 1f), rnd, outPos);
                FillSphere(dim, bounds, spacing, new Vector3(0.5f, 0.72f, 0.5f), 0.17f, rnd, outPos);
                break;
            case SceneKind.Pool:
                FillBox(dim, bounds, spacing, new Vector3(0f, 0f, 0f), new Vector3(1f, 0.45f, 1f), rnd, outPos);
                break;
            case SceneKind.Empty:
                break;
        }
    }

    private static Vector3 ToWorld(Vector3 n, Vector3 bounds) => (n - new Vector3(0.5f)) * bounds;

    private static void FillBox(int dim, Vector3 bounds, float s, Vector3 nMin, Vector3 nMax, Random rnd, List<Vector4> outPos)
    {
        float margin = s * 0.5f;
        Vector3 a = ToWorld(nMin, bounds) + new Vector3(margin);
        Vector3 b = ToWorld(nMax, bounds) - new Vector3(margin);
        int nx = Math.Max(1, (int)((b.X - a.X) / s) + 1);
        int ny = Math.Max(1, (int)((b.Y - a.Y) / s) + 1);
        int nz = dim == 3 ? Math.Max(1, (int)((b.Z - a.Z) / s) + 1) : 1;
        float jitter = s * 0.08f;

        for (int z = 0; z < nz; z++)
        for (int y = 0; y < ny; y++)
        for (int x = 0; x < nx; x++)
        {
            float px = a.X + x * s + J(rnd, jitter);
            float py = a.Y + y * s + J(rnd, jitter);
            float pz = dim == 3 ? a.Z + z * s + J(rnd, jitter) : 0f;
            outPos.Add(new Vector4(px, py, pz, 1f));
        }
    }

    private static void FillSphere(int dim, Vector3 bounds, float s, Vector3 nCentre, float nRadius, Random rnd, List<Vector4> outPos)
    {
        Vector3 c = ToWorld(nCentre, bounds);
        float r = nRadius * bounds.Y;
        int n = (int)(r / s);
        float jitter = s * 0.08f;
        int zr = dim == 3 ? n : 0;
        for (int z = -zr; z <= zr; z++)
        for (int y = -n; y <= n; y++)
        for (int x = -n; x <= n; x++)
        {
            var o = new Vector3(x, y, z) * s;
            if (o.LengthSquared() > r * r) continue;
            outPos.Add(new Vector4(c.X + o.X + J(rnd, jitter), c.Y + o.Y + J(rnd, jitter),
                dim == 3 ? c.Z + o.Z + J(rnd, jitter) : 0f, 1f));
        }
    }

    private static float J(Random r, float amount) => (float)(r.NextDouble() * 2 - 1) * amount;
}
