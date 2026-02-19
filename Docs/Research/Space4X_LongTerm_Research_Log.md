# Space4X Long-Term Research Log

Use this file as the append-only memory for insights that should survive day-to-day implementation churn.

## Usage Rules

- Add new notes at the top of `## Entries` (newest first).
- Keep each entry short, concrete, and decision-oriented.
- Include source path/link when the point came from runtime logs, code, docs, or web research.
- If an item changes direction, add a new entry; do not silently rewrite history.

## Entry Template

```md
### YYYY-MM-DD - Short title
- Context: What problem or question this was about.
- Finding: What we learned that is likely to matter long term.
- Evidence: File/path, log snippet, profiling result, or URL.
- Decision: What we chose to do (or avoid) because of this.
- Follow-up: Concrete next action, owner, or trigger condition.
```

## Entries

### 2026-02-18 - Tiered nuisance filter keeps human time on feel, not regressions
- Context: Presentation iteration needs frequent visual checks, but many regressions are repeatable and machine-detectable.
- Finding:
  - A three-tier gate is practical:
    - Tier 1 hard fail (block manual pass),
    - Tier 2 warn (narrow manual pass),
    - Tier 3 human-only (feel/aesthetics).
  - Existing movement telemetry + run/invariant summaries can cover most Tier 1/2 simulation nuisances.
  - Camera alignment/orientation nuisances need a structured probe stream (`jsonl`) from runtime, not only on-screen HUD text.
- Evidence:
  - `scripts/presentation_nuisance_filter.ps1`
  - `Docs/Presentation/Space4X_Presentation_Nuisance_Filter.md`
  - `Docs/Presentation/Space4X_Presentation_Nuisance_Thresholds.json`
  - `Assets/Scripts/Space4x/Diagnostics/Space4XCameraFollowProbe.cs`
- Decision:
  - Standardize nuisance filtering as a pre-step before every manual visual pass.
  - Keep thresholds as data (`.json`) so tuning does not require script edits.
- Follow-up:
  - Feed probe + metrics from dedicated FleetCrawl presentation scenarios nightly.
  - Auto-post `green/yellow/red` digest in operator channel before asking for human eyes.

### 2026-02-18 - Headless telemetry is necessary but insufficient for camera/input bugs
- Context: Camera follow and mode-switch regressions were hard to diagnose from runtime console + headless movement telemetry alone.
- Finding:
  - Headless diagnostics validate simulation invariants, but camera/input issues are presentation-loop problems and need in-editor/runtime visual instrumentation.
  - Unity’s recommended workflow for these cases is a combination of runtime HUD state, Input Debugger, ECS Systems/Hierarchy windows, Profiler Timeline, and Frame Debugger.
  - Mode-switch bugs are commonly caused by update-order and multi-writer handoff issues (for us: RTS rig components vs follow camera state).
- Evidence:
  - Local code:
    - `Assets/Scripts/Space4x/UI/Space4XFollowPlayerVessel.cs`
    - `Assets/Scripts/Space4x/UI/Space4XPlayerFlagshipController.cs`
    - `Assets/Scripts/Space4x/Diagnostics/Space4XCameraFollowDebugHud.cs`
  - Unity docs:
    - Input Debugger (Input System): https://docs.unity.cn/Packages/com.unity.inputsystem@1.13/manual/Debugging.html
    - Entities Hierarchy window: https://docs.unity.cn/Packages/com.unity.entities@1.0/manual/editor-hierarchy-window.html
    - Entities Systems window: https://docs.unity.cn/Packages/com.unity.entities@1.0/manual/editor-systems-window.html
    - Managing update and execution order: https://docs.unity3d.com/ja/current/Manual/managing-update-order.html
    - CPU Usage Profiler Timeline view: https://docs.unity3d.com/es/2021.1/Manual/ProfilerCPU.html
    - Frame Debugger workflow: https://docs.unity3d.com/kr/6000.0/Manual/FrameDebugger-debug.html
- Decision:
  - Keep headless telemetry for simulation correctness.
  - Add gameplay-facing camera/input diagnostics HUD and treat camera target alignment as a first-class invariant during presentation-slice development.
- Follow-up:
  - Add a camera-follow invariant check in playmode tests (controller entity == follow target entity after mode changes).
  - Document a repeatable debug checklist in `Docs/Guides/` for camera/input regressions.

### 2026-02-18 - Believable movement needs a single fixed-step authority
- Context: Runtime movement still feels non-physical and occasionally unstable despite recent inertial tuning.
- Finding:
  - Unity ECS/Physics expects simulation authority in fixed-step loops; mixing `Update`/`LateUpdate` motion writes with fixed-step simulation causes visible inconsistency.
  - `LocalToWorld` can be stale during simulation and is not a safe authoritative source for gameplay transforms.
  - Our player ship currently writes transform from a MonoBehaviour `Update` path while AI movement also updates the same entity class in fixed-step, creating potential dual-writer conflicts.
  - Current player flight still includes hidden damping paths (`passiveDriftDrag`) and hard velocity clipping, which can read as "gamey correction" instead of physical thrust/retro behavior.
