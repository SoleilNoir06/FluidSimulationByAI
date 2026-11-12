#version 430 core
// Turns the accumulated splat field into a shaded liquid surface (metaball style).
in vec2 vUv;
out vec4 oColor;

uniform sampler2D uField;
uniform vec2  uTexel;
uniform float uThreshold;

float Height(vec2 uv)
{
    float a = texture(uField, uv).a;
    return 1.0 - exp(-1.4 * max(a - uThreshold * 0.6, 0.0));
}

void main()
{
    vec4 f = texture(uField, vUv);
    float a = f.a;
    if (a < uThreshold * 0.4) discard;

    float aa = fwidth(a) + 1e-4;
    float cover = smoothstep(uThreshold - aa, uThreshold + aa, a);
    if (cover <= 0.0) discard;

    vec3 base = f.rgb / max(a, 1e-4);

    vec2 dx = vec2(uTexel.x * 2.0, 0.0);
    vec2 dy = vec2(0.0, uTexel.y * 2.0);
    float hx = Height(vUv + dx) - Height(vUv - dx);
    float hy = Height(vUv + dy) - Height(vUv - dy);
    vec3 n = normalize(vec3(-hx * 3.0, -hy * 3.0, 1.0));

    vec3 L = normalize(vec3(-0.45, 0.65, 0.62));
    float diff = clamp(dot(n, L), 0.0, 1.0);
    float spec = pow(max(dot(reflect(-L, n), vec3(0.0, 0.0, 1.0)), 0.0), 28.0);
    float rim = 1.0 - n.z;
    float depth = clamp((a - uThreshold) / 4.0, 0.0, 1.0);

    vec3 col = base * (0.72 + 0.38 * diff) * mix(1.06, 0.86, depth);
    col += vec3(spec) * 0.35 + base * rim * 0.45;
    oColor = vec4(col, cover);
}
