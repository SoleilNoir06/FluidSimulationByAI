# Fluid Lab — SPH fluid simulation (2D / 3D)

## Built entirely with Claude Code, as a way to test it and learn how to use it.

Real-time GPU fluid simulation in C#, using **Raylib-cs** (window, interface, primitives)
and **OpenTK** (compute shaders and custom OpenGL rendering).

## Running

```bash
dotnet run -c Release
```

Requirements: .NET 10 and a GPU that supports OpenGL compute shaders (OpenGL 4.3, or the
`ARB_compute_shader` + `ARB_shader_storage_buffer_object` extensions — any recent NVIDIA/AMD card).

## Features

- **2D and 3D views**, two independent worlds: switch with `Tab` or the selector; each one keeps its state.
- **Tools**: add particles (you can "throw" them by moving the mouse), attract, repel, erase.
- **Scenes**: dam break, double dam, drop, pool, empty.
- **2D rendering**: coloured particles, or *liquid* mode (lit metaball field).
- **3D rendering**: lit spheres, or *liquid* mode (screen-space fluid rendering: smoothed depth,
  reconstructed normals, Fresnel, specular highlights, thickness-based absorption).
- **6 gradient palettes**, colour by speed or density.
- Settings panel: gravity, viscosity, stiffness, near pressure, wall bounciness, simulation speed, substeps…
  (right-click a slider to reset it, hold Shift for rounded steps).

## Controls

| Input | Action |
| --- | --- |
| Left click | Selected tool |
| Right click (2D) | Inverse action (attract ↔ repel, add ↔ erase) |
| Right drag (3D) | Orbit the camera |
| Scroll wheel | Brush radius (2D) · zoom (3D) |
| Shift + scroll | Brush radius (3D) |
| `1` `2` `3` `4` | Add · Attract · Repel · Erase |
| `Tab` | 2D ↔ 3D |
| `Space` | Pause / play |
| `→` or `N` | Step one frame |
| `R` | Reset the scene |
| `V` | Particles / liquid |
| `C` | Next palette |
| `H` | Hide the interface |
| `F` or `F11` | Fullscreen |
| `F1` | Help |

## Architecture

```
App/             FluidApp (main loop, input, tools, frame composition), capture options
Simulation/      GpuFluidSim (compute pipeline), SimSettings, Scenes
Rendering/       FluidRenderer2D / 3D, OrbitCamera, gradients, render targets, GL helpers
UI/              Immediate-mode GUI (animated widgets), panel, theme, vector icons, fonts
Shaders/Sim/     simulation compute shaders
Shaders/Render/  rendering shaders (impostors, metaballs, screen-space fluid)
Assets/Fonts/    Inter font (SIL OFL licence, see Inter-LICENSE.txt)
```

**Simulation** — "double density" SPH (pressure + near pressure, Clavet et al. 2005), viscosity,
predicted positions. Every substep, particles are sorted by grid cell (GPU counting sort: hashing,
two-level prefix sum, reordering), which makes the neighbour search linear instead of quadratic.
The particle buffers are read directly as vertex attributes for rendering, with no CPU copy.

## Automated capture (development)

```bash
FluidLab --shot capture.png --frames 200 --dim 3 --style liquid --scene drop --hide-ui
```

Options: `--dim 2|3`, `--style particles|liquid`, `--scene dam|double|drop|pool|empty`, `--palette N`,
`--density`, `--hide-ui`, `--help-overlay`, `--scroll px`, `--poke x y add|attract|repel|erase from to`.
