# Presentation Layer Guidelines

> Tracking for open items lives in `Docs/TODO/PresentationBridge_TODO.md`; use this file for principles/patterns and keep both in sync.

## Principles
- Keep simulation (hot archetypes) separate from presentation (cold archetypes/companion entities).
- Presentation reads data; never mutates simulation components directly.
- Tolerate rewind: presentation systems pause or rebuild visuals when `RewindState` enters playback.
- Burst where possible; fall back to main thread only for engine APIs (render, audio).

## Architecture
- `PresentationSystemGroup` runs after simulation with `PresentationRewindGuardSystem` guarding playback.
- Simulation entities expose lightweight handles (ids, event streams, transform refs).
- Companion entities (in presentation world or same world with cold archetype) store render/VFX/UI state.
- `PresentationSyncSystem` jobs gather simulation data into `PresentationBuffer` components for consumption by rendering bridges.

### Hot vs. Cold Split
- Hot archetype: essential components (`LocalTransform`, gameplay state).
- Cold archetype/companion: `PresentationHandle`, `RenderMeshArray`, `VfxAnchor`, `UiBinding`. Use enableable tags for toggling.
- Keep cold components out of hot chunk: use companion entity created via conversion or runtime spawn.

### Data Flow
1. Simulation emits events/buffers (`BuffAppliedEvent`, `AreaSelectionEvent`, industrial metrics).
2. Presentation aggregator systems read events, update companion buffers.
3. Rendering/VFX/UI scripts consume companion data (Entities Graphics, SubScene MonoBehaviours anchored to ECS data).

## Sync Patterns
- Use deterministic buffers (`DynamicBuffer<PresentationCommand>`) for state changes.
- Apply interpolation/extrapolation on presentation companions only; store `LastSimTick` for reconciliation.
- For transforms, copy `LocalTransform` to companion with smoothing (lerp) in presentation world.
- Avoid direct references to simulation `Entity` in Mono scripts; use `EntityGUID` or `PresentationHandle`.

## UI & HUD
- Maintain `HudData` singletons per scope (village, fleet) updated by metric engine/registries.
- Presentation UI reads `HudData` snapshots each frame; queue events for tooltips/notifications.
- Use `EventSystemConcepts` to subscribe to gameplay events (buffs, contracts) and push to UI.
- Provide inspector helpers to map UI widgets to metric ids and buff catalogs.

## Animation & Entities Graphics
- Use Entities Graphics for crowd/ship rendering; store `MeshIndex`, `MaterialIndex`, `AnimationState` on companion components.
- `AnimationSyncSystem` maps simulation states (job phase, combat state) to animation clips.
- For procedural animation, keep logic in presentation world to avoid impacting simulation performance.

## Audio
- Mirror buff/environment events to audio command queue.
- Use pooling-friendly audio sources; stop playback on rewind or state reset.

## Rewind Handling
- `PresentationRewindGuardSystem` disables presentation update systems during catch-up.
- Presentation maintains minimal state; on resume, rebuild from latest simulation snapshot (clear VFX, repopulate companions).
- History buffers (`PresentationHistorySample`) optional for fade continuity.

## Testing & Tooling
- Provide playmode tests verifying companion generation, VFX spawn/despawn, UI updates under rewind.
- Editor gizmos to visualize presentation bindings (companion existence, missing VFX references).
- Profiling guidelines: track presentation frame cost separate from simulation.

## Checklist
- [ ] Companion archetypes defined per domain (villager, fleet, facility, buff).
- [ ] Presentation sync systems registered and Burst-compiled where possible.
- [ ] Rewind guard enforced; presentation rebuild path documented.
- [ ] UI widgets mapped to metric registry; validation ensures ids exist.
- [ ] VFX/audio events centralized through command buffers.
