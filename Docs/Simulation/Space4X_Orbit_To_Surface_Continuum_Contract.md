# Space4X Orbit-to-Surface Continuum Contract

Status: Draft v1 (planning contract)
Date: 2026-02-18
Related:
- `Docs/Simulation/Orbits_ReferenceFrames.md`
- `Docs/Simulation/Space4X_Solar_System_Expansion_Architecture.md`
- `Docs/Simulation/Space4X_Solar_System_Expansion_Plan.md`

## 1) Purpose

Define a continuous, large-feeling spatial band from orbit to planet surface that can host massive battles while preserving:

- Tiny surface entity readability.
- Planet scale without consuming the full system budget.
- Deterministic DOTS simulation with bounded cost.

## 2) Core Model

Use layered coordinates and tiered simulation:

1. `SystemFrame` (authoritative, high precision)
   - Orbit and long-range movement truth.
2. `PlanetFrame` (authoritative, high precision)
   - Planet-centered frame for approach and gravity context.
3. `SurfacePatchFrame` (authoritative local frame)
   - Tangent/local patch frame for small entities in meters.
4. `RenderFrame` (camera-relative float)
   - Derived view-space transforms only.

Rule: gameplay never depends on camera-space coordinates.

## 3) Continuum Tiers (Altitude by Planet Radius)

Let `r = distance_to_planet_center / planet_radius`.

- Tier A: Deep Orbit
  - `r > 8.0`
  - Aggregated or on-rails simulation.
- Tier B: Operational Orbit
  - `2.0 < r <= 8.0`
  - Fleet movement and wide engagements.
- Tier C: Near-Orbital Combat
  - `1.05 < r <= 2.0`
  - Dense combat, orbital infrastructure, interception lanes.
- Tier D: Approach Shell
  - `1.005 < r <= 1.05`
  - Transitional shell to surface patches, high detail POI density.
- Tier E: Surface Local
  - `0.999 < r <= 1.005`
  - Local-patch simulation with meter-scale entities.

Use hysteresis for transitions (`enter` and `exit` thresholds) to prevent thrash.

## 4) Planet Size vs System Size Constraint

To keep planets feeling huge while not consuming system coordinates:

- Keep physical/orbital truth in frame coordinates, not raw float world units.
- Bind local gameplay to `SurfacePatchFrame` meters.
- Promote/demote entities between tiers through frame membership changes.
- Use presentation scaling and camera-relative rendering for far/near readability.

Outcome:
- Planet can feel massive on approach.
- Surface entities remain tiny and stable in local meters.
- System remains tractable and not dominated by one giant float-space mesh.

## 5) Entity Hosting on Planets

Entities "on planet" are hosted by patch anchors:

- `PlanetoidBody` (planet metadata and frame root).
- `SurfacePatchAnchor` entities (partitioned around active regions).
- Hosted entities store patch-local position and velocity.

Patch policy:

- Only activate patches near active players/fleets/objectives.
- Keep inactive patches in coarse state.

## 6) Battle Envelope Rules

Massive battles across the continuum are supported by envelope constraints:

- Tier B-C: primary large-fleet battle volumes.
- Tier D: approach battles with higher occlusion and terrain influence.
- Tier E: localized battles tied to patch activation.
- Cross-tier targeting is allowed only through explicit proxy/adaptor logic.

## 7) Activation and Streaming Rules

- High-cost systems run only in active tiers/patches.
- Use enableable components for frequent activation toggles.
- Use scene sections/streamed content for heavy assets and static population.
- Batch structural changes with ECB.

## 8) Precision and Rendering Rules

- High-precision frame state for orbital and planet frames.
- Camera-relative rendering/origin management for precision near viewer.
- Depth precision strategy should assume large far range and avoid tiny near clip defaults.

## 9) Acceptance Checks

- Continuity: no visible or physical jumps at tier boundaries.
- Stability: no jitter regression when moving from Tier C -> D -> E.
- Scale read: approach conveys increasing planetary dominance.
- Surface read: entities on Tier E remain visually tiny and controllable.
- Cost: active-system budget stays within profile limits.

## 10) External References (Design Inputs)

- Cesium origin shifting and globe anchoring:
  - https://cesium.com/learn/unity/unity-placing-objects/
- Cesium georeferenced sub-scenes (local simulation islands):
  - https://cesium.com/learn/unity/unity-subscenes/
- Unity Entities scene streaming:
  - https://docs.unity.cn/Packages/com.unity.entities%401.0/manual/streaming-scenes.html
- Unity Entities enableable components:
  - https://docs.unity.cn/Packages/com.unity.entities%401.3/manual/components-enableable-use.html
- Unreal LWC translated world-space guidance:
  - https://dev.epicgames.com/documentation/en-us/unreal-engine/large-world-coordinates-rendering-in-unreal-engine-5
- Depth precision / reversed-Z rationale:
  - https://developer.nvidia.com/blog/visualizing-depth-precision/
  - https://docs.unity3d.com/cn/2018.4/Manual/SL-PlatformDifferences.html
- Ellipsoidal clipmaps (planet-scale terrain continuity):
  - https://doi.org/10.1016/j.cag.2015.06.006
  - https://doi.org/10.1016/j.gmod.2023.101209

