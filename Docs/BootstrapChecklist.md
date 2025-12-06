# Space4X Bootstrap Checklist (Aligned to Unified ECS)

_Last updated: 2025-12-06_

Use this to bring up the Space4X demo/scene with correct ECS layers, ordering, and messaging.

## 1) Time Spine & Rewind (Body world)
- [ ] Ensure `TimeState`, `TickTimeState`, `RewindState` singletons exist before sim systems.
- [ ] Rewind guard system runs before any mutating systems.

## 2) Registries & IDs
- [ ] Initialize registries: Fleet/Carrier, Colony/Logistics, Resource, Anomaly, Route/Trade.
- [ ] Ensure `AgentGuid`/`AgentSyncId` components + mapping system are present and run early.
- [ ] Spatial grid config/state baked/initialized before any grid queries (sector/stellar grids).

## 3) Body Layer (60 Hz, deterministic, Burst)
- [ ] Groups: Reflex/HotPath/Combat/Perception/Physics as applicable (movement, intercepts, combat).
- [ ] Systems: movement/physics, fleet interception/combat, mining/hauling execution, module/system health, sensor/radar processing, logistics routes execution.
- [ ] Burst-enabled; deterministic RNG; rewind-guarded; no presentation code.

## 4) Mind Layer (?1 Hz, managed)
- [ ] Components: goals/roles (carrier tasks, fleet posture), research/tech diffusion intents, alignment/compliance, deception/intent layers (if used), time controls.
- [ ] Systems: goal selection, intent generation (intercept/escort/mine), tech diffusion decisions; slow cadence.
- [ ] No Entity handles crossing layers; uses IDs/messages only.

## 5) Aggregate Layer (?0.2 Hz)
- [ ] Components: empire/fleet aggregate goals, morale, doctrines/strategies.
- [ ] Systems: group decision (routes, posture), morale/cohesion, high-level logistics; slow cadence.

## 6) Bridges & Messaging
- [ ] `AgentSyncBus` present; Mind?Body (intents) ~250 ms; Body?Mind (telemetry/perception) ~100 ms.
- [ ] Messages are value-only (ID, intent kind, target, tick); no Entity handles cross-layer.
- [ ] Bridge systems run after time/registry init.

## 7) Presentation Boundary
- [ ] Presentation systems/MonoBehaviours in `PresentationSystemGroup` only; frame-time (`Time.deltaTime`).
- [ ] Presentation is read-only to sim; no simulation mutations from camera/UI.

## 8) Scene/Bootstrap Wiring
- [ ] Main scene includes baked spatial grid(s), registries, time spine, AgentSyncBus singletons.
- [ ] Spawn baseline entities via bakers/authoring (carriers/fleets, miners, asteroids, colonies, routes).
- [ ] Input/time controls (pause/speed/rewind) use command path, not direct mutation.

## 9) Performance Hygiene
- [ ] Use enableables for toggles; avoid add/remove churn.
- [ ] Keep archetypes small; blob static specs (ships/modules/sensors/modules/tech) in BlobAssets.
- [ ] No managed allocations in Burst jobs; buffers via indexed `for`; blob by `ref` only; deterministic RNG.

## 10) Validation / Headless
- [ ] Run headless scenario via ScenarioRunner (Space4X scenario or adapted Scenario_MillionAgents.json).
- [ ] Targets: Body < 10 ms, Mind < 3 ms, sync jitter < 0.1 ms; deterministic outputs; no managed allocations in Burst stack.
- [ ] Add/maintain a Space4X CI scenario (e.g., mining/intercept bootstrap) for nightly sanity.

## Notes
- Align system ordering with `RuntimeLifecycle_TruthSource.md`.
- Keep presentation separated; use bridges/commands for all game-side actions.
- Update this checklist as new systems/registries are added.
