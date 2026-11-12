#version 430 core
// Screen-space fluid, pass 4: reconstruct the surface from smoothed depth and shade it
// (Fresnel reflection of a sky gradient, specular highlight, thickness-based absorption).
in vec2 vUv;
out vec4 oColor;

uniform sampler2D uDepth;
uniform sampler2D uThick;
uniform vec2  uTexel;
uniform float uTanHalfFov;
uniform float uAspect;
uniform float uProjShift;   // horizontal lens shift (NDC)
uniform mat4  uProj;
uniform mat4  uInvView;
uniform vec3  uLightDir;    // view space
uniform float uAbsorb;

const float Background = 1e4;

vec3 EyePos(vec2 uv, float d)
{
    vec2 ndc = uv * 2.0 - 1.0;
    ndc.x -= uProjShift;
    return vec3(ndc.x * uTanHalfFov * uAspect * d, ndc.y * uTanHalfFov * d, -d);
}

float D(vec2 uv) { return texture(uDepth, uv).r; }

void main()
{
    float d = D(vUv);
    if (d >= Background) discard;
    vec3 P = EyePos(vUv, d);

    vec2 ox = vec2(uTexel.x, 0.0), oy = vec2(0.0, uTexel.y);
    vec3 ddx  = EyePos(vUv + ox, D(vUv + ox)) - P;
    vec3 ddx2 = P - EyePos(vUv - ox, D(vUv - ox));
    if (abs(ddx.z) > abs(ddx2.z)) ddx = ddx2;
    vec3 ddy  = EyePos(vUv + oy, D(vUv + oy)) - P;
    vec3 ddy2 = P - EyePos(vUv - oy, D(vUv - oy));
    if (abs(ddy.z) > abs(ddy2.z)) ddy = ddy2;
    vec3 n = cross(ddx, ddy);
    n = length(n) > 1e-12 ? normalize(n) : vec3(0.0, 0.0, 1.0);
    if (abs(ddx.z) > 0.5 || abs(ddy.z) > 0.5) n = vec3(0.0, 0.0, 1.0); // isolated pixel

    vec4 th = texture(uThick, vUv);
    float thick = th.a;
    vec3 base = th.rgb / max(thick, 1e-5);

    vec3 V = normalize(-P);
    vec3 H = normalize(uLightDir + V);
    float ndl = dot(n, uLightDir);
    float diff = 0.45 + 0.55 * clamp(ndl, 0.0, 1.0);
    float spec = pow(max(dot(n, H), 0.0), 200.0) * 0.7;
    float fres = 0.02 + 0.98 * pow(1.0 - clamp(dot(n, V), 0.0, 1.0), 5.0);

    vec3 R = (uInvView * vec4(reflect(-V, n), 0.0)).xyz;
    vec3 sky = mix(vec3(0.04, 0.05, 0.09), vec3(0.62, 0.72, 0.92), smoothstep(-0.25, 0.85, R.y));

    float absorb = 1.0 - exp(-thick * uAbsorb);
    vec3 col = base * diff * mix(1.15, 0.8, absorb);
    col = mix(col, sky, fres * 0.75) + vec3(spec);
    float alpha = clamp(0.5 + 0.5 * absorb + fres * 0.3, 0.0, 1.0);

    vec4 clip = uProj * vec4(P, 1.0);
    gl_FragDepth = clip.z / clip.w * 0.5 + 0.5;
    oColor = vec4(col, alpha);
}
