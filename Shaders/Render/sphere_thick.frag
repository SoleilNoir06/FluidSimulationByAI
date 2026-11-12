#version 430 core
// Screen-space fluid, pass 2: additive thickness (a) and thickness-weighted colour (rgb).
in vec3 vColor;
in vec3 vCenter;
out vec4 oColor;

uniform float uRadius;

void main()
{
    vec2 c = gl_PointCoord * 2.0 - 1.0;
    float r2 = dot(c, c);
    if (r2 > 1.0) discard;
    float t = sqrt(1.0 - r2) * 2.0 * uRadius;
    oColor = vec4(vColor * t, t);
}
