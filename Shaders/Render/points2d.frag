#version 430 core
in vec3 vColor;
out vec4 oColor;
uniform float uPointSize;

void main()
{
    vec2 c = gl_PointCoord * 2.0 - 1.0;
    float r = length(c);
    float aa = 2.0 / max(uPointSize, 1.0);
    float alpha = 1.0 - smoothstep(1.0 - aa * 1.5, 1.0, r);
    if (alpha <= 0.0) discard;
    vec3 col = vColor * (0.9 + 0.18 * (1.0 - r * r));
    oColor = vec4(col, alpha);
}
