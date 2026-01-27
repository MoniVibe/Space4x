# Shared Perception & Visibility System Concepts

## Goals
- Provide a unified perception framework for entities across Godgame and Space4x, accounting for visibility modifiers (fog, dust, blizzard, nebula, buffs/debuffs).
- Allow AI, gameplay logic, and presentation (fog-of-war) to query consistent visibility data.
- Integrate with spatial services, climate systems, and narrative events while remaining deterministic and Burst-friendly.

## Core Data Structures
- `PerceptionConfig` singleton:
  - Base sight/hearing radii per faction/archetype.
  - Global modifiers (night, storm severity).
  - Update cadence for perception sampling.
- `PerceptionSource` component:
  - `float BaseRadius`, `float HeightOffset`, `PerceptionMode` flags (vision, thermal, magical, radar).
  - `PerceptionTraits` (keen senses, blind).
- `PerceptionOccluder` component:
  - `OccluderType` (terrain, building, weather), `float Density`, `float Height`.
- `PerceptionModifierVolume` buffer:
  - Regions with visibility modifiers (fog/dust clouds, blizzards, nebulae) referencing climate or effect systems.
  - Contains `float VisibilityMultiplier`, `float DetectionPenalty`, `BuffTag`.
- `PerceptionResult` buffer on sources:
  ```csharp
  public struct PerceptionResult : IBufferElementData
  {
      public Entity Target;
      public float VisibilityScore; // 0-1
      public float Distance;
      public PerceptionModeMode ModeUsed;
      public uint LastSeenTick;
  }
  ```
- `FogOfWarCell` buffer (optional per layer): aggregated visibility state for map rendering.

## Perception Pipeline
1. **Sampling Phase** (`PerceptionSamplingSystem`):
   - Uses spatial grid to gather candidate targets within max radius.
   - For each candidate, evaluates line-of-sight (raycaster or cell stepping), occlusion density, and modifier volumes.
   - Computes `VisibilityScore`:
     ```
     score = baseSensorStrength * modeMultiplier
           * exp(-occlusionDensity * occlusionWeight)
           * environmentalVisibilityMultiplier
           * buffMultiplier;
     ```
   - If `score` exceeds threshold, target considered seen; update `PerceptionResult`, `LastSeenTick`.
2. **Decay Phase** (`PerceptionDecaySystem`):
   - Fades out entries not refreshed for `ForgetTicks`.
   - Emits events when visibility lost/regained (`PerceptionLostEvent`, `PerceptionGainedEvent`).
3. **Fog-of-War Update**:
   - Aggregate results into `FogOfWarCell` buffers (grid-based). Blend dynamic modifiers (fog, dust) with seen status.
   - Provide per-faction maps if required (Space4x fleets vs. villagers).
4. **Buff/Debuff Integration**:
   - Buff systems adjust source/target modifiers (e.g., `BlessingOfVision` +20%, `Smoke` -40%).
   - Effects send `PerceptionModifierVolume` updates; scheduler handles fade-in/out.

## Environmental Modifiers
- **Climate Hooks**: blizzards, snow storms, and dust storms from climate system register as modifier volumes with density curve over time.
- **Miracles/Spells**: create temporary fog/visibility buff volumes via event system.
- **Space Nebulae**: 3D volumes referenced by nav/physics; reduce sensor ranges, disrupt radar.
- **Night/Lighting**: time-of-day service exposes global visibility multiplier; torches/lamps act as local buffs.

## Queries
- `bool CanSee(Entity source, Entity target, PerceptionMode mode)` helper.
- `float GetVisibilityScore(Entity source, Entity target)` returning latest score.
- `PerceptionResult[] GetVisibleTargets(Entity source)` for AI decision-making.
- `bool IsCellVisible(int cellId, FactionId faction)` for fog-of-war.

## Authoring & Config
- `PerceptionProfile` ScriptableObject:
  - Default ranges per archetype (villager, guard, scout, ship class).
  - Mode strengths (vision, thermal, magical).
- `VisibilityModifierProfile`:
  - Fog density curves, storm strength vs visibility multiplier, color for HUD overlays.
- `BuffDefinition` link: specify perception bonuses/penalties.
- Bakers convert profiles to blobs for runtime.

## Technical Considerations
- **SoA storage**: store candidate data (positions, occlusion, modifiers) in separate arrays.
- **Parallel jobs**: use `IJobChunk`/`IJobParallelFor` to evaluate sight lines; rely on hashed grid for neighbor lookup.
- **Line-of-sight**: implement cell stepping (Bresenham/Batched DDA) for deterministic LoS on grid; for 3D, use voxel stepping.
- **Caching**: reuse candidate lists for multiple modes; evaluate additional modes (thermal, radar) without re-querying spatial grid.
- **Determinism**: sort perception results; avoid floating accumulation by using hashed seeds or fixed precision.
- **Rewind**: store minimal state (perception results, modifier versions) or recompute deterministically on playback.
- **Performance**: throttle sampling (e.g., every N ticks) and stagger across entities to balance frame time.

## Integration
- **AI Behavior Trees**: perception results populate blackboards (threat lists, target priorities).
- **Combat**: accuracy/ambush bonuses when target unseen; integrates with combat resolution.
- **Narrative**: events triggered when players detect/lose sight of key entities.
- **Presentation**: fog-of-war overlays, minimap updates, stealth indicators.
- **Telemetry**: log detection times, unseen durations, effectiveness of stealth buffs.

## Testing
- Unit tests for visibility score calculations under different modifier combinations.
- Integration tests mixing climate fog, buffs, and occluders with multiple perception modes.
- Stress tests for large numbers of sources/targets (villager city, fleet engagement).
- Determinism tests verifying consistent visibility across record/playback.
