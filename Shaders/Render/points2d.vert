#version 430 core
#include "particle_common.glsl"

uniform mat4  uMvp;
uniform float uPointSize;   // diameter in pixels

out vec3 vColor;

void main()
{
    gl_Position = uMvp * vec4(aPos.xy, 0.0, 1.0);
    gl_PointSize = uPointSize;
    vColor = ParticleColor();
}
