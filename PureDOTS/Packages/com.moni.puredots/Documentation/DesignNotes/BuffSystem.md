# Buffs & Debuffs System Concepts

## Goals
- Provide a unified framework for status effects (buffs, debuffs, conditions) usable by both games.
- Support stacking rules, durations, triggers, special effects (VFX/SFX), and integration with services (combat, industrial, perception).
- Ensure Burst-friendly data, deterministic application/removal, and rewind safety.

## Data Model
- `BuffDefinition` ScriptableObject:
  - `BuffId` (ushort), `Name`, `Description`.
  - `DurationType` (instant, timed, sustained, permanent).
  - `DurationSeconds`, `TickCadence`.
  - `Stacks` (stackable, replace, refresh).
  - `Categories` (combat, morale, industry, climate, space).
  - `Modifiers` (stat adjustments, multipliers, tags).
  - `Triggers` (on apply, on tick, on expire, on event type).
  - `VfxPresetId`, `SfxPresetId`.
  - `Flags` (dispellable, persistent across zones, hidden).
- Bakers compile definitions into `BuffCatalogBlob` for runtime lookup.
- `ActiveBuff` buffer on entity:
  ```csharp
  public struct ActiveBuff : IBufferElementData
  {
      public ushort BuffId;
      public float StartTime;
      public float Duration;
      public byte StackCount;
      public float Accumulator; // for tick cadence
      public BuffStateFlags Flags; // e.g., recently applied
      public Entity Source; // optional
  }
  ```
- `BuffModifier` component:
  - Aggregated stat deltas (float3/float4 for key attributes) computed from active buffs.
  - Split by domain (combat, economy, perception) to avoid large structs.

## Application Flow
1. **BuffApplicationRequest** buffer (per entity or global):
   - Populated by gameplay systems (abilities, industrial policies, environmental effects).
   - Contains `BuffId`, `DurationOverride`, `StackAdjustment`, `Source`.
2. `BuffApplicationSystem`:
   - Validates against `BuffCatalog`.
   - Applies stacking rules:
     - **Stackable**: increment stack up to max.
     - **Replace**: remove existing before apply.
     - **Refresh**: reset duration only.
   - Updates `ActiveBuff` buffer, sets `StartTime`, `Duration`.
   - Emits `BuffAppliedEvent`.
   - Handles VFX spawn via command buffer (authoring-specified prefab).
3. `BuffTickSystem`:
   - Runs each simulation tick or per cadence.
   - Decrements duration (`Duration -= deltaTime`).
   - Computes periodic triggers (e.g., DoT/HoT) by accumulating elapsed time.
   - Applies tick modifiers (damage, healing, resource change).
   - Emits `BuffTickEvent`.
4. `BuffExpirySystem`:
   - Removes expired buffs, triggers on-expire effects, despawns VFX, emits `BuffExpiredEvent`.
   - Uses ECB or enableable components for toggling.

## Modifier Aggregation
- `BuffAggregationSystem`:
  - Each tick (or when dirty) recomputes aggregated modifiers.
  - For each buff, apply stat modifications defined in catalog (e.g., `+10% attack`, `-15% production`).
  - Store results in `BuffModifier` component to avoid per-system iteration.
  - Use SoA buffers (separate arrays for additive/multiplicative modifiers).
- Systems (combat, industrial, perception) read `BuffModifier` to adjust behavior.

## Triggers & States
- Trigger types:
  - **OnApply**: run effect once (heals, immediate stat change).
  - **OnTick**: repeated effect (DoT/HoT every N seconds).
  - **OnExpire**: final action when buff ends.
  - **OnEvent**: respond to external events (damage taken, facility uptime drop).
- Buff states encoded in `BuffStateFlags` (applied, ticking, expiring, suspended).
- Support `Suspended` flag for pause (e.g., entering safe zone).

## VFX/SFX Integration
- `BuffVisualRegistry` linking `BuffId`→prefab reference.
- On apply, spawn pooled VFX entity attached to target (companion entity for presentation).
- On expire, send command to despawn or fade out.
- Ensure presentation world reads buff events and handles gracefully under rewind (ID-based).

## Technical Considerations
- Use Burst-friendly data: avoid managed references in runtime components.
- Keep `ActiveBuff` buffers small (limit per entity; use pooling).
- Deterministic ordering: sort application requests by `BuffId` before apply.
- Rewind compatibility: record buff events (`BuffHistorySample`) or recompute from event log.
- Tag buff systems to run in appropriate groups (combat, industry, climate) after applying domain logic but before consumers.
- Avoid per-frame allocation by pooling request buffers.
- Use `IEnableableComponent` for toggling simple status tags (e.g., `StunnedTag`).

## Authoring & Config
- `BuffCatalog` asset edited by designers; support partial reload.
- Provide validation (stack rules, missing modifiers).
- Support runtime overrides via policy decisions (e.g., industrial buffs triggered by management).

## Integration Points
- **Combat**: damage multipliers, resistance, status effects (stun, slow).
- **Industrial**: facility throughput bonuses, workforce morale boosts/penalties.
- **Perception**: vision buffs (`BlessingOfSight`), stealth debuffs.
- **Environmental effects**: storms apply slow/fear debuffs.
- **Narrative**: quest rewards apply timed buffs; story events remove them.
- **Skill progression**: buffs can adjust XP gain rates.
- **Metric engine**: events for buff application feed analytics dashboards.

## Testing
- Unit tests: stacking, duration, triggers.
- Integration tests: buff modifiers affecting combat/resource output.
- Rewind tests: ensure buffs reapply consistently.
- Performance tests: mass buff application (10k entities) within budget.
