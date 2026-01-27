# Environmental Effects & Phenomena Concepts

## Goals
- Provide a unified system for environmental effects (fog, dust, smog, blizzards, hail, lightning, tornadoes, hurricanes, nebulae, localized phenomena) usable by both Godgame and Space4x.
- Integrate effects with climate grids, spatial services, perception, navigation, and gameplay mechanics.
- Keep effect definitions data-driven via catalogs and ensure deterministic Burst-friendly evaluation.

## Effect Types
- **Scalar Fields**: modify existing grids (visibility, temperature, pollution).
- **Vector Fields**: influence wind, flow, or debris direction.
- **Event Pulses**: discrete events (lightning strikes, meteor impacts) that schedule command buffers.
- **Volumes**: 3D or 2.5D regions with effect density/falloff (fog banks, dust clouds, nebulae).

## Data Model
- `EnvironmentEffectCatalog` (existing): extend with new effect types and parameters.
- `EnvironmentEffectDefinition`:
  - `EffectType` (scalar, vector, pulse, volume).
  - `Shape` (sphere, box, cylinder, spline volume).
  - `IntensityCurve` over time (activation, sustain, decay).
  - `SpatialFalloff` (linear, exponential).
  - `Tags` (weather, magical, hazard, space).
  - `GameplayModifiers` (movement penalty, perception penalty, damage).
- `ActiveEnvironmentEffect` component:
  - Stores runtime state (current intensity, elapsed time, seed, owner entity).
- `EnvironmentEffectVolume` buffer:
  - Precomputed voxel/ cell coverage for volumes to accelerate sampling.
- `LightningStrikeEvent`, `TornadoSpawnCommand`, `HurricanePathState` components for special cases.

## System Pipeline
1. **Effect Scheduling** (`EnvironmentEffectSchedulerSystem`):
   - Activates effects based on climate state, narrative events, or miracles.
   - Handles recurring/seasonal triggers and random seeds for placement.
2. **Effect Update** (`EnvironmentEffectUpdateSystem`):
   - Already evaluates scalar/vector/pulse effects; extend to support volume sampling with spatial hashing for performance.
   - Applies contributions to climate grids (moisture, temperature), navigation danger layers, and perception modifiers.
3. **Volume Sampling** (`EnvironmentEffectVolumeSystem`):
   - Generates cell masks for new volumes, caches in `EnvironmentEffectVolume`.
   - Supports both terrain-aligned and full 3D volumes (Space4x nebulae).
4. **Event Dispatch** (`EnvironmentEffectEventSystem`):
   - Lightning/hail pulses spawn command buffers (damage, visual effects).
   - Tornado/Hurricane update position, apply forces to nearby entities, update path state.
5. **Cleanup** (`EnvironmentEffectCleanupSystem`):
   - Removes expired effects, releases pooled buffers, emits `EffectEndedEvent`.

## Integration Points
- **Climate Systems**: storms modify moisture, temperature; hail accumulates snow/ice layers; dust storm increases pollution.
- **Perception**: effect volumes register as `PerceptionModifierVolume` (dense fog reduces visibility).
- **Navigation**: update danger layers and traversal penalties (e.g., avoid tornado path).
- **Skill/Status Effects**: apply buffs/debuffs (e.g., lightning storm increases mana cost, nebula reduces sensor range).
- **Miracles & Abilities**: triggers from hand/fleet commands instantiate effects via scheduler.
- **Logistics/Production**: storms can delay production orders, damage ships.
- **Narrative**: events respond to storms starting/ending, tornado path hitting settlements.
- **Telemetry**: track effect duration, coverage area, damage inflicted.

## Authoring & Config
- Extend `EnvironmentEffectCatalog` ScriptableObject with effect presets (fog bank, dust storm, blizzard, lightning storm, tornado, hurricane, nebula).
- Provide `EffectSpawnProfile` assets mapping conditions (season, location, tech) to effect triggers.
- Add `EffectVisualizationProfile` for presentation hints (particle system ids, color schemes).
- Bakers convert catalog entries into blob assets referenced by scheduler.

## Technical Considerations
- **SoA storage** for active effects: arrays for position, radius, intensity, tag to ensure Burst performance.
- **Spatial hashing**: store volume cell indices in hashed lists; avoid dynamic allocations by pooling `NativeList<int>` per effect.
- **Determinism**: use hashed seeds per effect instance for random behavior (lightning strike order, tornado path). Ensure playback recomputes volumes identically.
- **Performance**: throttle updates (e.g., volume sampling every N ticks). Spread heavy effects across frames.
- **Threading**: process volumes in `IJobParallelFor` batches; avoid branching by grouping effect types.
- **Rewind**: record effect activation events; recompute contributions deterministically on playback. For long-lived storms, store minimal path state ( position/velocity ) per tick.
- **3D Support**: for Space4x, volumes use nav volume adjacency and sample with 3D coordinates; ensure effect intensity informs flight navigation and perception.

## Testing
- Unit tests verifying effect intensity/falloff calculations and interaction with climate/perception grids.
- Integration tests for combined effects (fog + lightning) with gameplay systems (villager perception, fleet navigation).
- Stress tests with multiple concurrent storms affecting large regions.
- Determinism tests covering effect activation, random seeds, and playback.

## Tooling
- In-editor effect preview (position, radius, intensity curve).
- Runtime overlay showing active effect volumes and intensity heatmaps.
- Debug commands to spawn/clear effects for QA.
- Analytics log of effect events for balancing.
