// Shared particle vertex inputs + colour mapping.
layout(location = 0) in vec4 aPos;
layout(location = 1) in vec4 aVel;
layout(location = 2) in vec2 aDens;

uniform sampler2D uGradient;
uniform int   uColorMode;      // 0 = speed, 1 = density
uniform float uSpeedMax;
uniform float uTargetDensity;

vec3 ParticleColor()
{
    float t;
    if (uColorMode == 0)
        t = length(aVel.xyz) / uSpeedMax;
    else
        t = (aDens.x / uTargetDensity - 0.55) / 0.9;
    return textureLod(uGradient, vec2(clamp(t, 0.0, 1.0), 0.5), 0.0).rgb;
}
