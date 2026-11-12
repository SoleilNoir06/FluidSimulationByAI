// ---------------------------------------------------------------------------
// Shared header for every simulation compute shader.
// The C# side prepends "#version 430 core" and "#define DIM 2|3".
// Model: "double density relaxation" SPH (Clavet et al. 2005) expressed as
// pressure / near-pressure forces, with a uniform grid for neighbour search.
// ---------------------------------------------------------------------------

uniform int   uNumParticles;
uniform float uDt;
uniform vec3  uBoundsSize;        // box centred on the origin
uniform float uH;                 // smoothing radius (= grid cell size)
uniform float uTargetDensity;
uniform float uPressureMult;
uniform float uNearPressureMult;
uniform float uViscosity;
uniform float uGravity;
uniform float uCollisionDamping;
uniform ivec3 uGridDim;
uniform int   uNumCells;

uniform vec3  uInteractPos;
uniform float uInteractRadius;
uniform float uInteractStrength;  // > 0 attract, < 0 repel, 0 = off

// Kernel normalisation constants (dimension dependent, computed on the CPU)
uniform float uKSpikyPow2;
uniform float uKSpikyPow3;
uniform float uKSpikyPow2Deriv;
uniform float uKSpikyPow3Deriv;
uniform float uKPoly6;

#if DIM == 2
  #define ZR 0
#else
  #define ZR 1
#endif

float DensityKernel(float r)         { float v = uH - r; return v * v * uKSpikyPow2; }
float NearDensityKernel(float r)     { float v = uH - r; return v * v * v * uKSpikyPow3; }
float DensityDerivative(float r)     { float v = uH - r; return -v * uKSpikyPow2Deriv; }
float NearDensityDerivative(float r) { float v = uH - r; return -v * v * uKSpikyPow3Deriv; }
float ViscosityKernel(float r)       { float v = uH * uH - r * r; return v * v * v * uKPoly6; }

ivec3 CellCoord(vec3 p)
{
    ivec3 c = ivec3(floor((p + 0.5 * uBoundsSize) / uH));
    return clamp(c, ivec3(0), uGridDim - 1);
}

int CellIndex(ivec3 c) { return (c.z * uGridDim.y + c.y) * uGridDim.x + c.x; }

bool CellInGrid(ivec3 c) { return all(greaterThanEqual(c, ivec3(0))) && all(lessThan(c, uGridDim)); }