- Evidence:
  - Local code:
    - `Assets/Scripts/Space4x/UI/Space4XPlayerFlagshipController.cs`
    - `Assets/Scripts/Space4x/Systems/AI/VesselMovementSystem.cs`
    - `Assets/Scripts/Space4x/Runtime/Space4XMovementInertiaComponents.cs`
  - Unity docs:
    - Fixed-step group catch-up and default timestep: https://docs.unity.cn/Packages/com.unity.entities@1.0/api/Unity.Entities.FixedStepSimulationSystemGroup.html
    - Transform timing and `LocalToWorld` staleness note: https://docs.unity.cn/Packages/com.unity.entities@1.0/manual/transforms-concepts.html
    - Fixed update backlog behavior: https://docs.unity3d.com/Manual/fixed-updates.html
    - Time variation and `maximumDeltaTime` limits: https://docs.unity3d.com/Manual/time-handling-variations.html
    - Physics velocity/damping semantics: https://docs.unity.cn/Packages/com.unity.physics@1.0/api/Unity.Physics.PhysicsVelocity.html and https://docs.unity.cn/Packages/com.unity.physics@1.0/api/Unity.Physics.PhysicsDamping.html
    - Physics simulation modification points and ordering: https://docs.unity.cn/Packages/com.unity.physics@1.0/manual/simulation-modification.html
    - Scale baseline for believable physics/readability (`1 unit = 1 meter`): https://docs.unity3d.com/es/2021.1/Manual/BestPracticeMakingBelievableVisuals1.html
- Decision:
  - Treat movement authority as a hard contract: one writer, fixed-step only, for position/rotation/velocity.
  - MonoBehaviour/UI layer should emit intent only; simulation systems consume intent and apply motion.
  - Use explicit inertial modes (drift, dampeners, retro) as data-driven toggles, not hidden corrective behavior.
- Follow-up:
  - Add a manual-control tag/component consumed in `FixedStepSimulationSystemGroup` and exclude those entities from autonomous movement jobs.
  - Remove direct `LocalToWorld` writes from player MonoBehaviours; let transform systems resolve render matrices.
  - Add a small movement preflight diagnostic to flag entities written by more than one movement authority in the same tick.

### 2026-02-18 - Cursor-steer camera now follows ship heading via data settings
- Context: Request to avoid hardcoded behavior and make cursor-steer mode camera follow the ship orientation.
- Finding: Cursor mode previously relied on a fixed world-yaw orbit strategy; stable but not ship-heading-coupled.
- Evidence: `Assets/Scripts/Space4x/UI/Space4XFollowPlayerVessel.cs`, `Assets/Scripts/Space4x/UI/Space4XPlayerFlagshipController.cs`
- Decision:
  - Added serialized cursor-mode camera settings (`cursorModeFollowsShipHeading`, follow sharpness, heading offset, manual yaw offset limit).
  - Added serialized mode hotkey fields in both follow/controller scripts instead of hardcoded `1/2/3`.
- Follow-up:
  - Move camera + control tunables into dedicated profile assets (ScriptableObject) shared across scenes.
  - Add per-mode HUD indicator so testers know which camera/control profile is active.

### 2026-02-18 - Flagship flight model changed from arcade snap to inertial thrust
- Context: Movement felt too sharp and non-space-like; requirement is Avorion-like drift with retro braking.
- Finding: Previous flagship controller directly assigned frame velocity from input and wrote position immediately, producing near-instant starts/stops.
- Evidence: `Assets/Scripts/Space4x/UI/Space4XPlayerFlagshipController.cs`
- Decision: Use thrust/acceleration integration with persistent world velocity, speed caps per axis, optional inertial dampeners, and explicit hard-brake retros.
- Follow-up:
  - Validate feel tuning in-editor (`X` hard brake, `Z` dampeners toggle).
  - Move flight tuning to a ScriptableObject profile so scenarios can swap handling without code edits.

### 2026-02-18 - Scale ownership and tiny-entity failure mode
- Context: In gameplay runtime, entities rendered but appeared too tiny to recognize/control, especially non-carrier craft.
- Finding: `Space4XPresentationLifecycleSystem` was assigning carrier/miner `PresentationScale` from hardcoded defaults and did not apply `Space4XMiningVisualConfig` scale values; this made authored visual scale settings ineffective for those entity classes.
- Evidence:
  - Code before fix: `Assets/Scripts/Space4x/Presentation/Space4XPresentationLifecycleSystem.cs`
  - Runtime scale constants adjusted: `Assets/Scripts/Space4x/Presentation/Space4XPresentationLifecycleSystem.cs`, `Assets/Scripts/Space4x/Presentation/Space4XPresentationDepthSystem.cs`
  - Authoring default baseline adjusted: `Assets/Scripts/Space4x/Authoring/Space4XMiningScenarioAuthoring.cs`
  - Unity references:
    - Transform scaling and "1 Unity unit = 1 meter": https://docs.unity3d.com/2022.1/Documentation/Manual/BestPracticeMakingBelievableVisuals1.html
    - Camera clipping planes (visibility window): https://docs.unity3d.com/Manual/class-Camera.html
    - LOD percentage by screen height (readability over distance): https://docs.unity3d.com/Manual/class-LODGroup.html
    - DOTS transform scale semantics (`PostTransformMatrix` for non-uniform scale): https://docs.unity.cn/Packages/com.unity.entities%401.0/manual/transforms-concepts.html
- Decision: Make presentation lifecycle the explicit owner of fallback scale defaults and wire visual singleton scales into carrier/miner `PresentationScale` assignment.
- Follow-up:
  - Validate in-editor with flagship selection flow and log on-screen apparent size by class.
  - Add an in-game debug readout for effective final scale (`poseScale * presentationScale * multiplier`) to prevent future regressions.

### 2026-02-18 - Log initialized
- Context: Need a durable place for cross-session research memory.
- Finding: Research notes were spread across chats and ad-hoc docs.
- Evidence: User request to create appendable long-term research file.
- Decision: Create a single append-only log under `Docs/Research/`.
- Follow-up: Add first technical entries as rendering/menu findings are validated.
