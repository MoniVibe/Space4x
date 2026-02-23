# FleetCrawl Bootstrap Contract

This document defines the *non-negotiable* bootstrap requirements for FleetCrawl so that
iterators can ship content without silently breaking presentation or scenario routing.

## Canonical Requirements

1. **Scenario Authority**
   - `space4x_fleetcrawl_core_micro` is the canonical FleetCrawl scenario.
   - `SPACE4X_MODE` + `SPACE4X_SCENARIO_PATH` must resolve to that scenario in editor and validator.

2. **Smoke Scene SRP Fallback**
   - `Assets/Scenes/TRI_Space4X_Smoke.unity` must reference `ScenarioURP.asset` as its fallback SRP asset.
   - The fallback asset GUID must match `Assets/Resources/Rendering/ScenarioURP.asset.meta`.

3. **Render Catalog Parity**
   - `Assets/Data/Space4XRenderCatalog_v2.asset` and `Assets/Resources/Space4XRenderCatalog_v2.asset`
     must be byte-identical.
   - `Space4XAutoRenderCatalogBootstrap` must load `Resources/Space4XRenderCatalog_v2.asset` at runtime.

4. **FleetCrawl Scenario Flags**
   - `applyReferenceFrames` should remain `false` unless explicitly testing frame/orbit behavior.
   - `orbitalBand.enabled` should remain `0` for FleetCrawl baseline.
   - `renderFrame.useBandScale` should remain `0` for FleetCrawl baseline.

## Guardrail Checklist

Before merging iterator content:
- Run `Tools/ScenarioReferenceCheck.ps1`.
- Confirm canonical scenario path in console logs (`[Space4XScenarioRef]`).
- Confirm SRP fallback asset guid matches `ScenarioURP.asset.meta`.
- Confirm render catalog parity (Data/Resources identical).

If any check fails, do **not** merge until the baseline is restored.
