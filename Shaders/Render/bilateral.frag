#version 430 core
// Screen-space fluid, pass 3: separable depth smoothing that turns the sphere bumps
// into a continuous surface while preserving silhouettes.
// Narrow-range variant: a sample that is background or too far in depth is replaced by
// the centre depth instead of being skipped, which keeps the kernel symmetric and avoids
// the streaks a one-sided average produces along silhouettes.
in vec2 vUv;
out float oDepth;

uniform sampler2D uDepth;
uniform vec2  uDir;          // texel size along the blur axis
uniform float uWorldRadius;  // blur radius in world units
uniform float uPointScale;   // viewport height / tan(fov/2)
uniform float uFalloff;      // 1 / depth tolerance

const float Background = 1e4;

void main()
{
    float d = texture(uDepth, vUv).r;
    if (d >= Background) { oDepth = d; return; }

    float rpx = clamp(uWorldRadius * uPointScale * 0.5 / d, 1.0, 32.0);
    int R = int(rpx);
    float sigma = max(rpx * 0.5, 1.0);
    float inv2s2 = 1.0 / (2.0 * sigma * sigma);
    float baseTol = 1.0 / uFalloff;
    // World size of one pixel at this depth: lets the tolerance grow with the sample offset,
    // so steep (grazing) surfaces are still smoothed while real gaps are preserved.
    float pixelWorld = d / (uPointScale * 0.5);
    const float SlopeAllowance = 4.0;

    float sum = 0.0;
    float wsum = 0.0;
    for (int i = -R; i <= R; i++)
    {
        float s = texture(uDepth, vUv + uDir * float(i)).r;
        float dz = s - d;
        float tolerance = baseTol + abs(float(i)) * pixelWorld * SlopeAllowance;
        if (s >= Background || abs(dz) > tolerance) { s = d; dz = 0.0; }
        // soft range weight inside the tolerance band
        float w = exp(-float(i * i) * inv2s2) * (1.0 - 0.5 * smoothstep(0.0, tolerance, abs(dz)));
        sum += s * w;
        wsum += w;
    }
    oDepth = sum / max(wsum, 1e-6);
}
