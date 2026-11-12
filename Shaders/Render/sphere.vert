#version 430 core
#include "particle_common.glsl"

uniform mat4  uView;
uniform mat4  uProj;
uniform float uRadius;      // world-space sphere radius
uniform float uPointScale;  // viewport height / tan(fov/2)

out vec3 vColor;
out vec3 vCenter;           // view-space centre

void main()
{
    vec4 vp = uView * vec4(aPos.xyz, 1.0);
    vCenter = vp.xyz;
    gl_Position = uProj * vp;
    gl_PointSize = uRadius * uPointScale / max(-vp.z, 1e-3);
    vColor = ParticleColor();
}
