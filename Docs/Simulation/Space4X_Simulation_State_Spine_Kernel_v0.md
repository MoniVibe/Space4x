# Space4X Simulation State Spine Kernel v0

## Intent

Define the foundational kernel that all other kernels depend on:

- deterministic tick lane ownership
- scenario/mode runtime state
- bounded event envelope intake/history
- schema/version guardrails for persistence and migrations

This is Kernel-0 and should be hardened before deep coupling of combat, economy, progression, and diplomacy.

## Why this is first (web-backed)

1. Deterministic fixed-step lanes are the baseline for coherent simulation and replay.
   - Unity ECS fixed-step group guidance: `FixedStepSimulationSystemGroup`
   - https://docs.unity.cn/Packages/com.unity.entities@1.0/api/Unity.Entities.FixedStepSimulationSystemGroup.html
   - Fixed timestep rationale: https://www.gafferongames.com/post/fix_your_timestep/

2. ECS structural churn is a common failure mode; batching and bounded intake are required.
   - Unity sync-point guidance: https://docs.unity3d.com/Packages/com.unity.entities@1.4/manual/performance-sync-points.html
   - Unity ECB playback ordering guidance: https://docs.unity.cn/Packages/com.unity.entities@1.4/manual/systems-entity-command-buffer-playback.html

3. Determinism must be a project contract, not an assumption.
   - Unity Netcode prediction caveat: https://docs.unity.cn/Packages/com.unity.netcode@1.5/manual/intro-to-prediction.html
   - Unity prediction details: https://docs.unity.cn/Packages/com.unity.netcode@1.5/manual/prediction-details.html
   - Factorio determinism + core-count pitfalls: https://factorio.com/blog/post/fff-415

4. Long-lived simulation data needs additive schema evolution and version governance.
   - Protobuf schema evolution rules: https://protobuf.dev/programming-guides/proto2/

5. Event stream + snapshots is the practical architecture for replay/debug/recovery.
   - Event sourcing pattern: https://martinfowler.com/eaaDev/EventSourcing.html

## Kernel Contract (v0)

Milestone 0 introduces these runtime contracts:

- `Space4XSimulationStateSpineRootTag`
- `Space4XSimulationStateSpineMeta`
  - `SchemaVersion`
  - `RegistryVersion`
- `Space4XSimulationStateSpineConfig`
  - `Enabled`
  - `RecordEventHistory`
  - `MaxRetainedEvents`
  - `MaxIntakePerTick`
- `Space4XSimulationStateSpineState`
  - `Lifecycle`
  - `DeterministicLane`
  - `LastTick`
  - `ScenarioSeed`
  - `ScenarioHash32`
  - `LastEventSerial`
- `Space4XSimulationStateSpineOverflow`
  - per-tick and total dropped event counters
- `Space4XSimulationStateSpineEventInbox` (producer-facing envelope)
- `Space4XSimulationStateSpineEvent` (retained event history envelope)

System lane added:

- `Space4XSimulationStateSpineSystemGroup` under `FixedStepSimulationSystemGroup`
- `Space4XSimulationStateSpineBootstrapSystem`
- `Space4XSimulationStateSpineTickSystem`

## Milestone Plan (Depth-First)

### M0: Spine bootstrap and bounded envelope (implemented start)

Goal:

- create canonical root singleton
- run a fixed-step spine lane
- enforce bounded inbox consumption and bounded retained history

Acceptance:

- one and only one spine root exists
- no unbounded growth in spine event buffers
- overflow counters tick when producers exceed intake budget

### M1: Determinism probes and replay digest

Goal:

- add deterministic digest components at spine lane end
- verify same digest across repeated runs with same scenario/seed

Acceptance:

- deterministic digest stable for repeated same-seed replay
- digest matrix validated across worker/core-count variants

Implementation status (current):

- implemented in runtime spine contracts and tick lane:
  - `Assets/Scripts/Space4x/Runtime/Space4XSimulationStateSpineComponents.cs`
  - `Assets/Scripts/Space4x/Systems/StateSpine/Space4XSimulationStateSpineSystems.cs`
- validated by PlayMode tests:
  - `Assets/Scripts/Space4x/Tests/PlayMode/Space4XSimulationStateSpineDeterminismTests.cs`

### M2: Scenario and mode runtime state contract

Goal:

- move mode/scenario lifecycle transitions behind explicit spine state transitions
- remove implicit side-channel state mutation where possible

Acceptance:

- mode/scenario transitions are observable in one spine state log
- state transition legality table enforced (no invalid jumps)

### M3: Domain/family registry governance

Goal:

- introduce generated registry versioning for domain/family IDs
- enforce uniqueness and stability checks in validation

Acceptance:

- duplicate/unstable IDs fail validation
- unknown IDs at load map to inert fallback and telemetry warning

### M4: Kernel event producers migration

Goal:

- route combat, power/heat/signature, economy, and progression producers into inbox envelope
- coalesce overflow deterministically

Acceptance:

- all targeted domains emit through spine inbox
- no domain bypass in hot-path systems for milestone scope

### M5: Save/load checkpoint integration

Goal:

- add snapshot checkpoint handshake on spine root
- support additive migration and replay continuation from checkpoint

Acceptance:

- load from prior schema fixture succeeds with defaults
- replay resumes with deterministic digest continuity

## Notes for current sprint

- Prefer deep completion of M0 and M1 over partial M2+.
- Keep all behavior data-driven and bounded before adding new domain complexity.
- Use the spine overflow telemetry as the first capacity alarm during stress tests.
