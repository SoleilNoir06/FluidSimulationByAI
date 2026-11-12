#version 430 core
// Additive splat into the liquid field: rgb = colour * weight, a = weight.
in vec3 vColor;
out vec4 oColor;

void main()
{
    vec2 c = gl_PointCoord * 2.0 - 1.0;
    float r2 = dot(c, c);
    if (r2 >= 1.0) discard;
    float w = 1.0 - r2;
    w *= w;
    oColor = vec4(vColor * w, w);
}
