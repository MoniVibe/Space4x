# Space4X Frontend + Rendering Contract

This contract defines how FleetCrawl UI/UX is exposed for deterministic agent validation.

## Scope

- Scene: `Assets/Scenes/TRI_Space4X_Smoke.unity`
- Runtime shells: `Space4XMainMenuOverlay`, `Space4XInRunHudOverlay`
- Test harness scene strategy: PlayMode kernel tests run in isolated synthetic scenes (`SceneManager.CreateScene`) and instantiate overlays directly, so UI contract checks are not coupled to smoke-scene ECS simulation noise.
- Kernel contract: `Assets/Scripts/Space4x/UI/Space4XUiUxKernelContract.cs`
- Probe: `Assets/Scripts/Space4x/UI/Space4XUiUxKernelProbe.cs`
- Tests:
  - `Assets/Tests/PlayMode/Space4XUiUxKernelTests.cs`
  - `Assets/Tests/PlayMode/Space4XInRunHudKernelTests.cs`

## Kernel Contract

`Space4XMainMenuOverlay` must expose `CaptureKernelSnapshot()` with stable fields:

- UI state (`MainMenu`, `ShipSelect`, `Loading`, `InGame`)
- menu/panel visibility flags
- selected preset and difficulty
- active action map
- focused element
- primary control enabled state
- settings modal state and values:
  - `settings_visible`, `settings_dirty`, `settings_rebinding`, `settings_rebind_target`
  - video settings (`settings_quality_index`, `settings_quality_name`, `settings_fullscreen_mode`, `settings_resolution`)
  - HUD/audio settings (`settings_hud_scale`, `settings_audio_master`, `settings_audio_music`, `settings_audio_sfx`)
  - hotkey labels for HUD controls and layout editing (`settings_toggle_*`, `settings_ui_scale_*`, `settings_layout_reset_key`)

### Stable Element IDs

Use `Space4XUiUxElementIds` names on key controls so tests and probes can query without brittle selector heuristics.

Required IDs:

- `space4x.ui.root`
- `space4x.ui.container`
- `space4x.ui.panel.main_menu`
- `space4x.ui.panel.ship_select`
- `space4x.ui.button.settings`
- `space4x.ui.panel.settings`
- `space4x.ui.button.settings_apply`
- `space4x.ui.button.settings_close`
- `space4x.ui.dropdown.quality`
- `space4x.ui.dropdown.fullscreen`
- `space4x.ui.dropdown.resolution`
- `space4x.ui.slider.hud_scale`
- `space4x.ui.slider.audio_master`
- `space4x.ui.slider.audio_music`
- `space4x.ui.slider.audio_sfx`
- `space4x.ui.button.new_game`
- `space4x.ui.button.start_run`
- `space4x.ui.button.back`
- `space4x.ui.slider.difficulty`
- `space4x.ui.label.status`

Main menu scaling policy:

- Use a single menu scale multiplier (`UiScale` in `Space4XMainMenuOverlay`, currently `0.8`) and route panel/button/text spacing through helper functions rather than ad-hoc literal resizing.
- Keep settings modal fixed-size (non-resizable) for meta-menu stability in this slice.
- Persist settings payload in `PlayerPrefs` key `space4x.ui.settings.v1`.

### In-Run HUD Kernel

`Space4XInRunHudOverlay` must expose `CaptureKernelSnapshot()` with stable fields:

- hull health, shields, armor
- supplies, fuel, food
- crew capacity and ammunition (`crew_current/max/ratio`, `ammo_current/max/ratio`)
- auxiliary fuel reserve emphasis (`FuelReserveLabel`) in logistics stack
- power capacity vs available (`MW`)
- power deficit metrics (`power_deficit_mw`) and reason tags (`power_deficit_tags`)
- speed + engine telemetry (`speed_current_ups`, `engine_power_draw_mw`, `engine_fuel_draw_per_tick`) with estimate flags (`engine_power_estimated`, `engine_fuel_estimated`)
- force holo telemetry (`heading_deg`, local velocity channels, local acceleration channels, `accel_total_ups2`, `accel_estimated`, `forces_holo_visible`)
- minimap/feed visibility and tactical contact telemetry:
  - minimap render mode + camera readiness (`minimap_widget_3d`, `minimap_camera_ready`)
  - tracked contacts (`minimap_contacts_tracked`)
  - relation buckets (`minimap_self_count`, `minimap_ally_count`, `minimap_neutral_count`, `minimap_hostile_count`, `minimap_unknown_count`)
  - relation summary (`minimap_relation_counts`)
  - bounded contact window (`minimap_contacts_window_count`, `minimap_contacts[]`)
  - per-contact fields:
    - `entity_ref`
    - `relation`, `relation_score`, `relation_stance`, `relation_source`
    - `color_token` (semantic relation color)
    - `confidence`, `threat_level`
    - `distance`, `bearing_deg`, `relative_x`, `relative_z`, `speed`, `closing_speed`
