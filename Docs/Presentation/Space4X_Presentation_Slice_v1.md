# Space4X Presentation Slice v1

**Status:** Active implementation plan  
**Date:** 2026-02-18  
**Goal:** Move from temporary smoke/debug presentation to a stable FleetCrawl-facing frontend and render path.

## Scope

- Runtime menu stack (`MainMenu -> ShipSelect -> Loading -> InGame`)
- Input ownership split (UI vs gameplay/camera)
- Deterministic DOTS render visibility checks
- Camera handoff from menu background to player vessel
- Scene transition contract for starting runs

## Source-Backed Baseline

1. DOTS rendering baseline

- Use Entities Graphics with SRP (URP/HDRP; built-in RP unsupported).
- Prefer baked entities/prefabs and `RenderMeshArray` + `MaterialMeshInfo`.
- Use `RenderMeshUtility.AddComponents` only for exceptional runtime creation paths.

2. UI/menu baseline

- Use UI Toolkit runtime UI for player-facing flow.
- Prefer UXML + USS + `UIDocument` with explicit `PanelSettings`.
- Keep all screen transitions in a single frontend state machine.

3. Input baseline

- Use Input System UI support for UI navigation/click/submit/cancel.
- Keep UI actions in the expected UI map/action names for automatic wiring.
- Switch action maps by state (`UI` in menu states, gameplay map in `InGame`).

4. Scene/loading baseline

- Use async scene transitions (`LoadSceneAsync`) for start-run handoff.
- Prefer `LoadSceneMode.Single` for gameplay entry unless additive ownership is explicit.
- If additive is used, unload intentionally and handle light probe notes as documented.

5. Data/config baseline

- Store ship presets and run-start defaults in ScriptableObject assets.
- Use lightweight preference storage only for menu/config values.
- If Addressables are used for UI/presentation assets, release handles explicitly.

## Implementation Order (Do This)

### Slice A: Frontend shell (first)

- Replace temporary `OnGUI` menu flow with UI Toolkit runtime UI.
- Keep current button set:
  - `New Game`
  - `Continue` (disabled)
  - `Multiplayer` (disabled)
  - `Settings`
  - `Quit`
- `New Game` transitions to `ShipSelect` state (not direct gameplay).

### Slice B: Ship select (second)

- Add ship preset selector (left/right).
- Add difficulty control.
- Add `Start Run` + `Back`.
- Persist selected preset+difficulty as a run-start payload.

### Slice C: Start-run handoff (third)

- On `Start Run`:
  - Switch input mode to gameplay.
  - Transition scene with async loading.
  - Disable smoke menu presentation.
  - Hand camera control to player vessel follow/controller.

### Slice D: Render confidence (parallel)

- Keep/extend diagnostics to verify:
  - `RenderSemanticKey` count
  - `MaterialMeshInfo` count
  - catalog/blob presence
  - render-bounds/culling sanity
- Do not close render bugs without preflight evidence.

## Implementation Snapshot (2026-02-18)

- `Space4XMainMenuOverlay` now uses UI Toolkit runtime UI (no player-facing IMGUI flow).
- `Start Run` now executes real async scene loading (`LoadSceneAsync`) before gameplay handoff.
- Ship selection is now data-driven via `Space4XShipPresetCatalog` ScriptableObject.
- Run-start payload now routes scenario id/path + seed, enabling FleetCrawl scenario switching from UI state.
- Editor bootstrap auto-creates a default catalog at `Assets/Resources/UI/Space4XShipPresetCatalog.asset` if missing.
- Runtime run payload is available via `Space4XRunStartSelection` for scenario/bootstrap consumers.
- A render preflight HUD (`F3` toggle) reports catalog/render/entity counts live for quick visibility triage.

## Acceptance Gates

- No IMGUI-driven player menu in normal runtime flow.
- Main menu and ship select are navigable via keyboard/gamepad UI actions.
- `New Game -> ShipSelect -> Start Run` path works with async loading.
- At least one carrier-scale and one vessel-scale entity are visible in runtime.
- Camera reliably transitions from menu background to player vessel control context.
- Presentation nuisance filter (`Docs/Presentation/Space4X_Presentation_Nuisance_Filter.md`) is green or justified yellow before full manual feel pass (prefer `scripts/presentation_mode1_headless.ps1` for Mode 1 checks).

## Local Project Notes

- Main menu + ship select controller: `Assets/Scripts/Space4x/UI/Space4XMainMenuOverlay.cs`
- Gameplay follow handoff: `Assets/Scripts/Space4x/UI/Space4XFollowPlayerVessel.cs`
- Run payload bridge: `Assets/Scripts/Space4x/UI/Space4XRunStartSelection.cs`
- Run payload scenario injector: `Assets/Scripts/Space4x/Scenario/Space4XRunStartScenarioSelectorSystem.cs`
- Ship preset data model: `Assets/Scripts/Space4x/UI/Space4XShipPresetCatalog.cs`
- FleetCrawl Survivors scenario: `Assets/Scenarios/space4x_fleetcrawl_survivors_v1.json`
- FleetCrawl Survivors headless proof: `Assets/Scripts/Space4x/Headless/Space4XFleetCrawlSurvivorsProofSystem.cs`
- Render preflight HUD: `Assets/Scripts/Space4x/Diagnostics/Space4XRenderPreflightHud.cs`
- Input actions asset already includes `UI`, `Player`, and `Camera` maps:
  - `Assets/InputSystem_Actions.inputactions`

## References

- Entities Graphics runtime creation (`RenderMeshArray`, `MaterialMeshInfo`):
  - https://docs.unity.cn/Packages/com.unity.entities.graphics@1.4/manual/runtime-entity-creation.html
- Entities Graphics feature matrix:
  - https://docs.unity.cn/Packages/com.unity.entities.graphics@1.4/manual/entities-graphics-versions.html
- Entities Graphics RP requirement (URP/HDRP):
  - https://docs.unity.cn/Packages/com.unity.entities.graphics@1.3/manual/requirements-and-compatibility.html
- Input System UI support and UI action map contract:
  - https://docs.unity.cn/Packages/com.unity.inputsystem@1.12/manual/UISupport.html
- PlayerInput and action-map switching:
  - https://docs.unity.cn/Packages/com.unity.inputsystem@1.6/manual/PlayerInput.html
- UI Toolkit runtime UI + panel settings:
  - https://docs.unity3d.com/Manual/UIE-HowTo-CreateRuntimeUI.html
  - https://docs.unity3d.com/Manual/UIE-Runtime-Panel-Settings.html
  - https://docs.unity3d.com/Manual/UIE-Runtime-Event-System.html
  - https://docs.unity3d.com/Manual/UIE-USS.html
- Async scene loading:
  - https://docs.unity3d.com/ScriptReference/SceneManagement.SceneManager.LoadSceneAsync.html
  - https://docs.unity3d.com/ScriptReference/SceneManagement.LoadSceneMode.Additive.html
- ScriptableObject data assets:
  - https://docs.unity3d.com/Manual/class-ScriptableObject.html
- PlayerPrefs (for lightweight settings only):
  - https://docs.unity3d.com/ScriptReference/PlayerPrefs.html
- Addressables memory lifecycle:
  - https://docs.unity.cn/Packages/com.unity.addressables@1.8/manual/MemoryManagement.html
