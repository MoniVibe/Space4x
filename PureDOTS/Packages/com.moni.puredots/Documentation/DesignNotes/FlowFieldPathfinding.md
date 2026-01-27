# Flow Field Pathfinding & Villager Movement Plan

## Purpose
Provide a scalable, deterministic navigation strategy for large crowds (100k–1M villagers) that fits the PureDOTS architecture, integrates with the spatial grid, and stays compatible with rewind. This replaces the temporary `sp.plan.md` document.

## High-Level Goals
- **Crowd scale:** Support tens of thousands of simultaneous movers without per-agent path searches.
- **Determinism:** Rebuild navigation data deterministically each frame/interval; replay produces identical results.
- **Modularity:** Separate high-level field generation, local steering, and job logic so systems evolve independently.
- **Configurability:** Designers tweak flow field resolution, rebuild cadence, steering weights via assets.
- **Observability:** Provide debugging overlays and statistics for performance tuning.

## Core Concepts
1. **Spatial grid dependency:** Reuse the hashed grid from `SpatialServices_TODO.md` for obstacle lookup, goal distribution, and sensor queries.
2. **Flow fields:** Direction maps computed in Burst jobs for each “goal category” (e.g., storehouses, safety zones, rally points).
3. **Local steering:** Per-agent adjustments (Reynolds separation/avoidance) layered on top of flow field direction.
4. **Sensors:** Cached proximity data (nearby villagers, threats, resources) updated on a configurable tick interval.
5. **Command requests:** Villager systems enqueue path/goal changes via buffers; flow-field builder consumes these to update fields.

## Data Layout
- **Components**
  - `FlowFieldConfig` (singleton): cell size, world bounds, rebuild cadence, weights.
  - `FlowFieldLayer` (buffer on singleton): per-goal metadata (layer id, mask, priority, rebuild interval, last build tick).
  - `FlowFieldCellData` (DynamicBuffer on singleton): direction vector (float2), cost, occupancy flags (SoA packing recommended).
  - `VillagerFlowState`: current layer id, cell index, cached direction, speed scalar.
  - `VillagerSpatialSensor`: cached entity lists (villager neighbours, threats, resources) + timestamp.
  - `VillagerSteeringState`: separation vector, obstacle avoidance vector, blended heading.
- **Blobs**
  - `FlowFieldProfileBlob`: precomputed sampling offsets, steering weights, goal filters from ScriptableObject.
  - `SteeringWeightBlob`: weights for avoidance/alignment/cohesion per archetype.
- **Buffers**
  - `FlowFieldRequest` (singleton buffer): queued goal changes (entity, layer id, priority, validity tick).
  - `FlowFieldHazardUpdate` (singleton buffer): pending danger/cost adjustments (cell id, delta cost, expiration tick).
  - `FlowFieldCacheEntry` (buffer per layer): cached sub-field gradients for hierarchical reuse (macro cells, corridor segments).
  - `VillagerCommandBuffer`: already present for job commands; extend to include movement overrides if needed.

## System Overview
1. **FlowFieldRequestSystem (Initialization)**
   - Collects new/updated goals (e.g., storehouse built, rally point changed).
   - Applies hazard updates to layer cost modifiers.
   - Marks relevant layers dirty; schedules rebuild jobs for next allowed tick.
2. **FlowFieldBuildSystem (Fixed Step / Spatial System Group)**
   - Runs Burst jobs to recompute dirty layers:
     - Gather sources (goal positions) and obstacles from spatial grid snapshots.
     - Use Dijkstra/Fast-Marching style propagation to compute cell costs and direction vectors.
     - Write results into double-buffered `FlowFieldCellData` to keep readers stable.
   - Deterministic iteration order (sorted cell indices, consistent queue usage).
   - Reuses cached sub-fields when only local segments changed; rebuild macro layer before micro layer to keep hierarchy coherent.
3. **FlowFieldFollowSystem (Simulation)**
   - For each villager with `VillagerFlowState`:
     - Sample flow field direction (bilinear interpolation optional).
     - Blend with steering vectors and job-specific speed modifiers.
     - Output target heading / acceleration into `VillagerMovement` data (or LocalTransform proxy).
4. **VillagerSensorUpdateSystem (Simulation, lower frequency)**
   - Every N ticks, query spatial grid around each villager (radius from archetype) to refresh `VillagerSpatialSensor`.
   - Cache results so other systems read without additional queries.
5. **VillagerLocalSteeringSystem (Simulation)**
   - Uses sensor data to compute avoidance/separation/cohesion forces.
   - Combine with flow direction; clamp to max turn rate/speed.
