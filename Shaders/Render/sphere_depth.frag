#version 430 core
// Screen-space fluid, pass 1: nearest sphere surface as linear eye depth.
in vec3 vColor;
in vec3 vCenter;
out float oDepth;

uniform mat4  uProj;
uniform float uRadius;

void main()
{
    vec2 c = gl_PointCoord * 2.0 - 1.0;
    c.y = -c.y;
    float r2 = dot(c, c);
    if (r2 > 1.0) discard;

    vec3 P = vCenter + vec3(c, sqrt(1.0 - r2)) * uRadius;
    vec4 clip = uProj * vec4(P, 1.0);
    gl_FragDepth = clip.z / clip.w * 0.5 + 0.5;
    oDepth = -P.z;
}
