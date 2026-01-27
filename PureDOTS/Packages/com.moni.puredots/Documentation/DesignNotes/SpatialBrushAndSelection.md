# Spatial Brush & Selection System Concepts

## Goals
- Provide a shared DOTS-friendly toolkit for spatial selection, brush-based painting, and area-of-effect targeting across games (terraforming, miracles, logistics, RTS commands).
- Support free-form shapes (circle, rectangle, spline, lasso), brush falloffs, and dynamic modifiers (intensity, hardness).
- Integrate with spatial services (grid, danger layers), registries, and event systems while remaining deterministic and Burst-ready.

## Core Components
- `BrushInputState` component:
  - `float3 Origin`, `float Radius`, `float Hardness`, `float Falloff`, `float Angle`, `BrushShape Shape`, `BrushMode Mode`.
  - `BrushFlags` (additive, subtractive, smooth, invert).
- `SelectionStrokeBuffer` (dynamic buffer):
  - Stores stroke samples (world position, timestamp, pressure) for replay or delayed evaluation.
- `SelectionResultBuffer`:
  - Entities or cells affected by latest stroke, with weight (0–1) and metadata (distance, falloff).
- `BrushProfileBlob`:
  - Defines default falloffs, allowed shapes, axis constraints, material cost multipliers.
- `AreaPreviewData`:
  - Cached mesh/line data for rendering outlines (cold path).
- `TerraformingEffectCommand`, `MiracleAreaCommand`, `LogisticsAreaCommand`:
  - Specialized command buffers that consume `SelectionResultBuffer` to drive downstream systems.

## Brush Shapes & Computation
- Circle / sphere: project onto terrain plane or use world space radius (3D).
- Rectangle / box: align with camera-facing plane or specified orientation.
- Spline / freehand: interpret stroke buffer as polyline; generate polygon via triangulation.
- Lasso: similar to spline; closed polygon with winding rules to determine inclusion.
- Paint: accumulate weights over time; supports layering (multiple passes) by storing per-cell weight and blending (max, additive, average).

### Falloff & Intensity
`weight = pow(1 - saturate(distance / radius), falloffExponent) * hardness;`
- Support custom curves via lookup tables (blob data).
- For 3D brushes, distance measured in full 3D or projected onto surface depending on mode.

## Selection Pipeline
1. `BrushInputSystem` (Presentation/Interaction):
   - Reads divine hand/mouse/VR input, updates `BrushInputState`, records stroke samples.
   - Applies snapping (grid, angle) if modifiers active.
   - Chooses projection mode (`TerrainSurface`, `WorldVolume`, `CustomPlane`) based on brush profile.
   - Applies input-plane resolution rules:
     - Terrain mode: raycast from cursor/hand to terrain collision; use hit point for origin.
     - Space mode: project onto adaptive plane anchored to camera forward at target depth; adjust plane height slowly (lerp) to avoid jitter.
     - Vertical casting: clamp plane normal to world up when aiming above ground, so miracles spawn at elevated position.
2. `BrushSamplingSystem` (Simulation/Spatial Group):
   - For terrain mode: project brush onto terrain plane, sample intersecting grid cells (2.5D).
   - For 3D mode: traverse nav volume cells or sample points along stroke volume (sphere/capsule/spline extrusion).
   - Computes weight per cell/entity using falloff formulas.
   - Writes results into `SelectionResultBuffer` with version/tick metadata and projection mode.
3. `SelectionFilterSystem`:
   - Filters entities based on mode (terraformable terrain, selectable units, resource nodes).
   - Cross-references registries and layer masks; emits typed command buffers.
4. `AreaCommandDispatchSystem`:
   - Converts selection into action-specific commands (raise terrain, apply miracle, target logistic zone).
   - Applies intensity scaling, resource cost, cooldown checks.
5. `StrokeReplaySystem` (optional):
   - Replays stored strokes for undo/redo or scheduled actions (e.g., timed miracles).

## Data Integration
- **Spatial Grid**: accelerate selection by iterating only cells intersecting brush volume; caches per-tick lists to avoid repeated overlap tests.
- **Danger Layers**: brush preview color-coded based on danger or resource density layers for player feedback.
- **Terrain Height**: use height/slope metadata to adjust brush effect (clamp to maximum slope change).
- **Registries**: filtered selection writes entity handles that other systems (terraforming, AI commands) consume.
- **Scheduler**: queue long-running area operations (large terraforming) via `SchedulerAndQueueing.md`.
- **Event System**: emit `AreaSelectionEvent` for analytics, undo stack, or narrative triggers.

## Authoring & Config
- `BrushProfile` ScriptableObject:
  - Default radius range, falloff curve, hardness limits, allowed shapes.
  - Resource cost multipliers (mana, materials) per mode.
- `SelectionFilterProfile`:
  - Defines which component tags qualify for selection per mode (e.g., terraform → `TerrainCell`, logistics → `Storehouse`, miracles → `Villager`, `ResourceNode`).
- `HotspotPreset` assets for stored patterns (e.g., farmland layout, defensive wall).
- Bakers produce blobs consumed by runtime systems.

## Technical Considerations
- **SoA Layout**: store selection weights, distances, and entity ids in separate arrays to keep Burst-friendly.
- **Parallel Sampling**: use `IJobParallelFor` over candidate cells; compute weights and mark results via `ParallelWriter`.
- **Determinism**: sort selection results by cell/entity id before writing commands; ensure stroke sampling uses fixed time steps.
- **Undo/Redo**: maintain history buffers with deterministic replay instructions; integrate with Rewind by replaying strokes within recorded window.
- **Multiplayer readiness**: stroke buffer can be serialized; compression via delta encoding.
- **Performance**: target <0.5 ms per brush sample pass for 1k cells; avoid dynamic allocations by pooling selection buffers.
- **3D Space**: adapt brush projection to 3D volumes (e.g., sphere brush) by using nav volumes and cell adjacency from `UniversalNavigationSystem.md`.
- **Input plane best practices**:
  - Align plane to dominant surface normal (terrain vs. ship plane) with smoothing to prevent sudden jumps.
  - Clamp plane distance to avoid accidental far-plane hits when cursor passes over void.
  - Bias move orders for Space4x by snapping to nearest valid nav volume cell and providing ghost preview before committing.
  - Provide altitude handles (mouse wheel, modifier keys) to adjust plane height explicitly when needed.

## Testing
- Unit tests for brush falloff math, shape inclusion, weight accumulation.
- Integration tests verifying selections hit correct entities, respect filters, and align with registries.
- Performance benchmarks for continuous painting at various radii.
- Rewind tests to ensure recorded strokes replay identically.

## Tooling & Visualization
- Editor gizmos showing brush radius, falloff rings, and intensity preview.
- Heatmap overlay displaying selection weights and dangerous areas.
- Stroke history viewer for debugging (timeline with intensity).
- Config inspectors with real-time preview using stored heightmaps.