- notification count
- notification text window payload (`notifications_latest`, `notifications_window_count`, `notifications_window`)
- panel placement telemetry (`hud_panel_x`, `hud_panel_y`)
- layout telemetry:
  - `layout_edit_mode`, `ui_global_scale`
  - status panel (`status_panel_x`, `status_panel_y`, `status_panel_w`, `status_panel_h`, `status_panel_scale`)
  - minimap panel (`minimap_panel_x`, `minimap_panel_y`, `minimap_panel_w`, `minimap_panel_h`, `minimap_panel_scale`)
  - target panel (`target_panel_x`, `target_panel_y`, `target_panel_w`, `target_panel_h`, `target_panel_scale`)
  - inventory panel (`inventory_panel_x`, `inventory_panel_y`, `inventory_panel_w`, `inventory_panel_h`, `inventory_panel_scale`)
  - notifications panel (`notification_panel_x`, `notification_panel_y`, `notification_panel_w`, `notification_panel_h`, `notification_panel_scale`)
  - speed panel (`speed_panel_x`, `speed_panel_y`, `speed_panel_w`, `speed_panel_h`, `speed_panel_scale`)
  - forces panel (`forces_panel_x`, `forces_panel_y`, `forces_panel_w`, `forces_panel_h`, `forces_panel_scale`)
  - power routing panel (`power_routing_panel_x`, `power_routing_panel_y`, `power_routing_panel_w`, `power_routing_panel_h`, `power_routing_panel_scale`)
  - production panel (`production_panel_x`, `production_panel_y`, `production_panel_w`, `production_panel_h`, `production_panel_scale`)
- fallback inventory primitive telemetry:
  - `inventory_visible`
  - `inventory_line_count`
  - `inventory_summary`
- production telemetry:
  - `production_visible`, `production_summary`, `production_facility_count`
  - selected facility state (`production_selected_entity_ref`, `production_selected_recipe_id`, `production_selected_shift`, `production_selected_power_scale`, `production_selected_queue_count`)
  - per-facility payload (`production_facilities[]`) including queue window entries (`queue[]`)
- time state (`paused`, `speed`, `tick`, `world_seconds`)
- action map and focused element

Stable IDs are defined in `Space4XInRunHudElementIds`, including:

- `space4x.ui.hud.root`
- `space4x.ui.hud.panel.root`
- `space4x.ui.hud.label.health`
- `space4x.ui.hud.label.shields`
- `space4x.ui.hud.label.armor`
- `space4x.ui.hud.label.supplies`
- `space4x.ui.hud.label.fuel`
- `space4x.ui.hud.label.fuel_reserve`
- `space4x.ui.hud.label.food`
- `space4x.ui.hud.label.crew`
- `space4x.ui.hud.label.ammo`
- `space4x.ui.hud.label.power`
- `space4x.ui.hud.panel.speed_widget`
- `space4x.ui.hud.panel.forces_holo`
- `space4x.ui.hud.label.heading`
- `space4x.ui.hud.label.velocity`
- `space4x.ui.hud.label.push`
- `space4x.ui.hud.label.speed`
- `space4x.ui.hud.label.engine_draw`
- `space4x.ui.hud.panel.power_routing`
- `space4x.ui.hud.panel.minimap`
- `space4x.ui.hud.minimap.viewport`
- `space4x.ui.hud.label.minimap_summary`
- `space4x.ui.hud.list.minimap_contacts`
- `space4x.ui.hud.panel.target`
- `space4x.ui.hud.label.target_primary`
- `space4x.ui.hud.label.target_secondary`
- `space4x.ui.hud.label.target_hull`
- `space4x.ui.hud.panel.inventory`
- `space4x.ui.hud.list.inventory`
- `space4x.ui.hud.label.inventory_hint`
- `space4x.ui.hud.label.inventory_ship`
- `space4x.ui.hud.column.inventory_segments`
- `space4x.ui.hud.column.inventory_cargo`
- `space4x.ui.hud.panel.production`
- `space4x.ui.hud.list.production`
- `space4x.ui.hud.list.production_queue`
- `space4x.ui.hud.panel.notifications`
- `space4x.ui.hud.list.notifications`
- `space4x.ui.hud.panel.utility_controls`
- `space4x.ui.hud.panel.time_controls`
- `space4x.ui.hud.button.toggle_pause`
- `space4x.ui.hud.button.toggle_inventory`
- `space4x.ui.hud.button.toggle_production`
- `space4x.ui.hud.button.time_half`
- `space4x.ui.hud.button.time_normal`
- `space4x.ui.hud.button.time_fast`

