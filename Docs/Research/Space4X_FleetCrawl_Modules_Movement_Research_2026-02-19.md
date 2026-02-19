# Space4X Fleet Crawl Research Notes (2026-02-19)

## Scope
- Question: should Fleet Crawl keep flagship modules as simple equip now, or jump to in-hull 3D fitting ("robot arena") immediately.
- Goal: identify online best practices and map them to current Space4X runtime paths.

## Executive Call
- Keep `equip-first` for Fleet Crawl v1.
- Gate in-hull 3D fitting behind a separate v2 flag.
- Harden movement/camera invariants before increasing module interaction complexity.

## Why (Source-Backed)
- Vertical slices should prove production viability for current scope, not expand scope while core feel is still being tuned. Source: Rami Ismail on prototypes vs vertical slice.
- Fixed-step simulation with bounded stepping is the stable foundation for smooth motion; variable or collapsed integration amplifies jitter and non-linear behavior. Source: Gaffer "Fix Your Timestep".
- Unity Entities transform data has timing caveats:
- `LocalToWorld` can be out of date or include graphical smoothing offsets during `SimulationSystemGroup`.
- `ComputeWorldTransformMatrix` is recommended for accurate simulation-time world transforms.
- Source: Unity Entities transform concepts and `LocalToWorld` API docs.
- Unity Input pointer delta semantics matter for click-to-steer:
- `Pointer.delta` resets to zero every frame and accumulates multiple changes in one frame.
- This can cause jumpy control if used as a persistent anchor baseline.
- Source: Unity Input System pointer docs.
- Unity update order guidance supports camera follow in late frame stages to reduce visible jitter between mover and camera. Source: Unity script execution order docs.
- Large-world / multi-scale simulation in industry engines explicitly uses higher precision and careful coordinate management to avoid precision drift. Source: Unreal Engine 5 LWC docs.
- Camera dead/soft zones are a standard way to avoid immediate camera pull when framing target movement. Source: Unity Cinemachine follow docs.
- Production hardening is safer with explicit decision records and runtime toggles:
- ADRs capture context, options, and consequences.
- Feature toggles let us ship low-risk behavior while staging higher-risk work.
- Sources: adr.github.io and Martin Fowler feature toggles.

## Mapping To Current Space4X Code
- Module path already supports equip-style runtime:
- Scenario toggles default loadouts: `Assets/Scenarios/space4x_fleetcrawl_survivors_v1.json`.
- Default loadouts and module entity creation: `Assets/Scripts/Space4x/Scenario/Space4XMiningScenarioSystem.cs`.
- Module-to-weapon normalization and limb/organ profiles: `Assets/Scripts/Space4x/Systems/Modules/Space4XModuleNormalizationSystems.cs`.
- Combat consumes `WeaponMount` and limb state: `Assets/Scripts/Space4x/Registry/Space4XCombatSystem.cs`.
- Camera/input path already has click-anchor deadzone primitives:
- `Space4XPlayerFlagshipController` uses anchor + deadzone gating for cursor steering.
- `Space4XFollowPlayerVessel` uses orbit/follow interpolation and mode-dependent yaw behavior.
- Movement stutter risk remains where flagship integration collapses multiple ticks into one larger `dt`:
- `Space4XPlayerFlagshipInputSystem` computes `dt = FixedDeltaTime * tickStepCount`.
- This is deterministic but can produce chunked acceleration/rotation under frame pressure.

## Recommended Work Order

## Phase 1: Fleet Crawl v1 Hardening (now)
- Keep module UX to slot/equip, power, heat, and limb integrity.
- Do not implement in-hull 3D placement UI in this phase.
- Update flagship integration to iterate per fixed tick step (small fixed substeps) instead of one collapsed large step.
- Ensure camera target sampling uses simulation-authoritative pose path consistently and smooths render-only interpolation without changing simulation truth.
- Add click-anchor steering behavior contract:
- On mouse-down, anchor at pointer position.
- No steering until leaving deadzone radius.
- While held, steer from anchor offset only.
- On mouse-up, clear anchor.

## Phase 2: Forward-Compatible Data (parallel-safe)
- Add non-UI fields needed for future 3D fitting:
- `module_bounds`
- `attachment_points`
- `power_draw_curve`
- `heat_output_curve`
- `clearance_class`
- Keep these fields inert in v1 gameplay, validated by schema tests only.

## Phase 3: Robot Arena v2 (flagged)
- Introduce `ship_fitting_v2` feature toggle.
- Add occupancy and collision solver inside hull volume.
- Add fitting editor UX and save/load for module transforms.
- Ship only when movement/camera invariant suite is green in CI and playmode.

## Acceptance Invariants To Enforce
- Flagship remains in camera frame center tolerance while not in RTS mode.
- WASD local basis matches ship forward/right/up consistently in both cursor and cruise modes.
- No position "ping-pong" between two truths (camera probe and motion diagnostics agree on single authoritative pose).
- Click-anchor deadzone has zero orientation jump on mouse-down.
- Module equip changes propagate deterministically to `WeaponMount` and capability outputs.

## Runtime Hygiene Guardrails
- Keep one authority per concern:
- Ship motion authority: fixed-step movement systems.
- Camera authority: presentation follow system reading authoritative pose.
- Avoid hidden dual writes to `LocalTransform` for the same entity in the same phase unless explicitly ordered and documented.
- Record any group-order or authority change as an ADR.

## Sources
- Rami Ismail, "Prototypes and Vertical Slice": https://ltpf.ramiismail.com/prototypes-and-vertical-slice/
- Gaffer on Games, "Fix Your Timestep!": https://www.gafferongames.com/post/fix_your_timestep/
- Unity Entities docs, "System update order": https://docs.unity.cn/Packages/com.unity.entities@1.2/manual/systems-update-order.html
- Unity Entities docs, "Transform concepts": https://docs.unity.cn/Packages/com.unity.entities@1.0/manual/transforms-concepts.html
- Unity Entities API, `LocalToWorld`: https://docs.unity.cn/Packages/com.unity.entities@1.0/api/Unity.Transforms.LocalToWorld.html
- Unity Input System docs, "Pointer": https://docs.unity.cn/Packages/com.unity.inputsystem@1.13/manual/Pointer.html
- Unity Input System docs, "Interactions": https://docs.unity.cn/Packages/com.unity.inputsystem@1.6/manual/Interactions.html
- Unity Manual, "Event function execution order": https://docs.unity3d.com/Manual/execution-order.html
- Unreal Engine docs, "Large World Coordinates": https://dev.epicgames.com/documentation/en-us/unreal-engine/large-world-coordinates-in-unreal-engine-5
- Unity Cinemachine docs, "Follow camera": https://docs.unity3d.com/Packages/com.unity.cinemachine@2.8/manual/CinemachineBodyTransposer.html
- Architecture Decision Records hub: https://adr.github.io/
- Martin Fowler, "Feature Toggles": https://martinfowler.com/articles/feature-toggles.html
