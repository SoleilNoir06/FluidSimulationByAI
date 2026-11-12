#version 430 core
// Ray-traced sphere impostor on a point sprite: correct normals and depth.
in vec3 vColor;
in vec3 vCenter;
out vec4 oColor;

uniform mat4  uProj;
uniform float uRadius;
uniform vec3  uLightDir;   // view space, normalised

void main()
{
    vec2 c = gl_PointCoord * 2.0 - 1.0;
    c.y = -c.y;
    float r2 = dot(c, c);
    if (r2 > 1.0) discard;

    vec3 n = vec3(c, sqrt(1.0 - r2));
    vec3 P = vCenter + n * uRadius;
    vec4 clip = uProj * vec4(P, 1.0);
    gl_FragDepth = clip.z / clip.w * 0.5 + 0.5;

    vec3 V = normalize(-P);
    vec3 H = normalize(uLightDir + V);
    float diff = max(dot(n, uLightDir), 0.0);
    float spec = pow(max(dot(n, H), 0.0), 64.0);
    float rim = pow(1.0 - max(dot(n, V), 0.0), 3.0);

    vec3 col = vColor * (0.30 + 0.80 * diff) + vec3(spec) * 0.45 + vColor * rim * 0.25;
    oColor = vec4(col, 1.0);
}
