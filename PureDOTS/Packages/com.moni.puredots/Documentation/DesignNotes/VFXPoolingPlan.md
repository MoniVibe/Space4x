# VFX Pooling Plan

## Goals
- Centralize VFX instantiation for miracles, buffs, environmental effects, and industrial events.
- Avoid runtime allocations/spawn spikes; reuse pooled entities with deterministic lifecycle control.
- Provide authoring workflow tying `BuffId`, `EffectId`, `EventType` to VFX presets.

## Architecture
- Extend `PoolingCoordinatorSystem` and `SceneSpawnSystem` to manage VFX pools.
- Maintain `VfxPoolRegistry` singleton:
  - `PoolId`, `Prefab`, `InitialSize`, `MaxSize`, `AutoExpand`, `Category` (buff, environment, combat, industrial).
  - Blob generated from `VfxPoolCatalog` ScriptableObject.
- `VfxSpawnRequest` buffer:
  ```csharp
  public struct VfxSpawnRequest : IBufferElementData
  {
      public ushort PoolId;
      public float3 Position;
      public quaternion Rotation;
      public float Duration;
      public Entity AttachTo; // optional companion entity
      public VfxSpawnFlags Flags; // e.g., FollowTarget
  }
  ```
- `VfxRecycleRequest` buffer for manual despawn or event-driven cleanup.

## Flow
1. Gameplay systems (buffs, environmental effects, miracles) enqueue `VfxSpawnRequest` referencing `PoolId`.
2. `VfxPoolingSystem` (Presentation group) processes spawn requests:
   - Pop entity from pool; if empty and `AutoExpand`, instantiate via `SceneSpawnSystem` using pooled prefab.
   - Initialize transforms, attach follow component if needed.
   - Schedule auto recycle based on `Duration` (via scheduler or tick accumulation).
3. `VfxUpdateSystem` handles follow/position updates, lifetime countdown, and optional color/intensity changes.
4. `VfxRecycleSystem` recycles entities on duration expiry or explicit request, disabling renderers and returning to pool.

## Authoring
- `VfxPoolCatalog` asset maps `BuffId`, `EnvironmentEffectId`, `MiracleId`, `IndustrialEventId` to pool definitions.
- Provide inspectors to preview prefabs, configure pooling caps, set follow anchors.
- Designers define categories to allow bulk tuning (e.g., all industrial VFX share performance budget).

## Integration Points
- `BuffSystem`: on apply/expire events spawn/destroy VFX via pool.
- `EnvironmentalEffects`: storms, tornadoes, fog spawn persistent VFX with follow anchors.
- `SpatialBrushAndSelection`: brush paints spawn decal VFX pooled by size.
- `MetricEngine`/UI: highlight spikes or achievements with pooled effects.
- Audio: optional pairing with audio pool using same request id.

## Technical Considerations
- Keep pooled VFX on presentation/companion archetypes; ensure they are excluded from hot archetypes.
- Use enableable render/audio components to toggle without structural changes.
- Track active count per pool; expose metrics for budget enforcement.
- For GPU instancing (Entities Graphics), pools provide GPU buffer handles to avoid re-upload.
- Deterministic replays: replay VFX spawn events with same ordering; rely on event ids instead of randomness.
- Handle rewind by recycling all active VFX when entering playback, then respawn from simulation events after resume.

## Testing
- Playmode tests spawning large numbers of VFX (10k) to verify pooling works without GC spikes.
- Rewind tests: ensure VFX despawn/resync correctly.
- Stress tests mixing buff/environment/industrial VFX to confirm pool caps and fallback behavior.

## Tooling
- Runtime overlay listing pools (active vs. capacity, expansion count).
- Editor validation to ensure every `BuffId`/`EffectId` referenced by gameplay has matching pool entry.
- CI metric: fail if pool expansions exceed threshold during automated soak run.