Runtime fallback behavior:

- If `SPACE4X_UIUX_FORCE_RUN_ACTIVE=1`, HUD is forced on (test harness mode).
- Without force mode, HUD will activate when:
  - main-menu kernel reports `run_active=1`, or
  - a playable flagship context exists (`Space4XPlayerFlagshipController` claim or `PlayerFlagshipTag` in ECS), or
  - active scene is `TRI_Space4X_Smoke` when smoke fallback is enabled.
- Minimap contact sourcing is layered:
  - always include a self anchor contact (`relation=self`, `color_token=self_cyan`) for deterministic UI validation,
  - use `PerceivedEntity` sensor buffers when available,
  - when sensor buffers are missing or empty, use fallback proximity sampling (`LocalTransform` + `HullIntegrity`) and classify relation via `ScenarioSide` / faction diplomacy lookup.
- Hotkeys:
  - defaults:
    - `` ` `` toggles HUD root visibility.
    - `I` toggles fallback inventory panel visibility.
    - `M` toggles minimap panel visibility.
    - `N` toggles notifications feed visibility.
    - `F7` toggles forces holo widget visibility.
    - `F8` toggles layout edit mode.
    - `[` / `]` decrease/increase global HUD scale.
    - `F9` resets HUD window layout + scale to defaults.
    - production panel toggles via utility pad `Prod` button (kernel command `ToggleProduction`).
  - defaults can be rebound in settings modal (`Settings Kernel v0`) and are persisted via `space4x.ui.settings.v1`.
  - `Ctrl + Mouse Wheel` over a window decreases/increases local window scale.
  - `H` remains reserved for legacy camera tick overlay control.
- HUD input behavior:
  - control buttons are mouse-activation only (keyboard/gamepad navigation focus is suppressed to avoid `WASD` stealing focus/highlighting UI controls).
  - drag/resize interactions are active only while layout edit mode is enabled.
- Runtime layout profile:
  - compact lower-left resource stacks (Hull/Shields/Armor, Supplies/Food/Water, Crew/Ammunition).
  - red fuel reserve bar duplicated in logistics stack for quick survivability scanning.
  - top-right compact control pads:
    - utility pad (`Map`, `Inv`, `Feed`, pause) separate from time-speed pad.
    - speed pad (`0.5x`, `1x`, `2x`) as small square buttons.
  - compact bottom-center speed/engine widget (speed + power/fuel draw telemetry).
  - opaque toggleable forces holo above speed widget for heading + push vector awareness.
  - circular minimap widget anchored top-right.
  - right-side notification feed column with clickable tiles that open contextual detail panel state.
  - floating production operations panel for facility selection, recipe queueing, shift control, power draw scaling, and limb selection.
- Kernel-driven layout controls:
  - `TrySetKernelElementPosition(id, absolutePosition)` supports:
    - `space4x.ui.hud.panel.root`
    - `space4x.ui.hud.panel.speed_widget`
    - `space4x.ui.hud.panel.forces_holo`
    - `space4x.ui.hud.panel.minimap`
    - `space4x.ui.hud.panel.target`
    - `space4x.ui.hud.panel.inventory`
    - `space4x.ui.hud.panel.power_routing`
    - `space4x.ui.hud.panel.production`
    - `space4x.ui.hud.panel.notifications`
  - all managed windows are clamped to viewport bounds.
  - layout (position/size/scale + global scale) is persisted in `PlayerPrefs` key `space4x.ui.hud.layout.v2`.
- Minimap anchor fallback order:
  - flagship ECS pose (`LocalTransform`/`LocalToWorld`) when available.
  - camera-derived ground anchor (for menu/unbound contexts).
  - fallback first valid `LocalTransform + HullIntegrity` entity anchor.
- Propulsion fuel policy (current slice):
  - base energy-generation fuel draw is represented by idle fuel usage.
  - extra propulsion fuel draw is only applied when boost (`Shift`) is held with thrust demand.
  - player movement no longer applies hard max-speed clamping; acceleration uses diminishing returns at higher speed.

## Probe Contract (JSONL)

`Space4XUiUxKernelProbe` writes JSONL snapshots with:

- menu kernel snapshot (`Space4XUiUxKernelSnapshot`)
- in-run HUD kernel snapshot (`Space4XInRunHudKernelSnapshot`)
- UI Toolkit visual-element snapshots (`Space4XUiUxElementSnapshot`)
- recent UI-related warnings/errors captured from Unity logs

Default output:

- `%LOCALAPPDATA%/../LocalLow/DefaultCompany/Space4x/space4x_uiux_kernel_probe.jsonl`

Environment controls:

- `SPACE4X_UIUX_KERNEL_PROBE=1|0` enable/disable probe
- `SPACE4X_UIUX_KERNEL_PROBE_OUT=<path>` explicit file path
- `SPACE4X_PROBE_OUT_DIR=<dir>` shared probe output directory
- `SPACE4X_AUTOSTART_RUN=0` disable auto run start for deterministic frontend tests
- `SPACE4X_AUTOSTART_SCENARIO_PATH=<Assets/Scenarios/*.json>` run-start scenario override for UI test harnesses
- `SPACE4X_AUTOSTART_SCENARIO_ID=<scenario_id>` optional override for scenario id label/logs
- `SPACE4X_AUTOSTART_SCENARIO_SEED=<uint>` optional run seed override
- `SPACE4X_AUTOSTART_FLAGSHIP_ANCHOR=auto|ship|station|colony` preferred initial control anchor claim target
- `SPACE4X_AUTOSTART_CONTROL_MODE=1|2|3|4|cursor|cruise|rts|god` apply initial control mode on run start
- `SPACE4X_AUTOSTART_CONTROL_VARIANT=0|1` optional mode variant boot toggle (for RTS/Divine rig planar lock and other mode-variant gates)
- `SPACE4X_UIUX_FORCE_RUN_ACTIVE=1|0` force HUD kernel to report run-active in isolated test harnesses (used for deterministic in-run HUD contract tests)

## Agent Validation Workflow (MCP)

1. Enter play mode.
2. Capture screenshot(s) for visual evidence.
3. Read console for warnings/errors.
4. Run PlayMode kernel suites: `Space4XUiUxKernelTests` + `Space4XInRunHudKernelTests`.
5. Correlate failures with probe JSONL and screenshot evidence.

Minimal MCP sequence:

- `manage_editor(action=play)`
- `manage_scene(action=screenshot, screenshot_file_name=<name>)`
- `read_console(action=get, count='50')`
- `run_tests(mode=PlayMode, test_names=Space4XUiUxKernelTests)`
- `run_tests(mode=PlayMode, test_names=Space4XInRunHudKernelTests)`
- `manage_editor(action=stop)`

CLI lane equivalent:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File Tools/RunUnityCliLane.ps1 `
  -CliRepoPath C:\dev\Tri\space4x `
  -TestPlatform PlayMode `
  -TestFilter "Space4XUiUxKernelTests|Space4XInRunHudKernelTests" `
  -SkipParityCheck `
  -IgnoreProjectLock
```

## Invariants

- UI/UX checks must rely on kernel snapshot + element IDs, not GameObject names only.
- Visual regressions require screenshot evidence and kernel/probe data in the same run.
- PlayMode tests must set `SPACE4X_AUTOSTART_RUN=0` when asserting menu-state flows.
- In-run HUD contract tests should use isolated harness scenes and `SPACE4X_UIUX_FORCE_RUN_ACTIVE=1` to validate HUD semantics without requiring smoke-scene simulation startup.
- Power telemetry for HUD agents should always include a non-empty `power_deficit_tags` string so downstream tooling can classify deficits without parsing free-form labels.
- Minimap tactical telemetry must always include relation color semantics (`relation` + `color_token`) for each exported contact window entry so agent tooling can evaluate hostility/friendliness without visual pixel parsing.
- Frontend instrumentation must not mutate simulation determinism state.
