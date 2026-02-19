# Space4X Frontend + Rendering Contract

**Status:** Required policy  
**Scope:** Main menu, ship select, camera handoff, and runtime DOTS visibility  
**Last Updated:** 2026-02-18

## Why This Exists

Space4X repeatedly hit the same avoidable failures:

- Menu code compiled but used fragile `OnGUI` patterns for player-facing flow.
- Namespace collisions (`Camera`, `Time`, `Input`, `main`) caused compile breaks.
- Scene/input handoff was implicit, so menu/gameplay states overlapped.
- Entities existed in ECS but were not visible due to missing render preflight checks.

This contract is the baseline every agent must follow before changing frontend or rendering behavior.

## 1. Frontend Architecture Rules (Required)

- Player-facing UI uses **UI Toolkit** or **uGUI**.
- `OnGUI` is allowed only for short-lived debug overlays and must be marked temporary.
- Model frontend as explicit states:
  - `MainMenu`
  - `ShipSelect`
  - `Loading`
  - `InGame`
- Do not blend states (for example, no active main menu buttons while in `InGame`).

## 2. Input Ownership Rules (Required)

- UI states (`MainMenu`, `ShipSelect`) use UI input only.
- Gameplay state (`InGame`) uses gameplay input only.
- Switch input maps during state transitions; do not poll both modes at once.
- Avoid hidden dependencies on legacy polling when Input System actions exist.

## 3. Scene Transition Rules (Required)

- Start-run transitions use async loading (`LoadSceneAsync` or ECS scene async APIs).
- Prefer `LoadSceneMode.Single` when entering playable run scenes.
- Use additive loading only when intentionally keeping a persistent background/system scene.
- If additive is used, define explicit unload ownership and timing.

## 4. Namespace Hygiene Rules (Required)

Presentation scripts must avoid Unity namespace collisions:

- Use aliases in mixed-context files:
  - `using UCamera = UnityEngine.Camera;`
  - `using UInput = UnityEngine.Input;`
  - `using UTime = UnityEngine.Time;`
- Do not use ambiguous identifiers like `main` for camera variables.
  - Use `mainCamera` or `activeCamera`.
- For DOTS math types, import or alias explicitly:
  - `using Unity.Mathematics;` or `using float3 = Unity.Mathematics.float3;`

## 5. DOTS Rendering Preflight (Required Before Claiming "No Entities")

Check in this order:

1. URP + Entities Graphics are active for the running scene.
2. Active SubScene/scene includes baked render catalog data and render mesh array.
3. Target entities have transform + render path data expected by catalog resolve/apply systems.
4. Resolve/apply/presenter systems are running in the expected world.
5. Camera culling/layer/frustum is valid for the spawned entities.
6. Diagnostic counts confirm: entities exist, are render-tagged, and are inside view.

Do not declare rendering broken until this checklist is complete.

## 6. FleetCrawl Flow Contract (Current Slice)

- `New Game` in main menu opens `ShipSelect` (not directly into combat control).
- `ShipSelect` includes:
  - Left/right selection through starter ship presets.
  - Difficulty control.
  - `Start Run` confirmation.
- `Continue` and `Multiplayer` remain visible but clearly disabled until implemented.
- `Start Run` loads the FleetCrawl scenario with selected preset+difficulty and hands camera/input to the player vessel.

## 7. Data Contract for Ship Select

- Ship presets should be data assets (ScriptableObject or equivalent data-driven config), not hardcoded UI strings.
- Difficulty is stored in a run-start config payload passed into scenario bootstrap.
- Run-start payload includes scenario routing (`scenarioId`, `scenarioPath`) and deterministic seed for launch parity.
- The run-start payload is the single source of truth for initial player ship and difficulty.

## 8. Definition of Done for Frontend/Rendering Changes

Before closing a task, confirm:

- No compile errors from namespace ambiguity (`CS0118`, `CS0119`, `CS0723`, `CS1061`, `CS0246`).
- Main menu and ship-select transitions follow the state machine above.
- At least one carrier-scale entity and one vessel-scale entity are visible during runtime.
- Starting a run hands camera focus to player vessel control context.
- Disabled buttons (`Continue`, `Multiplayer`) do not trigger gameplay transitions.

## Related Docs

- `Docs/Guides/Unity_DOTS_Common_Errors.md`
- `Docs/Rendering/DOTS_Rendering_BestPractices.md`
- `Docs/Rendering/Space4X_RenderCatalog_TruthSource.md`
- `Docs/Rendering/Space4X_RenderKey_TruthSource.md`
