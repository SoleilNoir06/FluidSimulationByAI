using System.Numerics;
using Raylib_cs;

namespace FluidSimulationByAI.Rendering;

/// <summary>
/// Smoothed orbit camera. Supports a horizontal lens shift so the scene can be centred in the
/// part of the window not covered by the side panel without distorting the perspective.
/// </summary>
public sealed class OrbitCamera
{
    public const float Near = 0.05f;
    public const float Far = 300f;

    public Vector3 Target = Vector3.Zero;
    public float Yaw = 0.62f, Pitch = 0.38f, Distance = 12.5f;
    public float FovY = 42f;
    /// <summary>NDC x-coordinate where the target appears (0 = centre of the window).</summary>
    public float LensShift;

    private float _yaw, _pitch, _dist, _shift;
    private bool _initialised;

    public float MinDistance = 3f, MaxDistance = 40f;

    public void Orbit(float dYaw, float dPitch)
    {
        Yaw += dYaw;
        Pitch = Math.Clamp(Pitch + dPitch, -0.15f, 1.5f);
    }

    public void Zoom(float wheel) => Distance = Math.Clamp(Distance * MathF.Pow(0.88f, wheel), MinDistance, MaxDistance);

    public void Update(float dt)
    {
        if (!_initialised)
        {
            _yaw = Yaw; _pitch = Pitch; _dist = Distance; _shift = LensShift;
            _initialised = true;
        }
        float k = 1f - MathF.Exp(-dt * 14f);
        _yaw += (Yaw - _yaw) * k;
        _pitch += (Pitch - _pitch) * k;
        _dist += (Distance - _dist) * k;
        _shift += (LensShift - _shift) * (1f - MathF.Exp(-dt * 10f));
    }

    public float CurrentShift => _shift;

    public Vector3 Position => Target + _dist * new Vector3(
        MathF.Cos(_pitch) * MathF.Sin(_yaw), MathF.Sin(_pitch), MathF.Cos(_pitch) * MathF.Cos(_yaw));

    public Vector3 Forward => Vector3.Normalize(Target - Position);
    public Vector3 Right => Vector3.Normalize(Vector3.Cross(Forward, Vector3.UnitY));
    public Vector3 Up => Vector3.Cross(Right, Forward);

    public float TanHalfFov => MathF.Tan(FovY * MathF.PI / 360f);

    public Camera3D ToRaylib() => new()
    {
        Position = Position,
        Target = Target,
        Up = Vector3.UnitY,
        FovY = FovY,
        Projection = CameraProjection.Perspective,
    };

    /// <summary>View matrix (mathematical layout, see ShaderProgram.Set).</summary>
    public Matrix4x4 View => Raymath.MatrixLookAt(Position, Target, Vector3.UnitY);

    public Matrix4x4 Projection(float aspect)
    {
        double top = Near * TanHalfFov;
        double right = top * aspect;
        double shift = -_shift * right;
        return Raymath.MatrixFrustum(-right + shift, right + shift, -top, top, Near, Far);
    }

    public Ray ScreenRay(Vector2 mouse, float screenW, float screenH)
    {
        float aspect = screenW / screenH;
        float nx = mouse.X / screenW * 2f - 1f - _shift;
        float ny = 1f - mouse.Y / screenH * 2f;
        float t = TanHalfFov;
        var dir = Vector3.Normalize(Right * (nx * t * aspect) + Up * (ny * t) + Forward);
        return new Ray(Position, dir);
    }

    /// <summary>Projects a world point to screen pixels. Returns false when behind the camera.</summary>
    public bool WorldToScreen(Vector3 p, float screenW, float screenH, out Vector2 screen)
    {
        var rel = p - Position;
        float d = Vector3.Dot(rel, Forward);
        screen = default;
        if (d <= Near) return false;
        float aspect = screenW / screenH;
        float t = TanHalfFov;
        float nx = Vector3.Dot(rel, Right) / (d * t * aspect) + _shift;
        float ny = Vector3.Dot(rel, Up) / (d * t);
        screen = new Vector2((nx + 1f) * 0.5f * screenW, (1f - ny) * 0.5f * screenH);
        return true;
    }

    /// <summary>Multiplies a direction by the upper 3x3 of a raymath matrix (column-vector convention).</summary>
    public static Vector3 TransformDir(Matrix4x4 m, Vector3 v) => new(
        m.M11 * v.X + m.M12 * v.Y + m.M13 * v.Z,
        m.M21 * v.X + m.M22 * v.Y + m.M23 * v.Z,
        m.M31 * v.X + m.M32 * v.Y + m.M33 * v.Z);
}
