namespace FluidSimulationByAI.Simulation;

/// <summary>Physical parameters of one simulation (2D and 3D each own an instance).</summary>
public sealed class SimSettings
{
    public float Gravity;
    public float SmoothingRadius;
    public float TargetDensity;
    public float PressureMultiplier;
    public float NearPressureMultiplier;
    public float Viscosity;
    public float CollisionDamping;
    public float TimeScale;
    public int Substeps;

    /// <summary>Distance between particles at rest density (used to spawn calm fluid blocks).</summary>
    public float RestSpacing(int dim) => MathF.Pow(1f / TargetDensity, 1f / dim);

    public SimSettings Clone() => (SimSettings)MemberwiseClone();

    public void CopyFrom(SimSettings o)
    {
        Gravity = o.Gravity;
        SmoothingRadius = o.SmoothingRadius;
        TargetDensity = o.TargetDensity;
        PressureMultiplier = o.PressureMultiplier;
        NearPressureMultiplier = o.NearPressureMultiplier;
        Viscosity = o.Viscosity;
        CollisionDamping = o.CollisionDamping;
        TimeScale = o.TimeScale;
        Substeps = o.Substeps;
    }

    public static SimSettings Default(int dim) => dim == 2 ? Default2D() : Default3D();

    public static SimSettings Default2D() => new()
    {
        Gravity = 12f,
        SmoothingRadius = 0.35f,
        TargetDensity = 55f,
        PressureMultiplier = 500f,
        NearPressureMultiplier = 18f,
        Viscosity = 0.06f,
        CollisionDamping = 0.95f,
        TimeScale = 1f,
        Substeps = 3,
    };

    public static SimSettings Default3D() => new()
    {
        Gravity = 10f,
        SmoothingRadius = 0.2f,
        TargetDensity = 630f,
        PressureMultiplier = 288f,
        NearPressureMultiplier = 2.15f,
        Viscosity = 0.001f,
        CollisionDamping = 0.95f,
        TimeScale = 1f,
        Substeps = 3,
    };
}

/// <summary>Mouse/brush force applied during a step.</summary>
public struct Interaction
{
    public System.Numerics.Vector3 Position;
    public float Radius;
    /// <summary>&gt; 0 attracts, &lt; 0 repels, 0 disables.</summary>
    public float Strength;

    public static Interaction None => default;
}