6. **Movement Integration**
   - Short-term: apply blended direction to `LocalTransform` (simple move).
   - Future: integrate with DOTS Physics or NavMesh once available.

## Phased Implementation Roadmap
### Phase 0 – Foundations (1–2 weeks)
- Finalize data schemas & authoring assets (`FlowFieldProfile`, `SteeringWeights`).
- Hook up `FlowFieldConfig` singleton and layer buffer.
- Instrument profiling markers.

### Phase 1 – Spatial Sensors (1–2 weeks)
- Implement `VillagerSpatialSensor` component + update system.
- Integrate with AI/job logic for threat detection and job evaluation.
- Add basic debug visualization for sensor ranges.

### Phase 2 – Job Query Optimization (1 week)
- Replace linear scanning in job assignment with sensor + spatial grid radius queries.
- Validate deterministic behaviour and performance gains.

### Phase 3 – Local Steering (1–2 weeks)
- Add `VillagerSteeringState` and steering system (Reynolds separation + obstacle avoidance).
- Blend steering with existing target heading (without flow fields yet).
- Add tests to ensure deterministic results and zero GC.

### Phase 4 – Flow Field MVP (2–3 weeks)
- Build single-layer flow field generation (e.g., “Deliver to nearest storehouse”).
- Hardcode limited goals; run rebuild every fixed cadence.
- Integrate with `VillagerFlowState` and verify villagers follow direction map.
- Measure performance with 100k agents.

### Phase 5 – Multi-Layer Flow Fields (2 weeks)
- Support multiple layers (resources, safety zones, combat rally points).
- Add layer prioritization and per-layer rebuild cadence.
- Allow villagers to switch layers based on job/command.

### Phase 6 – Optimization & Polish (1–2 weeks)
- Introduce lazy rebuild (only when goals/obstacles change significantly).
- Cache partial results / hierarchical grids (macro + micro).
- Integrate optional GPU acceleration investigation.
- Expand debug tooling (flow field overlays, layer toggles).

## Determinism & Rewind Strategy
- Rebuild flow fields when entering playback/catch-up; skip updates in playback.
- Snapshot minimal metadata (layer dirty flags, seeds) if rebuild cost becomes excessive; otherwise recompute deterministically.
- Ensure sensor caches can be recomputed cheaply when rewinding (e.g., mark as invalid and refresh next simulation tick).
- Use deterministic priority queues (fixed-size arrays or radix queue) to avoid scheduling differences between runs.

## Performance Targets
- **Sensors:** 10k agents per frame, cache for 5–10 ticks (<0.5 ms / 10k agents).
- **Flow fields:** 256×256 grid per layer generated every 30–60 ticks under 3 ms.
- **Steering:** <1.5 ms for 50k agents per frame (vectorized math).
- **Overall:** 100k villagers moving with flow fields under 5 ms total navigation budget on target hardware.
- **Cache reuse:** Track ratio of cached vs. rebuilt segments; target >60% reuse in stable scenarios to cut rebuild time.

## Testing Strategy
- Unit tests for flow field generation (cost propagation, direction accuracy).
- Determinism tests: identical inputs produce identical flow fields and steering outputs.
- Rewind tests: record/rewind/resume verifying flow states & sensors match.
- Stress benchmarks at 10k/50k/100k/1M inhabitants using placeholder movement.
- Integration tests combining job assignment, sensors, and flow fields to ensure consistent behaviour.
- Hazard regression tests to ensure dynamic danger updates bias flow away from risky cells.

## Tooling & Debugging
- Flow field visualizer (scene gizmos) showing direction vectors and cost heatmaps.
- Runtime UI overlay with layer toggles, rebuild stats, sensor hit counts.
- Performance logging (frames per rebuild, agent update time) integrated into profiling harness.
- Editor validation: warn if flow field bounds mismatch scene size; check cell size vs. spatial grid config.
- Cached segment overlay to highlight reused gradients and verify hierarchy efficiency.

## Dependencies
- Relies on spatial partition service (hashed grid) for obstacle and neighbor queries.
- Uses pooled native containers/instrumentation from registry/memory utilities.
- Shares HUD/presentation pipeline with divine hand/camera for debugging overlays.
- Consumes time-of-day service for schedule-based layer switching (e.g., night-time shelter fields).

## Next Actions
1. Approve data schemas and config assets with the simulation leads.
2. Schedule Phase 1 (sensors) as first implementation milestone.
3. Set up profiling scenes (10k/50k/100k villagers) for repeatable benchmarks.
4. Update `Docs/TODO/VillagerSystems_TODO.md` to link to this plan (already referenced).
5. Begin implementation following phase roadmap, updating this document with findings.
